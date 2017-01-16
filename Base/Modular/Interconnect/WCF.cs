//-------------------------------------------------------------------
/*! @file Interconnect/WCF.cs
 *  @brief Defines a service, a client, and related classes, that are used to allow WCF connections to provide remote access to Interconnect Values and Parts.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2015 Mosaic Systems Inc.
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
using System.Reflection;
using System.Linq;
using System.ServiceModel;
using System.Runtime.Serialization;
using MosaicLib.Utils;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Utils.StringMatching;
using MosaicLib.Utils.Pooling;
using MosaicLib.Time;
using MosaicLib.Modular.Action;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;

// Modular.Interconnect is the general namespace for tools that help interconnect Modular Parts without requiring that that have pre-existing knowledge of each-other's classes.
// This file contains the definitions for the underlying Modular.Interconnect.WCF namespace.

namespace MosaicLib.Modular.Interconnect.WCF
{
    #region IInterconnectPropagationSessionClientAPI, and IInterconnectPropagationSessionCommonAPI Contracts, and related classes and types.

    /// <summary>
    /// This is the client specific version of the InterconnectPropagationSessionAPI Contract - it is used by the client and is implemented by the server.
    /// In addtion this Contract is a duplex contact with the Callback using the IInterconnectPropagationSessionCommonAPI to allow the server to pass information to the client directly.
    /// </summary>
    [ServiceContract(Name = "InterConnPropagSessAPI", 
                    Namespace = Constants.ModularInterconnectNameSpace, 
                    SessionMode = SessionMode.Required, 
                    CallbackContract = typeof(IInterconnectPropagationSessionCommonAPI),
                    ProtectionLevel = System.Net.Security.ProtectionLevel.None)]
    public interface IInterconnectPropagationSessionClientAPI 
        : IInterconnectPropagationSessionCommonAPI
    {
        /// <summary>
        /// This method is used by the client to initiate the session within which the remaining methods are used.
        /// The nameMatchRuleSet parameter defines the set/sub-set of the server's names that the client is interested in being attached to.
        /// </summary>
        [OperationContract(IsInitiating = true)]
        void StartSession(StartSessionParameter param);

        /// <summary>
        /// Method used by the client to explicitly close a session.
        /// </summary>
        [OperationContract(IsInitiating = false, IsTerminating = true, IsOneWay = true)]
        void EndSession();
    }

    /// <summary>
    /// This is the packaged content for the values that are passed to a StartSession call.
    /// </summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public class StartSessionParameter
    {
        #region Serialzied Properties

        /// <summary>Passes the name of the client for this session.</summary>
        [DataMember(Order = 1)]
        public string ClientName { get; set; }

        /// <summary>Passes the name of the interconnect table that the client would like to connect to.  Use null/empty to connect to the default table.</summary>
        [DataMember(Order = 10)]
        public string InterconnectTableName { get; set; }

        /// <summary>
        /// The set of rules for what server side names shall be propagated to the client using this session.  
        /// If this parameter is passed as null then it will be replaced with and interpreted as MatchRuleSet.Any
        /// </summary>
        [DataMember(Order = 20)]
        public MatchRuleSet NameMatchRuleSet { get; set; }

        #endregion

        #region Non-Serialized parts (primarily for Service to identify the Client Connection on which this parameter was recieved).

        /// <summary>
        /// This non-serialized property is used by the Server side to record and track the client instances on which each Server side push call is handled.
        /// </summary>
        public IInterconnectPropagationSessionCommonAPI ClientCallbackInstance { get; set; }

        #endregion
    }

    /// <summary>
    /// This is the Common part of the InterconnectPropagationSessionAPI contract.  
    /// It represents the portion of the API/Protocol that is symetric, ie it is implemented in both sides of the duplex connection.
    /// The individual OperationContract (methods) in this contract are each marked as IsOneWay = true.  
    /// This is intended to allow them to used (called) by client and server as generally non-blocking calls (calls that return as soon as the work has been enqueued).
    /// In addition this contract is only expected to be used with ConcurrancyMode = Single so that the calls will be delivered in order.
    /// Generally we expect that the service provider will immediately requeue the passed information to allow the WCF infrastructure to start working to deliver the next
    /// call immedaitely.
    /// </summary>
    [ServiceContract(Name = "InterConnPropagCommonAPI", 
                    Namespace = Constants.ModularInterconnectNameSpace, 
                    ProtectionLevel = System.Net.Security.ProtectionLevel.None)]
    public interface IInterconnectPropagationSessionCommonAPI
    {
        /// <summary>
        /// This is the primary method in the protocol.  It delivers a single packaged parameter to the other side of the connectxion.
        /// Thie method is used to for multiple purposes in the protocol.  
        /// Primarily it is used to propagate sets of observed changes from one side of the connection to the other.
        /// It is also used to establish the mapping of names to session specific IDs which are only assigned by the server side of the connection.
        /// The difference between registration type propagation and simple value propagation is simply indicated by the presance of a non-empty string in the Name of the first item in the set.
        /// The client may inform the server of added names by propagating "AddName" items.  These are items that have a Name defined but no ID.  The server will then add these to the server's IVI table and then pass back an registration item
        /// with an empty value to acknowledge that added item.  Normally the client only does this after it believes the initial set of registrations have been propagated from the server to the client.
        /// </summary>
        /// <remarks>
        /// Both sides will use this method incrementally.  We expect that both side will send no more than 300 registration, reference value, or AddName items and no more than 1000 simple value propagation items per call.
        /// As such it may take a few calls to complete any given update cycle.
        /// In addition both sides will use the partial flag to indicate when the current call is part of a sequence that has not ended yet.  For example the first PropagateValues call from the server to the client
        /// with partial passed as false informs the client that the server has passed all of the initial registration items that it is going to do.
        /// </remarks>
        [OperationContract(IsInitiating = false, IsOneWay = true)]
        void Push(PushParameter param);
    }

    #region PushParameter, ValuePropagationItemList, and ValuePropagationItem

    /// <summary>
    /// This is the packaged content for each Push operation/method.  
    /// It contains a set of Properties that are used to conveigh/push protocol inforation from one end of the connection to the other.
    /// Individual properties in the class are used for different parts of the protocol.
    /// </summary>
    [DataContract(Name = "param", Namespace = Constants.ModularInterconnectNameSpace)]
    public class PushParameter
    {
        #region Acknowledgement and SeqNum

        /// <summary>
        /// When provided this contains a sequence number of the last PushParameter that was processed by the other end the connection (of this symetric protocol)
        /// This property is completely independent of the use of other properties in any given Push call.  
        /// However we expect that most Push calls will include an AckSeqNum.
        /// <para/>Note also that lack of acknowledgment will block/delay the sending side from continuing to post Push calls and if acknoweledgement does not arrive within
        /// a protocol specific period, the current session will be aborted
        /// </summary>
        [DataMember(Order = 10, EmitDefaultValue = false, IsRequired = false)]
        public Int32 AckSeqNum { get; set; }

        /// <summary>Predicate returns true if this propagation item's AckSeqNum is non-zero</summary>
        public bool HasAckSeqNum { get { return (AckSeqNum != 0); } }

        /// <summary>
        /// This property must be provided with a valid sender side sequence number for each push call that contains anything other than an AckSeqNum.  
        /// Without a non-zero SeqNum, the recieving side will not process any of the remaindure of the PushParameter.
        /// </summary>
        [DataMember(Order = 20, EmitDefaultValue = false, IsRequired = false)]
        public Int32 SeqNum { get; set; }

        /// <summary>Predicate returns true if this propagation item's SeqNum is non-zero</summary>
        public bool HasSeqNum { get { return (SeqNum != 0); } }

        #endregion

        #region Value Registration and Propagation

        /// <summary>
        /// This parameter contains a full or partial list of ValuePropagationItems, 
        /// each of which may be used for registration (Server->Client only), name addition requests (Client -> Server only), or normal value propagation,
        /// depending on their exact property usage and mix.  Generally the session starts by having the Server send a VPI for each matching name in the server's IVI.
        /// </summary>
        [DataMember(Order = 30, EmitDefaultValue = false, IsRequired = false)]
        public ValuePropagationItemList VPIList { get; set; }

        #endregion

        #region Remote Service Action Start and Update

        /// <summary>
        /// This parameter contains the list of RemoteServiceActionRequest items that are being pushed accross the connection on this iteration.
        /// </summary>
        [DataMember(Order = 40, EmitDefaultValue = false, IsRequired = false)]
        public RemoteServiceActionRequestList RSARList { get; set; }

        /// <summary>
        /// This parameter contains the list of RemoteServiceActionUpdate items that are being pushed accross the connetion on this iteration.
        /// </summary>
        [DataMember(Order = 50, EmitDefaultValue = false, IsRequired = false)]
        public RemoteServiceActionUpdateList RSAUList { get; set; }

        #endregion

        #region Non-Serialized parts (primarily for Service to identify the Client Connection on which each PushParameter was recieved).

        /// <summary>
        /// This non-serialized property is used by the Server side to record and track the client instances on which each Server side push call is handled.
        /// </summary>
        public IInterconnectPropagationSessionCommonAPI ClientCallbackInstance { get; set; }

        /// <summary>
        /// This gives the accumulated estimated size for all of the items in this PushParameter item.
        /// </summary>
        public int EstimatedContentSizeInBytes { get; set; }

        #endregion

        /// <summary>
        /// Method used immediately prior to returing this item the a freelist.
        /// </summary>
        internal void Clear()
        {
            AckSeqNum = 0;
            SeqNum = 0;
            VPIList = null;
            RSARList = null;
            RSAUList = null;
            ClientCallbackInstance = null;
            EstimatedContentSizeInBytes = 0;
        }
    }

    /// <summary>
    /// Custom CollectionDataContract object type used with IInterconnectPropagationSessionAPI Push method and related PushParameter parameter. 
    /// </summary>
    [CollectionDataContract(ItemName = "item", Namespace = Constants.ModularInterconnectNameSpace)]
    public class ValuePropagationItemList : List<ValuePropagationItem>
    {
        /// <summary>Default constructor gives empty list</summary>
        public ValuePropagationItemList() {}

        /// <summary>Copy constructor</summary>
        public ValuePropagationItemList(IEnumerable<ValuePropagationItem> v) : base(v) { }
    }

    /// <summary>
    /// Value Propagation Item for use as DataContract serializable item representation in a ValuePropagationItemList.
    /// Each item comes in one of three forms: 
    /// <para/>Registration Record (Server -> Client, has ID and Name)
    /// <para/>Add Name Request Record (Client -> Server, has Name but no ID)
    /// <para/>Normal Update Record (bidirectional, has ID but no Name)
    /// </summary>
    [DataContract(Namespace=Constants.ModularInterconnectNameSpace)]
    public class ValuePropagationItem
    {
        /// <summary>Carries the optional server side assigned ID that is used to effeciently refer to the named value after it has been registered and assigned an ID by the server.</summary>
        [DataMember(Order = 10, EmitDefaultValue = false, IsRequired = false)]
        public Int32 ID { get; set; }

        /// <summary>Carries the connection specific variant of the named value's name.  property is only used in Registration and Add Name Request Records.  It shall be set to null, and thus omitted, in all other cases.</summary>
        [DataMember(Order = 20, EmitDefaultValue = false, IsRequired = false)]
        public String Name { get; set; }

        /// <summary>Client usable property to get and set the value that is to be carried in this propagation item.  Actual serialization of same is done using a ValueContainerEnvelope</summary>
        public ValueContainer VC { get; set; }

        /// <summary>Serialization object for the VC.</summary>
        [DataMember(Name = "VC", Order = 30)]
        public ValueContainerEnvelope Env { get { return new ValueContainerEnvelope() { VC = VC }; } set { VC = value.VC; } }

        /// <summary>Helper method to restore item to its constructor default contents so that it may be recycled using a free list</summary>
        public void Clear()
        {
            ID = 0;
            Name = null;
            VC = ValueContainer.Empty;
        }

        /// <summary>Predicate returns true if this item is configured as a Registration Record</summary>
        public bool IsRegistrationRecord { get { return (ID != 0 && Name != null); } }

        /// <summary>Predicate returns true if this item is configured as an Add Name Request Record</summary>
        public bool IsAddNameRequestRecord { get { return (ID == 0 && Name != null); } }

        /// <summary>Predicate returns true if this item is configured as a Normal Update Record</summary>
        public bool IsNormalUpdateRecord { get { return (ID != 0 && Name == null); } }

        /// <summary>Returns the approximate size of the contents in bytes.</summary>
        public int EstimatedContentSizeInBytes
        {
            get
            {
                return 30 + Name.EstimatedContentSizeInBytes() + VC.EstimatedContentSizeInBytes;
            }
        }
    }

    /// <summary>
    /// Custom CollectionDataContract object type used with IInterconnectPropagationSessionAPI Push method and related PushParameter parameter. 
    /// </summary>
    [CollectionDataContract(ItemName = "item", Namespace = Constants.ModularInterconnectNameSpace)]
    public class RemoteServiceActionRequestList : List<RemoteServiceActionRequest>
    {
        /// <summary>Default constructor gives empty list</summary>
        public RemoteServiceActionRequestList() { }

        /// <summary>Copy constructor</summary>
        public RemoteServiceActionRequestList(IEnumerable<RemoteServiceActionRequest> v) : base(v) { }
    }

    /// <summary>
    /// This class is used as part of the PushParameters to allow one end to request the other end to create and start a context sensative IServiceAction using the given
    /// information and to reply with periodic status updates using the ActionUUID to report the progess of the action through the reverse connetion.
    /// This class can also be used to request that a previously started action should be asked to RequestCancel.
    /// </summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public class RemoteServiceActionRequest
    {
        /// <summary>Carries a unique identifier created for each newly started RemoteServiceAction</summary>
        [DataMember(Order = 10)]
        public string ActionUUID { get; set; }

        /// <summary>Gives the ServiceName that was passed in with the IServiceAction</summary>
        [DataMember(Order = 20, EmitDefaultValue = false, IsRequired = false)]
        public string ServiceName { get; set; }

        /// <summary>Carries the, potentialy null or empty, set of NamedValue prameters that were provided with the IServiceAction when it was last Started.</summary>
        [DataMember(Order = 30, EmitDefaultValue = false, IsRequired = false)]
        public NamedValueSet NamedParamValues { get; set; }

        /// <summary>When true this indicates that the sender would like the receiver to request a cancel on the identified action.</summary>
        [DataMember(Order = 40, EmitDefaultValue = false, IsRequired = false)]
        public bool RequestCancel { get; set; }

        /// <summary>Helper method to restore item to its constructor default contents so that it may be recycled using a free list</summary>
        public void Clear()
        {
            ActionUUID = null;
            ServiceName = null;
            NamedParamValues = null;
            RequestCancel = false;
        }

        /// <summary>Returns the approximate size of the contents in bytes.</summary>
        public int EstimatedContentSizeInBytes
        {
            get
            {
                return 30 + ActionUUID.EstimatedContentSizeInBytes() + ServiceName.EstimatedContentSizeInBytes() + NamedParamValues.MapNullToEmpty().EstimatedContentSizeInBytes + (RequestCancel ? 20 : 0);
            }
        }
    }

    /// <summary>
    /// Custom CollectionDataContract object type used with IInterconnectPropagationSessionAPI Push method and related PushParameter parameter. 
    /// </summary>
    [CollectionDataContract(ItemName = "item", Namespace = Constants.ModularInterconnectNameSpace)]
    public class RemoteServiceActionUpdateList : List<RemoteServiceActionUpdate>
    {
        /// <summary>Default constructor gives empty list</summary>
        public RemoteServiceActionUpdateList() { }

        /// <summary>Copy constructor</summary>
        public RemoteServiceActionUpdateList(IEnumerable<RemoteServiceActionUpdate> v) : base(v) { }
    }

    /// <summary>
    /// This class is used as part of the PushParameters to allow one end of inform the other about updates to the IActionState for a previously started RemoteServiceAction.
    /// </summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public class RemoteServiceActionUpdate
    {
        /// <summary>Carries a unique identifier that indicates which RemoteServiceAction this state update applies to.</summary>
        [DataMember(Order = 10)]
        public string ActionUUID { get; set; }

        /// <summary>Carries the most recently seen IActionState for this action.  Once the action IsComplete then no further updates will be propagated.</summary>
        [DataMember(Order = 20)]
        public ActionStateCopy ActionState { get; set; }

        /// <summary>Helper method to restore item to its constructor default contents so that it may be recycled using a free list</summary>
        public void Clear()
        {
            ActionUUID = null;
            ActionState = null;
        }

        /// <summary>Returns the approximate size of the contents in bytes.</summary>
        public int EstimatedContentSizeInBytes
        {
            get
            {
                return (30 + ActionUUID.EstimatedContentSizeInBytes() + ActionState.EstimatedContentSizeInBytes);
            }
        }
    }

    #endregion

    #endregion

    #region Server (ServerServiceConfig and ServerServicePart)

    /// <summary>
    /// This class is used to specify all of the per instance configurable details about a Part that is used as the Client end of this WCF Modular Interconnect Service
    /// </summary>
    public class ServerServiceConfig
    {
        /// <summary>
        /// Constructior.  Sets NominalScanPeriod to 0.10 sec, ReconnectHoldoffPeriod to 3.0 seconds, and RemoteNameMatchRuleSet to MatchRuleSet.Any
        /// </summary>
        public ServerServiceConfig()
        {
            NominalScanPeriod = TimeSpan.FromSeconds(0.10);

            SetupDefaultUris("Generic");
        }

        public ServerServiceConfig SetupDefaultUris(string dynamicPathPart)
        {
            ServiceEndPointUriArray = new Uri[] 
            {
                new Uri("net.tcp://localhost:13601/{0}.Modular.Interconnect.Wcf".CheckedFormat(dynamicPathPart)),
                new Uri("net.pipe://localhost/13601_NP/{0}.Modular.Interconnect.Wcf".CheckedFormat(dynamicPathPart)),
            };

            return this;
        }

        /// <summary>Gives the name of the ServerServicePart</summary>
        public String PartID { get; set; }

        /// <summary>This array gives the set of endpoints that this service will serve.  Corresponding binding supports net.tcp and net.pipe Uri Schemes.</summary>
        public Uri[] ServiceEndPointUriArray { get; set; }

        /// <summary>Gives the NominalScanPeriod that should be used for ConnectionScanHelpers created by the configured server.</summary>
        public TimeSpan NominalScanPeriod { get; set; }

        /// <summary>Clone equivilent method.</summary>
        public ServerServiceConfig MakeCopyOfThis()
        {
            return (ServerServiceConfig) MemberwiseClone();
        }

        public static readonly Uri[] EmptyUriArray = new Uri[0];
    }

    /// <summary>
    /// This Active Part is used to implement a server instance that supports the IInterconnectPropagationSessionClientAPI.  
    /// Typicaly this part creates a callback singleton that is used by all of the duplex channel communication objects to route client requests into asynch (thread safe)
    /// methods in the part.  These calls then either queue or use internal actions to handle the requests using the part's main thread.  The part supports
    /// multiple concurrent client sessions and supports allowing different clients to access different IVI tables as well as different sub-sets of items in any given table.
    /// </summary>
    /// <remarks>
    /// At present the current class definition is a placeholder until the coding of the ClientServicePart and the ConnetionScanHelper is done.
    /// </remarks>
    public class ServerServicePart : SimpleActivePartBase
    {
        #region Construction

        public ServerServicePart(ServerServiceConfig config) 
            : base(config.PartID) 
        {
            Config = config.MakeCopyOfThis();
            WaitTimeLimit = TimeSpan.FromSeconds(0.01);

            serverMethodHandler = new InterconnectPropagationSessionClientAPIHandler() 
            { 
                StartSessionHandler = AsyncHandleStartSession, 
                PushParameterHandler = AsyncConsumePushParameter, 
                EndSessionHandler = AsynchHandleEndSession, 
            };
        }

        private ServerServiceConfig Config { get; set; }

        private InterconnectPropagationSessionClientAPIHandler serverMethodHandler;      // initizlied in the constructor

        #endregion

        #region SimpleActivePart methods

        protected override string PerformGoOnlineAction(Action.IProviderActionBase<bool, NullObj> action)
        {
            string ec = String.Empty;
            bool andInitialize = action.ParamValue;
            bool closeExistingConnection = (!BaseState.IsConnected || andInitialize);
            string actionAsStr = action.ToString(ToStringSelect.MesgAndDetail);

            SetBaseState(UseState.AttemptOnline, "Starting {0}".CheckedFormat(actionAsStr), true);

            if (ec.IsNullOrEmpty() && closeExistingConnection)
            {
                InnerCloseServiceHost();

                SetBaseState(ConnState.Disconnected, "During {0}".CheckedFormat(actionAsStr), true);
            }

            if (ec.IsNullOrEmpty() && !BaseState.IsConnected)
            {
                ec = AttemptToCreateAndOpenServiceHost();
            }

            if (ec.IsNullOrEmpty())
                SetBaseState(UseState.Online, ConnState.Connected, "{0} Completed".CheckedFormat(actionAsStr), true);
            else
                SetBaseState(UseState.AttemptOnlineFailed, ConnState.ConnectFailed, "{0} Failed: {1}".CheckedFormat(actionAsStr, ec), true);

            return ec ?? String.Empty;
        }

        protected override string PerformGoOfflineAction(Action.IProviderActionBase action)
        {
            string actionAsStr = action.ToString(ToStringSelect.MesgAndDetail);

            InnerCloseServiceHost();

            SetBaseState(UseState.Offline, ConnState.Disconnected, "{0} Completed".CheckedFormat(actionAsStr), true);

            return String.Empty;
        }

        protected override string PerformServiceAction(IProviderActionBase<string, NullObj> action)
        {
            string actionAsStr = action.ToString(ToStringSelect.MesgAndDetail);

            if (!BaseState.IsOnline)
            {
                return "{0} cannot be performed: Server is not Online [{1}]".CheckedFormat(actionAsStr, BaseState);
            }

            StringScanner ss = new StringScanner(action.ParamValue);

            string targetClientName = ss.ExtractToken();

            BuildWorkingArraysIfNeeded();

            foreach (SessionTracker tracker in sessionTrackerWorkingArray)
            {
                if (tracker.StartSessionParameter.ClientName == targetClientName)
                {
                    tracker.ConnectionScanHelper.SendStartRequestAndTrackAction(action, ss.Rest);
                    return null;        // we have just comsumed the action and will mark that it is complete later.
                }
            }

            return "Service Action '{0}' cannot be performed: No matching session client name was found".CheckedFormat(actionAsStr);
        }

        protected override void MainThreadFcn()
        {
            base.MainThreadFcn();

            InnerCloseServiceHost();

            ServicePendingCloseQueue(true);
        }

        protected override void PerformMainLoopService()
        {
            BuildWorkingArraysIfNeeded();

            foreach (SessionTracker tracker in sessionTrackerWorkingArray)
            {
                tracker.ConnectionScanHelper.Service();

                if (tracker.ConnectionScanHelper.IsAbortRequested)
                    RemoveSessionTracker(tracker.ClientCallbackProxy, "Scan Helper Requested Abort: {0}".CheckedFormat(tracker.ConnectionScanHelper.AbortReason));
            }

            if (asyncInboundPushParameterQueueCount > 0 || syncInboundPushParameterQueue.Count > 0)
            {
                // synchronously transfer all of the currently pending push parameters from the asynch queue to the sync queue (a temporary holding pen)
                lock (asyncInboundPushParameterQueueMutex)
                {
                    while (asyncInboundPushParameterQueue.Count > 0)
                    {
                        syncInboundPushParameterQueue.Enqueue(asyncInboundPushParameterQueue.Dequeue());
                    }

                    asyncInboundPushParameterQueueCount = asyncInboundPushParameterQueue.Count;
                }

                // next (outside of the mutex) distribute the push paramters to the correct connection scan helper instance
                while (syncInboundPushParameterQueue.Count > 0)
                {
                    PushParameter ppItem = syncInboundPushParameterQueue.Dequeue();
                    SessionTracker tracker = FindSessionTracker(ppItem.ClientCallbackInstance);
                    if (tracker != null)
                        tracker.ConnectionScanHelper.IncommingPushParametersQueue.Enqueue(ppItem);
                    else
                        Log.Debug.Emit("Dropped PushParameter item from unkown client");
                }
            }

            ServicePendingCloseQueue(false);
        }

        private Queue<PushParameter> syncInboundPushParameterQueue = new Queue<PushParameter>();

        #endregion

        #region ServiceHost and related methods

        private ServiceHost serviceHost = null;

        private string AttemptToCreateAndOpenServiceHost()
        {
            string ec = String.Empty;

            try
            {
                serviceHost = new ServiceHost(serverMethodHandler);

                foreach (Uri uri in (Config.ServiceEndPointUriArray ?? ServerServiceConfig.EmptyUriArray))
                {
                    Binding binding = null;

                    switch (uri.Scheme)
                    {
                        case "net.tcp":
                            binding = new NetTcpBinding(SecurityMode.None, false)
                            {
                                TransferMode = TransferMode.Buffered,
                                MaxReceivedMessageSize = 20000000,
                                MaxBufferSize = 20000000,
                                OpenTimeout = TimeSpan.FromSeconds(5.0),
                                SendTimeout = TimeSpan.FromSeconds(5.0),
                                ReceiveTimeout = TimeSpan.FromSeconds(300.0),
                                CloseTimeout = TimeSpan.FromSeconds(5.0),
                                MaxConnections = 50,
                            };
                            break;
                        case "net.pipe":
                            binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
                            {
                                TransferMode = TransferMode.Buffered,
                                MaxReceivedMessageSize = 20000000,
                                MaxBufferSize = 20000000,
                                OpenTimeout = TimeSpan.FromSeconds(5.0),
                                SendTimeout = TimeSpan.FromSeconds(5.0),
                                ReceiveTimeout = TimeSpan.FromSeconds(300.0),
                                CloseTimeout = TimeSpan.FromSeconds(5.0),
                                MaxConnections = 50,
                            };
                            break;
                        default: break;
                    }

                    if (binding != null)
                        serviceHost.AddServiceEndpoint(typeof(IInterconnectPropagationSessionClientAPI), binding, uri);
                }

                //serviceHost.Description.Behaviors.Add();

                serviceHost.Open();
            }
            catch (System.Exception ex)
            {
                ec = "Attempt to create ServiceHost failed: {0}".CheckedFormat(ex);
            }

            return ec;
        }

        private void InnerCloseServiceHost()
        {
            BuildWorkingArraysIfNeeded();

            foreach (SessionTracker tracker in sessionTrackerWorkingArray)
            {
                RemoveSessionTracker(tracker.ClientCallbackProxy, "Closing ServiceHost");
            }

            try
            {
                if (serviceHost != null)
                {
                    CommunicationState serviceHostCommState = serviceHost.State;
                    switch (serviceHostCommState)
                    {
                        case CommunicationState.Closed:
                        case CommunicationState.Faulted:
                            Log.Debug.Emit("ServiceHost Close not needed.  Host is already {0}", serviceHost.State);
                            break;
                        case CommunicationState.Created:
                        case CommunicationState.Opening:
                        case CommunicationState.Opened:
                        case CommunicationState.Closing:
                        default:
                            serviceHost.Close();

                            Log.Debug.Emit("ServiceHost has been closed");
                            break;
                    }
                }

                Fcns.DisposeOfGivenObject(serviceHost);
            }
            catch (System.Exception ex)
            {
                Log.Debug.Emit("ServiceHost close failed unexpectedly: {0}", ex.ToString(ExceptionFormat.Full));
            }

            serviceHost = null;
        }

        #endregion

        #region Per client information

        private Dictionary<IInterconnectPropagationSessionCommonAPI, SessionTracker> commonCallbackProxyToTrackerDictionary = new Dictionary<IInterconnectPropagationSessionCommonAPI, SessionTracker>();

        private class SessionTracker
        {
            public StartSessionParameter StartSessionParameter { get; set; }
            public IInterconnectPropagationSessionCommonAPI ClientCallbackProxy { get; set; }
            public ICommunicationObject CommunicationObject { get; set; }
            public ConnectionScanHelper ConnectionScanHelper { get; set; }

            public void Clear()
            {
                StartSessionParameter = null;
                ClientCallbackProxy = null;
                CommunicationObject = null;
                ConnectionScanHelper = null;
            }
        }

        private SessionTracker[] sessionTrackerWorkingArray = null;

        private void ResetWorkingArrays()
        {
            sessionTrackerWorkingArray = null;
        }

        private void BuildWorkingArraysIfNeeded()
        {
            if (sessionTrackerWorkingArray == null)
            {
                sessionTrackerWorkingArray = commonCallbackProxyToTrackerDictionary.Values.ToArray();
            }
        }

        private void AddSessionTracker(SessionTracker tracker)
        {
            commonCallbackProxyToTrackerDictionary[tracker.ClientCallbackProxy] = tracker;
            ResetWorkingArrays();
        }

        private SessionTracker FindSessionTracker(IInterconnectPropagationSessionCommonAPI commonCallbackProxy)
        {
            SessionTracker tracker = null;

            commonCallbackProxyToTrackerDictionary.TryGetValue(commonCallbackProxy, out tracker);

            return tracker;
        }

        private void RemoveSessionTracker(IInterconnectPropagationSessionCommonAPI commonCallbackProxy, string reason)
        {
            SessionTracker tracker = FindSessionTracker(commonCallbackProxy);

            if (tracker != null)
            {
                commonCallbackProxyToTrackerDictionary.Remove(commonCallbackProxy);
                ResetWorkingArrays();

                // based on the connection state, attempt to start closing the connection.
                if (tracker.CommunicationObject != null)
                {
                    CommunicationState trackerCommState = tracker.CommunicationObject.State;
                    switch (trackerCommState)
                    {
                        case CommunicationState.Closed:
                        case CommunicationState.Closing:
                        case CommunicationState.Faulted:
                            break;      // there is nothing to do in these states.  The connection is already effectively closed.
                        case CommunicationState.Created:
                        case CommunicationState.Opening:
                        case CommunicationState.Opened:
                        default:
                            try
                            {
                                pendingCloseQueue.Enqueue(tracker.CommunicationObject);
                                tracker.CommunicationObject.BeginClose((iar) => { this.Notify(); }, this);
                            }
                            catch (System.Exception ex)
                            {
                                Log.Debug.Emit("Start connection close for client '{0}' failed: {1}", tracker.StartSessionParameter.ClientName, ex);
                            }
                            break;
                    }
                }
                else
                {
                    Log.Debug.Emit("Unable to close connection for client '{0}': tracker's connection object is null", tracker.StartSessionParameter.ClientName);
                }

                Log.Debug.Emit("Removed Session: {0}, reason: '{1}'".CheckedFormat(tracker.StartSessionParameter.ClientName, reason));

                sessionTrackerFreeList.Release(ref tracker);
            }
            else
            {
                Log.Debug.Emit("Could not remove indicated connection: no seession tracker not found in dictionary");
            }
        }

        private Queue<ICommunicationObject> pendingCloseQueue = new Queue<ICommunicationObject>();

        private void ServicePendingCloseQueue(bool waitUntilAllDone)
        {
            for (; ; )
            {
                if (pendingCloseQueue.Count <= 0)
                    return;

                ICommunicationObject commObj = pendingCloseQueue.Peek();

                if (commObj == null)
                {
                    pendingCloseQueue.Dequeue();
                    continue;
                }

                switch (commObj.State)
                {
                    case CommunicationState.Closed:
                    case CommunicationState.Faulted:
                        // this connection has closed - remove it from the queue.
                        pendingCloseQueue.Dequeue();
                        Log.Debug.Emit("Client comm object has finished closing");
                        continue;

                    case CommunicationState.Closing:
                        // this connection is still closing.... - keep waiting
                        break;

                    default:
                        // this connection is in an unexpected state - try to start closing it again
                        Log.Debug.Emit("Unexpected CommunicationObjectState:{0} for object in pendingCloseQueue", commObj.State);
                        try
                        {
                            // try to start closing it (again).
                            commObj.BeginClose((iar) => { this.Notify(); }, this);

                            // move it to the back of the queue
                            pendingCloseQueue.Enqueue(pendingCloseQueue.Dequeue());
                        }
                        catch (System.Exception ex)
                        {
                            Log.Debug.Emit("Attempt to restart Close for pending close connection failed:{0} [dropping from queue]", ex);
                            pendingCloseQueue.Dequeue();
                        }
                        break;
                }

                if (!waitUntilAllDone)
                    return;

                WaitForSomethingToDo();
            }
        }

        #endregion

        #region Custom internal implementations for CallbackHandler instance and delegates

        private void AsyncHandleStartSession(StartSessionParameter startSessionParameter)
        {
            IBasicAction action = new BasicActionImpl(actionQ, () => PerformStartSession(startSessionParameter), "Start Session From '{0}'".CheckedFormat(startSessionParameter.ClientName), ActionLoggingReference);
            string ec = action.Run();

            if (!ec.IsNullOrEmpty())
                throw new System.InvalidOperationException("{0} failed: {1}".CheckedFormat(action, ec));
        }

        private string PerformStartSession(StartSessionParameter startSessionParameter)
        {
            // verify that there is not already a session for this connection
            SessionTracker priorTracker = FindSessionTracker(startSessionParameter.ClientCallbackInstance);

            if (priorTracker != null)
            {
                string mesg = "StartSession can only be used once per connection: Prior session exists with this connection from client '{0}'".CheckedFormat(priorTracker.StartSessionParameter.ClientName);
                Log.Debug.Emit(mesg);
                return mesg;
            }

            // get and setup a tracker for this connection
            SessionTracker tracker = sessionTrackerFreeList.Get();

            tracker.StartSessionParameter = startSessionParameter;
            tracker.ClientCallbackProxy = startSessionParameter.ClientCallbackInstance;
            tracker.CommunicationObject = startSessionParameter.ClientCallbackInstance as ICommunicationObject;
            tracker.ConnectionScanHelper = new ConnectionScanHelper(PartID) 
            {
                Logger = Log,
                IsServer = true,
                CommonAPIConnectionProxy = tracker.ClientCallbackProxy,
                ICommunicationObject = tracker.CommunicationObject,
                IVI = Values.Values.GetTable(startSessionParameter.InterconnectTableName, true),
                AddAndRemoveLocalPrefix = String.Empty,
                LocalNameMatchRuleSet = startSessionParameter.NameMatchRuleSet,
                NominalOutgoingIVIScanInterval = Config.NominalScanPeriod,
            };

            // add the tracker to the dictionary
            AddSessionTracker(tracker);

            return String.Empty;
        }

        private void AsyncConsumePushParameter(PushParameter pushParameter)
        {
            lock (asyncInboundPushParameterQueueMutex)
            {
                asyncInboundPushParameterQueue.Enqueue(pushParameter);
                asyncInboundPushParameterQueueCount = asyncInboundPushParameterQueue.Count;
            }
        }

        private object asyncInboundPushParameterQueueMutex = new object();
        private Queue<PushParameter> asyncInboundPushParameterQueue = new Queue<PushParameter>();
        private volatile int asyncInboundPushParameterQueueCount = 0;

        void AsynchHandleEndSession(IInterconnectPropagationSessionCommonAPI clientCallbackProxy)
        {
            IBasicAction action = new BasicActionImpl(actionQ, () => PerformEndSession(clientCallbackProxy), "End Session", ActionLoggingReference);
            string ec = action.Run();

            if (!ec.IsNullOrEmpty())
                throw new System.InvalidOperationException("{0} failed: {1}".CheckedFormat(action, ec));
        }

        private string PerformEndSession(IInterconnectPropagationSessionCommonAPI clientCallbackProxy)
        {
            RemoveSessionTracker(clientCallbackProxy, "End Session requested");

            return String.Empty;
        }

        #endregion

        #region Free List(s)

        private BasicFreeList<SessionTracker> sessionTrackerFreeList = new BasicFreeList<SessionTracker>() { MaxItemsToKeep = 10, FactoryDelegate = () => new SessionTracker(), ClearDelegate = (item) => item.Clear() };

        #endregion

        #region service session API handler class

        [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = false)]
        class InterconnectPropagationSessionClientAPIHandler
            : DisposableBase
            , IInterconnectPropagationSessionClientAPI
        {
            public Action<StartSessionParameter> StartSessionHandler { get; set; }
            public Action<PushParameter> PushParameterHandler { get; set; }
            public Action<IInterconnectPropagationSessionCommonAPI> EndSessionHandler { get; set; }

            public void StartSession(StartSessionParameter param)
            {
                // record the operation context of the callback to the client so that the service can send it messages.
                IInterconnectPropagationSessionCommonAPI sessionClientCallback = OperationContext.Current.GetCallbackChannel<IInterconnectPropagationSessionCommonAPI>();

                param.ClientCallbackInstance = sessionClientCallback;
                StartSessionHandler(param);
            }

            public void EndSession()
            {
                IInterconnectPropagationSessionCommonAPI sessionClientCallback = OperationContext.Current.GetCallbackChannel<IInterconnectPropagationSessionCommonAPI>();

                EndSessionHandler(sessionClientCallback);
            }

            public void Push(PushParameter param)
            {
                IInterconnectPropagationSessionCommonAPI sessionClientCallback = OperationContext.Current.GetCallbackChannel<IInterconnectPropagationSessionCommonAPI>();

                param.ClientCallbackInstance = sessionClientCallback;
                PushParameterHandler(param);
            }
        }

        #endregion
    }

    #endregion

    #region Client (ClientServiceConfig and ClientServicePart)

    /// <summary>
    /// This class is used to specify all of the per instance configurable details about a Part that is used as the Client end of this WCF Modular Interconnect Service
    /// </summary>
    public class ClientServiceConfig
    {
        /// <summary>
        /// Constructior.  Sets NominalScanPeriod to 0.10 sec, ReconnectHoldoffPeriod to 3.0 seconds, and RemoteNameMatchRuleSet to MatchRuleSet.Any
        /// </summary>
        public ClientServiceConfig()
        {
            NominalScanPeriod = TimeSpan.FromSeconds(0.10);
            ReconnectHoldoffPeriod = TimeSpan.FromSeconds(3.0);
            RemoteNameMatchRuleSet = MatchRuleSet.Any;

            ClientConnectsToEndpointUri = new Uri("net.tcp://localhost:13601/Generic.Modular.Interconnect.Wcf");
            //ClientConnectsToEndpointUri = new Uri("net.pipe://localhost/13601_NP/Generic.Modular.Interconnect.Wcf");
        }

        public String PartID { get; set; }

        public Uri ClientConnectsToEndpointUri { get; set; } 
        //public String ClientEndpointConfigurationName { get; set; }

        public String LocalValueTableName { get; set; }
        public String RemoteValueTableName { get; set; }
        public IValuesInterconnection LocalIVI { get; set; }

        /// <summary>Initilizes both LocalValueTableName and RemoteValueTableName to the given value.</summary>
        public String ValueTableName { set { LocalValueTableName = RemoteValueTableName = value; } }

        public TimeSpan NominalScanPeriod { get; set; }
        public TimeSpan ReconnectHoldoffPeriod { get; set; }

        public string AddRemoveLocalNamePrefix { get; set; }
        public MatchRuleSet RemoteNameMatchRuleSet { get; set; }

        /// <summary>Clone equivilent method.</summary>
        public ClientServiceConfig MakeCopyOfThis()
        {
            return (ClientServiceConfig)MemberwiseClone();
        }
    }

    /// <summary>
    /// This is the Part that is used to implement the Client end of a Interconnect WCF Service.
    /// </summary>
    public class ClientServicePart : SimpleActivePartBase
    {
        /// <summary>
        /// Constructor.  Caller must provide a ClientServiceConfig that will be cloned by this part to record its operational settings.
        /// Then it determines the IVI that it will be linked to, from the given LocalIVI or from the LocalValueTableName if the local IVI was not given.
        /// </summary>
        public ClientServicePart(ClientServiceConfig config)
            : base(config.PartID) 
        {
            Config = config.MakeCopyOfThis();

            IVI = Config.LocalIVI ?? Interconnect.Values.Values.GetTable(Config.LocalValueTableName, true);

            WaitTimeLimit = TimeSpan.FromSeconds(0.01);     // max spin rate is 100 Hz

            clientCallbackHandler = new InterconnectPropagationSessionClientCallbackHandler() { PushParameterHandler = AsynchConsumePushParameter };
        }

        private ClientServiceConfig Config { get; set; }

        private IValuesInterconnection IVI { get; set; }

        #region SimpleActivePartBase overrides

        private IProviderFacet pendingGoOnlineAction = null;

        private bool HavePendingGoOnlineAction { get { return (pendingGoOnlineAction != null); } }

        protected override string PerformGoOnlineAction(Action.IProviderActionBase<bool, NullObj> action)
        {
            string ec = String.Empty;
            bool andInitialize = action.ParamValue;
            bool closeExistingConnection = (!BaseState.IsConnected || andInitialize);
            string actionAsStr = action.ToString(ToStringSelect.MesgAndDetail);

            SetBaseState(UseState.AttemptOnline, "Starting {0}".CheckedFormat(actionAsStr), true);

            if (ec.IsNullOrEmpty() && closeExistingConnection)
            {
                ec = InnerCloseCurrentConnection();

                if (ec.IsNullOrEmpty())
                    SetBaseState(ConnState.Disconnected, "During {0}".CheckedFormat(actionAsStr), true);
                else
                    SetBaseState(ConnState.Disconnected, "During {0}, ec:{1}".CheckedFormat(actionAsStr, ec), true);
            }

            if (ec.IsNullOrEmpty() && !BaseState.IsConnected)
            {
                ec = InnerAttemptToOpenConnection();
            }

            if (ec.IsNullOrEmpty())
            {
                if (andInitialize)
                {
                    pendingGoOnlineAction = action;
                    SetBaseState(UseState.Online, ConnState.Connecting, "{0} Waiting for registration complete".CheckedFormat(actionAsStr), true);
                    return null;    // this action is still in progress - will be comleted later.
                }
                else
                {
                    SetBaseState(UseState.Online, ConnState.Connected, "{0} Completed".CheckedFormat(actionAsStr), true);
                    return String.Empty;
                }
            }
            else
            {
                SetBaseState(UseState.OnlineFailure, ConnState.ConnectFailed, "{0} Failed: {1}".CheckedFormat(actionAsStr, ec), true);
                return ec;
            }
        }

        private void ServicePendingGoOnlineAction()
        {
            if (pendingGoOnlineAction != null)
            {
                string actionAsStr = pendingGoOnlineAction.ToString(ToStringSelect.MesgAndDetail);

                if (pendingGoOnlineAction.IsCancelRequestActive)
                {
                    pendingGoOnlineAction.CompleteRequest("Canceled by explicit request");
                }
                else if (BaseState.IsConnected)
                {
                    Log.Debug.Emit("Pending {0} action comleted: BaseState unexpectedly transitioned to {1}", actionAsStr, BaseState);
                    pendingGoOnlineAction.CompleteRequest(String.Empty);
                }
                else if (!BaseState.IsConnecting || (connectionScanHelper == null))
                {
                    pendingGoOnlineAction.CompleteRequest("Failed: BaseState unexpectedly transitioned to {0}".CheckedFormat(BaseState));
                }
                else if (connectionScanHelper.IsStateInRegistrationPhase)
                {
                    pendingGoOnlineAction.CompleteRequest(String.Empty);
                    SetBaseState(ConnState.Connected, "Pending {0} action comleted normally".CheckedFormat(actionAsStr), true);
                }

                if (pendingGoOnlineAction.ActionState.IsComplete)
                    pendingGoOnlineAction = null;
            }

            if (pendingGoOnlineAction == null && BaseState.IsConnecting && base.CurrentAction == null)
            {
                SetBaseState(ConnState.Connected, "Pending GoOnline and Initialize completed abnormally", true);
            }
        }

        protected override string PerformGoOfflineAction(Action.IProviderActionBase action)
        {
            string actionAsStr = action.ToString(ToStringSelect.MesgAndDetail);

            InnerCloseConnectionAndGoOffline(actionAsStr, String.Empty);

            return String.Empty;
        }

        protected override string PerformServiceAction(IProviderActionBase<string, NullObj> action)
        {
            StringScanner ss = new StringScanner(action.ParamValue);
            string actionAsStr = action.ToString(ToStringSelect.MesgAndDetail);

            if (ss.MatchToken("Remote"))
            {
                if (BaseState.IsConnectedOrConnecting)
                {
                    connectionScanHelper.SendStartRequestAndTrackAction(action, ss.Rest);
                    return null;    // we have consumed the action and will signal its completion later.
                }
                else
                {
                    return "Service Action '{0}' cannot be performed: Client is not connected to remote end.".CheckedFormat(actionAsStr);
                }
            }

            return base.PerformServiceAction(action);
        }

        protected override void MainThreadFcn()
        {
            base.MainThreadFcn();

            InnerCloseConnectionAndGoOffline("At MainThreadFcn.End", null);
        }

        protected override void PerformMainLoopService()
        {
            // service the connection - handle Fault and 
            if (clientConnectionObject != null)
            {
                CommunicationState clientConnState = clientConnectionObject.State;

                if (clientConnState == CommunicationState.Closed)
                    SetBaseState(ConnState.DisconnectedByOtherEnd, "Connection transitioned to {0} state unexpectedly".CheckedFormat(clientConnState), true);
                else if (clientConnState == CommunicationState.Faulted)
                    SetBaseState(ConnState.ConnectionFailed, "Connection transitioned to {0} state unexpectedly".CheckedFormat(clientConnState), true);

                if (!BaseState.IsConnectedOrConnecting)
                    InnerCloseCurrentConnection();
            }

            if (clientConnectionObject == null && BaseState.IsOnline && BaseState.TimeStamp.Age >= Config.ReconnectHoldoffPeriod)
            {
                SetBaseState(ConnState.Connecting, "Attempting to start auto-reconnect after {0:f1} seconds".CheckedFormat(BaseState.TimeStamp.Age.TotalSeconds), true);

                string ec = InnerAttemptToOpenConnection();

                if (ec.IsNullOrEmpty())
                    SetBaseState(UseState.Online, ConnState.Connected, "Auto-reconnect succeeded", true);
                else
                    SetBaseState(UseState.OnlineFailure, ConnState.ConnectFailed, "Auto-reconnect failed: {0}".CheckedFormat(ec), true);
            }

            if (BaseState.IsConnectedOrConnecting && clientConnectionProxy != null)
            {
                InnerServiceActiveSession();
            }

            ServicePendingGoOnlineAction();
        }

        #endregion

        #region Inner Connection open and close methods, Session tracking logic

        private IInterconnectPropagationSessionClientAPI clientConnectionProxy = null;
        private ICommunicationObject clientConnectionObject = null;

        private ConnectionScanHelper connectionScanHelper = null;

        private string InnerAttemptToOpenConnection()
        {
            string ec = String.Empty;

            // attempt to create and start client connection here
            try
            {
                Binding binding = null;
                switch (Config.ClientConnectsToEndpointUri.Scheme)
                {
                    case "net.tcp":
                        binding = new NetTcpBinding(SecurityMode.None, false)
                        {
                            TransferMode = TransferMode.Buffered,
                            MaxReceivedMessageSize = 20000000,
                            MaxBufferSize = 20000000,
                            OpenTimeout = TimeSpan.FromSeconds(5.0),
                            SendTimeout = TimeSpan.FromSeconds(5.0),
                            ReceiveTimeout = TimeSpan.FromSeconds(300.0),
                            CloseTimeout = TimeSpan.FromSeconds(5.0),
                        };
                        break;
                    case "net.pipe":
                        binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None)
                        {
                            TransferMode = TransferMode.Buffered,
                            MaxReceivedMessageSize = 20000000,
                            MaxBufferSize = 20000000,
                            OpenTimeout = TimeSpan.FromSeconds(5.0),
                            SendTimeout = TimeSpan.FromSeconds(5.0),
                            ReceiveTimeout = TimeSpan.FromSeconds(300.0),
                            CloseTimeout = TimeSpan.FromSeconds(5.0),
                        };
                        break;
                    default: break;
                }

                clientConnectionProxy = System.ServiceModel.DuplexChannelFactory<IInterconnectPropagationSessionClientAPI>.CreateChannel(clientCallbackHandler, binding, new EndpointAddress(Config.ClientConnectsToEndpointUri));

                clientConnectionObject = clientConnectionProxy as ICommunicationObject;

                connectionScanHelper = new ConnectionScanHelper(PartID) 
                { 
                    Logger = Log,
                    IsClient = true, 
                    IVI = IVI,  // use the IVI that we already captured and decoded from Config during object construction.
                    AddAndRemoveLocalPrefix = Config.AddRemoveLocalNamePrefix, 
                    LocalNameMatchRuleSet = Config.RemoteNameMatchRuleSet, 
                    CommonAPIConnectionProxy = clientConnectionProxy,
                    ICommunicationObject = clientConnectionObject,
                    NominalOutgoingIVIScanInterval = Config.NominalScanPeriod,
                };

                clientConnectionProxy.StartSession(new StartSessionParameter() { ClientName = PartID, InterconnectTableName = Config.RemoteValueTableName, NameMatchRuleSet = Config.RemoteNameMatchRuleSet });
            }
            catch (System.Exception ex)
            {
                ec = "Connection attempt did not succeed: {0}".CheckedFormat(ex);
            }

            return ec;
        }

        private void InnerCloseConnectionAndGoOffline(string actionDescription, string connectionAbortReason)
        {
            bool wasOnline = (BaseState.IsOnline);

            string ec = InnerCloseCurrentConnection();

            bool haveAbortReason = !connectionAbortReason.IsNullOrEmpty();
            bool closeFailed = !ec.IsNullOrEmpty();

            if (!haveAbortReason && !closeFailed)
                SetBaseState(UseState.Offline, ConnState.Disconnected, "{0} Completed".CheckedFormat(actionDescription), true);
            else if (!haveAbortReason)
                SetBaseState(UseState.Offline, ConnState.Disconnected, "{0} Failed: {1}".CheckedFormat(actionDescription, ec), true);
            else
                SetBaseState(wasOnline ? UseState.OnlineFailure : UseState.FailedToOffline, ConnState.ConnectionFailed, "Aborted: {0}".CheckedFormat(connectionAbortReason), true);
        }

        /// <summary>
        /// Attempts to close the current connection, if any.  Has no effect if there is no current connection.
        /// Returns empty string on success or non-empty string if any issue was encountered while closing the current connection.
        /// </summary>
        private string InnerCloseCurrentConnection()
        {
            string ec = String.Empty;

            try
            {
                // attempt to inform the server that we are closing the connection

                if (clientConnectionProxy != null)
                    clientConnectionProxy.EndSession();
            }
            catch { }

            // dispose of the connection to close it
            try
            {
                CommunicationState clientConnState = (clientConnectionObject != null ? clientConnectionObject.State : CommunicationState.Closed);
                if (clientConnState != CommunicationState.Closed && clientConnState != CommunicationState.Faulted)
                    clientConnectionObject.Close();

                Fcns.DisposeOfObject(ref connectionScanHelper);
                Fcns.DisposeOfObject(ref clientConnectionObject);

                // do not attempt to dispose of the proxy as it is actually the same object as the connection is.
            }
            catch (System.Exception ex)
            {
                ec = "Client Connection Proxy Dispose failed: {0}".CheckedFormat(ex);
            }

            // discard all representations of the connection that was just closed including all session state information about its use (in the connectionScanHelper).
            clientConnectionProxy = null;
            clientConnectionObject = null;
            connectionScanHelper = null;

            return ec;
        }

        private void InnerServiceActiveSession()
        {
            if (connectionScanHelper != null)
            {
                if (inboundPushParameterQueueCount > 0)
                {
                    lock (inboundPushParameterQueueMutex)
                    {
                        while (inboundPushParameterQueue.Count > 0)
                            connectionScanHelper.IncommingPushParametersQueue.Enqueue(inboundPushParameterQueue.Dequeue());

                        inboundPushParameterQueueCount = inboundPushParameterQueue.Count;
                    }
                }

                connectionScanHelper.Service();

                if (connectionScanHelper.IsAbortRequested)
                {
                    InnerCloseConnectionAndGoOffline(String.Empty, connectionScanHelper.AbortReason);
                }
            }
        }

        #endregion

        #region CallbackHandler instance and delegates

        private InterconnectPropagationSessionClientCallbackHandler clientCallbackHandler;      // initizlied in the constructor

        private void AsynchConsumePushParameter(PushParameter pushParameter)
        {
            lock (inboundPushParameterQueueMutex)
            {
                inboundPushParameterQueue.Enqueue(pushParameter);
                inboundPushParameterQueueCount = inboundPushParameterQueue.Count;
            }
        }

        private object inboundPushParameterQueueMutex = new object();
        private volatile int inboundPushParameterQueueCount = 0;
        private Queue<PushParameter> inboundPushParameterQueue = new Queue<PushParameter>();

        #endregion

        #region client callback service handler class

        [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Single, AutomaticSessionShutdown = true, UseSynchronizationContext = false)]
        private class InterconnectPropagationSessionClientCallbackHandler
            : IInterconnectPropagationSessionCommonAPI
        {
            public Action<PushParameter> PushParameterHandler { get; set; }

            public void Push(PushParameter param)
            {
                PushParameterHandler(param);
            }
        }

        #endregion
    }

    #endregion

    #region ConnectionScanHelperEngine

    /// <summary>
    /// This helper class is used to provide common code handling for most of the registration and value delivery parts of the IInterconnectPropagationSessionCommonAPI.
    /// Instances of this class are used by both the ClientServicePart and the ServerServicePart to encapsulate the common protocol code (and parts of the role specific code).
    /// This allows both the Client and the Server to make use of the same code to handle the normal scanning of the active set of value accessors and to generate and handle
    /// normal push calls through the protocol using common code.  Some role specific parts are included here as they pertain to the setup of a given session (registration 
    /// and related periods) as well as the handling of ongoing table additions that take place after the initial connection setup is complete.
    /// </summary>
    /// <remarks>
    /// The coding of this class is intended to include 0th order optimizations to avoid generating garbage more garbage allocations than is strictly required to support the protocol
    /// and related functionality.  
    /// </remarks>
    internal class ConnectionScanHelper : DisposableBase
    {
        #region default constrution and external setup properties

        /// <summary>
        /// Default constructor: 
        /// sets LocalNameMatchRuleSet to MatchRuleSet.Any, AckWaitTimeLimit to 30 seconds,
        /// MaxPendingPushCount to 100, MaxPostedSimpleValueItemsPerPush to 1000, MaxPostedComplexValueItemsPerPush to 100,
        /// and SendPingAfterIdleTimePeriodHasElpased to 10.0 seconds.
        /// </summary>
        public ConnectionScanHelper(string clientName)
        {
            LocalNameMatchRuleSet = MatchRuleSet.Any;
            AckWaitTimeLimit = TimeSpan.FromSeconds(30.0);

            MaxPendingPushCount = 100;
            MaxPendingEstimatedPushedByteCount = 10000000;
            NominalMaxEstimatedByteCountPerPushItem = 250000;

            SendPingAfterIdleTimePeriodHasElpased = TimeSpan.FromSeconds(10.0);    // send an auto ping when the connection has been idle for 10 seconds.

            Logger = new Logging.Logger("{0}.ScanHelper".CheckedFormat(clientName), Logging.LookupDistributionGroupName);
            Trace = new Logging.Logger("{0}.ScanHelper.Trace".CheckedFormat(clientName), Logging.LookupDistributionGroupName);

            NominalOutgoingIVIScanInterval = TimeSpan.FromSeconds(0.10);    // 10 Hz

            AddExplicitDisposeAction(() => Release());
        }

        /// <summary>
        /// The parrent part as an INotifyable so that action notify callbacks can be used to trigger the parent part to wake up.
        /// </summary>
        public INotifyable NotifyableParentPart { get; set; }

        /// <summary>Logger used for messages produced here</summary>
        public Logging.IBasicLogger Logger { get; set; }

        /// <summary>Trace Logger used for trace messages produced here</summary>
        public Logging.IBasicLogger Trace { get; private set; }

        /// <summary>Gives the IValuesInterconnection instance from which this connection endpoint instance will get or create ValueAccessor objects.</summary>
        public IValuesInterconnection IVI { get; set; }

        /// <summary>Gives the set of rules that are used to determine which names in the given IVI are expected to be included in the connection's value propagation traffic.</summary>
        public MatchRuleSet LocalNameMatchRuleSet { get; set; }

        /// <summary>
        /// Gives a prefix string that is removed from value names when generating the corresponding connection name and which is added to connection names
        /// when converting them to a value name for lookup/creation in the IVI.  This is generally used to allow the client to inject the connected names into local table as a form of
        /// sub-key.  Note that when this property is non-empty it acts as a second form match constraint in that it will only recognize IVI value names if they start with the given
        /// prefix, in addition to whatever rules are included in the LocalNameMatchRuleSet.
        /// </summary>
        public string AddAndRemoveLocalPrefix { get; set; }

        /// <summary>This is the API connection proxy object that the engine uses to post Push calls on.</summary>
        public IInterconnectPropagationSessionCommonAPI CommonAPIConnectionProxy { get; set; }

        /// <summary>This is the API connection proxy object casted as its underlying communication object.</summary>
        public ICommunicationObject ICommunicationObject { get; set; }

        /// <summary>
        /// This enumeration defines the different values for the concept of the role that a connection helper can be use with.
        /// <para/>values: Client, Server
        /// </summary>
        public enum ConnectionRole : int
        {
            /// <summary>The connection helper is to be used in the role of a Client where it waits for traffic from the server to complete normal registration and then begins operating in full connected state.</summary>
            Client,
            /// <summary>The connection helper is to be used in the role of a Server where it starts by forwarding registration records for all matching names in the table and then transitions to normal operation in the connected state.</summary>
            Server,
        }

        /// <summary>This get/set property defines the Role that this connection helper will be used for.</summary>
        public ConnectionRole Role
        {
            get { return role; }

            set
            {
                role = value;
                OtherEndRole = IsClient ? ConnectionRole.Server : ConnectionRole.Client;
            }
        }
        /// <summary>backing store for the Role property</summary>
        private ConnectionRole role;

        /// <summary>This property defines the name of the Role for the other end of the WCF connection.  This is used in log messages and abort reasons to indicate the name of the offending party.</summary>
        private ConnectionRole OtherEndRole { get; set; }

        /// <summary>Get/Set property.  Getter returns true if the current objects Role is as a Server.  Setter sets the Role based to match the given boolean value as true => Server and false => Client</summary>
        public bool IsServer { get { return (Role == ConnectionRole.Server); } set { Role = (value) ? ConnectionRole.Server : ConnectionRole.Client; } }

        /// <summary>Get/Set property.  Getter returns true if the current objects Role is as a Client.  Setter sets the Role based to match the given boolean value as true => Client and false => Server</summary>
        public bool IsClient { get { return (Role == ConnectionRole.Client); } set { Role = (value) ? ConnectionRole.Client : ConnectionRole.Server; } }

        /// <summary>This property defines the maximum time that the helper expects to elapse between when it passes a PushParameter item to a connection Push call and when it expects the corresponding SeqNum to be passed back as in an incomming PPItem's AckSeqNum.</summary>
        public TimeSpan AckWaitTimeLimit { get; set; }

        /// <summary>Thie property definss the maximum number of Un-Acked Push calls that can be pending at one time in the pendingPushedPPItemTrackerQueue.  Default is 100.</summary>
        public int MaxPendingPushCount { get; set; }

        /// <summary>This property defines the maximum Un-Acked estimated pushed byte count that can be pending at one time in the pendingPushedPPItemTrackerQueue.  Default is 10000000</summary>
        public int MaxPendingEstimatedPushedByteCount { get; set; }

        /// <summary>This property defines the maximum estimated content size in bytes be included in a single Push call.  Default value is 250000.</summary>
        public int NominalMaxEstimatedByteCountPerPushItem { get; set; }

        /// <summary>This propery defines the nominal amount of time that can elapse after which the helper will push an empty PPItem containing only a new SeqNum (as well as a AckSeqNum).</summary>
        public TimeSpan SendPingAfterIdleTimePeriodHasElpased { get; set; }

        /// <summary>Defines the nominal amount of time that this helper puts between adjacent efforts to scan the local IVA's for updates that need to be propagated.</summary>
        public TimeSpan NominalOutgoingIVIScanInterval { get; set; }

        #endregion

        #region public methods and related state information

        /// <summary>Publically accessible queue of PushParameter items that have been delivered from the other end of the WCF connection.</summary>
        public readonly Queue<PushParameter> IncommingPushParametersQueue = new Queue<PushParameter>();

        /// <summary>
        /// This method accepts a IProviderActionBase{string, NullObj}, assigns a UUID to it, enqueues a RemoteServiceActionRequest and adds it to the tracking table
        /// used to handle RemoteServiceActionUpdate items.
        /// </summary>
        public void SendStartRequestAndTrackAction(IProviderFacet providerFacet, string serviceName)
        {
            CreateStartAndAddActionTracker(providerFacet, serviceName);
        }

        /// <summary>
        /// This method is used to Release any pending work.
        /// In particular this method aborts all pending actions that have been given to this connection for transfer and execution by the remote end.
        /// Once this has happened the local end of these actions will no longer be able to track state updates from the remote end.
        /// </summary>
        public void Release()
        {
            // abort all pending actions that have been started/sent to the remote end.
            foreach (ServiceActionProviderFacetTracking providerFacetTracking in uuidToServiceActionProviderFacetTrackingDictinary.Values)
            {
                providerFacetTracking.Action.CompleteRequest("Remote connection was severed before action complete update was received");
            }
        }

        /// <summary>
        /// This variable holds the last SeqNum from the IVI table taken just before main Service method last serviced each of the IVATrackingItems.
        /// This allows the Service method to optimize out all work involving scanning of the states of tracked IVAs whenever the IVI table has had no changes made to it since the last such scan.
        /// </summary>
        private uint lastServicedIVISeqNum = 0;

        private QpcTimer scanTimer = new QpcTimer() { AutoReset = false, TriggerIntervalInSec = 0.001, Started = true };

        /// <summary>
        /// Main service method for this connection helper.
        /// This method processes all of the incomming PPItems, 
        /// Tracks new IVI names, changes to IVA items that are being relayed through this connection,
        /// Generates Registration, Add Name Request and Normal update ValuePropagationItems,
        /// generates and Push's these VPI's by including them in PushParameter that are given the api's Push method,
        /// and keeps track of how many PPItems have been pushed so that it keeps the outgoing connection busy without
        /// overloading the buffer space in either direction while still confirming that the PPItems are actually being processed by the other end,
        /// informs the other end about its progess in processing the pushed items that it has recieved from the other end.
        /// </summary>
        public void Service()
        {
            // process incoming PushParameter items

            int processedIncommingPPItemCount = 0;
            bool havePendingIVAValuesToSet = false;

            while (IncommingPushParametersQueue.Count > 0)
            {
                PushParameter ppItem = IncommingPushParametersQueue.Dequeue();
                bool itemHasSeqNum = ppItem.HasSeqNum;
                Int32 itemSeqNum = ppItem.SeqNum;

                ProcessAndReleaseIncommingPushParameter(ref ppItem, ref havePendingIVAValuesToSet);

                if (!IsAbortRequested && itemHasSeqNum)
                    lastSuccessfullyProcessedPushParameterSeqNum = itemSeqNum;

                processedIncommingPPItemCount++;
            }

            if (processedIncommingPPItemCount != 0)
                lastConnectionActivityTimeStamp.SetToNow();

            if (havePendingIVAValuesToSet)
            {
                SetAllPendingIVAs();
            }

            // service pending ServiceActions that we have sent.
            ServiceForwardPendingServiceActionCancelRequests();

            // if we already have Action Complete RSAUs to send then force a full IVI scan, even if the PendingVPIQueue is not empty. - things can get added to the pending rsau queue due to processing of inbound ppItems.
            // when connection is in registration phase we ignore this heuristic so that we allow the normal registration prioritiztion to take place.
            bool forceFullIVIScan = !PendingRSAUCompleteQueueIsEmpty && !IsStateInRegistrationPhase;

            // if the scanner is ready to make more IVI's and Action Complete RSAU's then scan service the pending client facet items to see if any of the actions we have created and started have changed
            // state and need to have update items sent to the other end.  The scanner is always ready to accept new normal RSAU items.
            if (PendingVPIQueueIsEmpty && PendingRSAUCompleteQueueIsEmpty)
            {
                ServiceClientFacetTracking();

                forceFullIVIScan |= !PendingRSAUCompleteQueueIsEmpty;
            }

            // if pending propagation item list is empty (or we are forced to by pending rsaus) then 
            //     service added IVI names
            //     service IVI and existing tracking objects
            //     generate pending VPI items.

            uint capturedIVIGlobalSeqNum = IVI.GlobalSeqNum;
            bool iviGlobalSeqNumChanged = (lastServicedIVISeqNum != capturedIVIGlobalSeqNum);
            bool scanTimerIsTriggered = (IsStateInRegistrationPhase || scanTimer.IsTriggered);

            if (forceFullIVIScan || (PendingVPIQueueIsEmpty && iviGlobalSeqNumChanged && CanConnectionHandleVPIItemsNow && scanTimerIsTriggered))
            {
                scanTimer.Start(NominalOutgoingIVIScanInterval);

                HandleAddedIVIItemsIfNeeded();

                ServiceIVATrackingItemsAndGeneratePendingVPIs();

                lastServicedIVISeqNum = capturedIVIGlobalSeqNum;
            }

            // Check for transmit timeout on previously pushed ppItems
            PushedPPItemTracker ppItemTracker = PeekFirstPendingPushedPPItemInQueue();
            TimeSpan ppItemTrackerAge = ((ppItemTracker != null) ? ppItemTracker.TimeStamp.Age : TimeSpan.Zero);
            if (ppItemTracker != null && ppItemTrackerAge > AckWaitTimeLimit)
            {
                AbortReason = "Timeout waiting for Ack for Pushed Item SeqNum:{0} after {1:f3} seconds".CheckedFormat(ppItemTracker.PPItem.SeqNum, ppItemTrackerAge.TotalSeconds);
            }

            // service pushing new ppItems to carry pending  VPIs and/or unacked received seq nums as needed.
            if (!PendingVPIQueueIsEmpty || !PendingRSARQueueIsEmpty || !PendingRSAUQueueIsEmpty || !PendingRSAUCompleteQueueIsEmpty)
            {
                // this loop runs until we have filled up the pendingPushedPPItemTrackerQueue with unacked items or we have run out of items to add
                while (!IsPendingPushedPPItemTrackerQueueLimitReached && !IsAbortRequested && !(PendingVPIQueueIsEmpty && PendingRSARQueueIsEmpty && PendingRSAUQueueIsEmpty && PendingRSAUCompleteQueueIsEmpty))
                {
                    // we have stuff to push so get a ppItem to send them (or at least some of them) with.
                    PushParameter ppItem = ppFreeList.Get();
                    ppItem.AckSeqNum = lastSuccessfullyProcessedPushParameterSeqNum;

                    if (!PendingVPIQueueIsEmpty)
                    {
                        ppItem.VPIList = vpilFreeList.Get();

                        while (!PendingVPIQueueIsEmpty && ppItem.EstimatedContentSizeInBytes < NominalMaxEstimatedByteCountPerPushItem)
                        {
                            ValuePropagationItem vpi = pendingVPIQueue.Dequeue();

                            ppItem.VPIList.Add(vpi);
                            ppItem.EstimatedContentSizeInBytes += vpi.EstimatedContentSizeInBytes;
                        }
                    }

                    if (!PendingRSARQueueIsEmpty)
                    {
                        ppItem.RSARList = rsarlFreeList.Get();

                        while (!PendingRSARQueueIsEmpty && ppItem.EstimatedContentSizeInBytes < NominalMaxEstimatedByteCountPerPushItem)
                        {
                            RemoteServiceActionRequest rsar = pendingRSARQueue.Dequeue();
                            ppItem.RSARList.Add(rsar);
                            ppItem.EstimatedContentSizeInBytes += rsar.EstimatedContentSizeInBytes;
                        }
                    }

                    if (!PendingRSAUQueueIsEmpty)
                    {
                        ppItem.RSAUList = rsaulFreeList.Get();

                        while (!PendingRSAUQueueIsEmpty && ppItem.EstimatedContentSizeInBytes < NominalMaxEstimatedByteCountPerPushItem)
                        {
                            RemoteServiceActionUpdate rsau = pendingRSAUQueue.Dequeue();
                            ppItem.RSAUList.Add(rsau);
                            ppItem.EstimatedContentSizeInBytes += rsau.EstimatedContentSizeInBytes;
                        }
                    }

                    // we can send NV update RSAUs whenever we want but we can only send action complete RSAUs after the pendingVPIQueue is empty, so that all IVI side effects visible
                    // after the Action completed have been delivered to and processed by the other end before the complete updates are delivered and processed.
                    if (!PendingRSAUCompleteQueueIsEmpty && PendingVPIQueueIsEmpty)
                    {
                        if (ppItem.RSAUList == null)
                            ppItem.RSAUList = rsaulFreeList.Get();

                        while (!PendingRSAUCompleteQueueIsEmpty && ppItem.EstimatedContentSizeInBytes < NominalMaxEstimatedByteCountPerPushItem)
                        {
                            RemoteServiceActionUpdate rsau = pendingRSAUCompleteQueue.Dequeue();
                            ppItem.RSAUList.Add(rsau);
                            ppItem.EstimatedContentSizeInBytes += rsau.EstimatedContentSizeInBytes;
                        }
                    }

                    ppItem.SeqNum = GetNextSeqNum();        // this ppItem needs to be acked
                    PushGivenPPItem(ref ppItem);
                }

                if (!allRegistrationItemsHaveBeenGenerated && PendingVPIQueueIsEmpty)
                    allRegistrationItemsHaveBeenGenerated = true;
            }

            // if the other side is waiting for us to generate an Ack and we did not send any new PPItems on this iteration then generate a non-sequenced one that is just used to give the
            // ack back to the other side.  Also if the connection has been idle for to long then push an empty item accross.

            bool pushEndOfRegistrationItemIsNeeded = IsStateInRegistrationPhase && allRegistrationItemsHaveBeenGenerated;
            bool pushPingItemIsNeeded = (!IsStateInRegistrationPhase && PendingVPIQueueIsEmpty && ConnectionIdleAge >= SendPingAfterIdleTimePeriodHasElpased);  // don't send pings during registration so the other end will not get the wrong impression about end of registration.
            bool pushItemWithSeqNumIsNeeded = (pushEndOfRegistrationItemIsNeeded || pushPingItemIsNeeded);
            bool pushAckOnlyItemIsNeeded = (lastPushedAckSeqNum != lastSuccessfullyProcessedPushParameterSeqNum);

            if ((pushItemWithSeqNumIsNeeded || pushAckOnlyItemIsNeeded) && !IsAbortRequested)
            {
                PushParameter ppItem = ppFreeList.Get();
                ppItem.AckSeqNum = lastSuccessfullyProcessedPushParameterSeqNum;
                ppItem.SeqNum = (pushItemWithSeqNumIsNeeded ? GetNextSeqNum() : 0);   // give this ppItem a SeqNum if it is being used for a ping (needs to be acked) but not if it is an ack only item.

                PushGivenPPItem(ref ppItem);

                if (pushEndOfRegistrationItemIsNeeded)
                    CompleteRegistrationPhase("Server has pushed end of registration item.");
            }
        }

        /// <summary>
        /// This method is used to process and consume each PushParameter item that the Service method pulls from the IncommingPushParametersQueue.
        /// It handles client side detection of the end of the registration phase,
        /// It calls HandlePPItemAckSeqNum if the incoming ppItem includes a non-zero AckSeqNum,
        /// It processes each ValuePropagationItem in the ppItem's VPIList (if non-null and non-empty) using the ProcessReceivedValuePropagationItem method,
        /// and finally it releases the ppItem (and all objects it contains) into the corresponding free lists.
        /// </summary>
        private void ProcessAndReleaseIncommingPushParameter(ref PushParameter ppItem, ref bool havePendingIVAValuesToSet)
        {
            // detect end of Registration phase for clients
            if (IsStateInRegistrationPhase && IsClient)
            {
                if (ppItem.VPIList == null || ppItem.VPIList.Count == 0)
                    CompleteRegistrationPhase("recieved Push with no ValueItems [SeqNum:{0}]".CheckedFormat(ppItem.SeqNum));
                else if (ppItem.VPIList[0].Name.IsNullOrEmpty())
                    CompleteRegistrationPhase("Recieved Pushed ValueItem with no Name [SeqNum:{0}]".CheckedFormat(ppItem.SeqNum));
            }

            // handle any AckSeqNum carried by this ppItem
            if (ppItem.HasAckSeqNum)
            {
                HandlePPItemAckSeqNum(ppItem.AckSeqNum);
            }

            // handle any ValueItemList carried by this item.
            if (ppItem.VPIList != null)
            {
                if (ppItem.HasSeqNum)
                {
                    foreach (ValuePropagationItem vpi in ppItem.VPIList)
                    {
                        ProcessReceivedValuePropagationItem(vpi, ref havePendingIVAValuesToSet);
                    }
                }
                else
                {
                    AbortReason = "Received invalid PPItem containing a VPIList but no SeqNum";
                }
            }

            if (ppItem.RSARList != null)
            {
                foreach (RemoteServiceActionRequest rsas in ppItem.RSARList)
                {
                    ProcessRemoteServiceActionRequest(rsas);
                }
            }

            if (ppItem.RSAUList != null)
            {
                foreach (RemoteServiceActionUpdate rsau in ppItem.RSAUList)
                {
                    ProcessRemoteServiceActionUpdate(rsau);
                }
            }

            // place the received ppItem in the free list.
            Release(ref ppItem);
        }

        /// <summary>
        /// This method is responsible for catagorizing, validating and processing each ValuePropagationItem that the connection helper is given (from the other end).
        /// For Normal Update Record type vpi's this consists of finding the IVATrackingItem from the ID value in the vpi and using it so accept the VC value and pass it
        /// to the ValueAccessor for this item.
        /// For Clients this method handles Registration Records by creating IVATrackingItems for each newly registered item, providing the tracking item with the Assigned ID
        /// and passing the VC value into the corresponding ValueAccessor if the vpi's VC was not Empty.
        /// For Servers this method handles Add Name Requests in a similar manner.  Please note that the server will generate Registration Item for newly added IVATrackingItems
        /// as this method does not set the IVATrackingItem's HasRegistrationBeenGenerated property.  That will only be done later when the server is scanning the table for new
        /// VPI's that need to be generated and pushed.
        /// </summary>
        private IVATrackingItem ProcessReceivedValuePropagationItem(ValuePropagationItem vpi, ref bool havePendingIVAValuesToSet)
        {
            IVATrackingItem item = null;

            // first handle the symetic high rate case - where a successfully registered value is being passed.
            if (vpi.IsNormalUpdateRecord)
            {
                // non-registration/addition access - item must already exist in ID table
                item = FindTrackingItem(vpi.ID);

                if (item != null)
                {
                    item.IVA.ValueContainer = vpi.VC;       // may mark set as pending if the VC contents is differnt than the current ValueContainer contents.

                    if (!havePendingIVAValuesToSet)
                        havePendingIVAValuesToSet = item.IVA.IsSetPending;
                }
                else
                    AbortReason = "Encountered Unknown/Unregistered ID:{0} from {1}".CheckedFormat(vpi.ID, OtherEndRole);
            }
            else if (IsClient)
            {
                // this must be a registration item
                if (vpi.IsRegistrationRecord)
                {
                    item = FindTrackingItem(vpi.Name, true);

                    if (!item.IsIDAssigned)
                    {
                        item.AssignedID = vpi.ID;
                        assignedIDToTrackingItemDictionary[vpi.ID] = item;
                    }

                    if (item != null)
                    {
                        if (!vpi.VC.IsEmpty)
                        {
                            item.IVA.ValueContainer = vpi.VC;

                            if (!havePendingIVAValuesToSet)
                                havePendingIVAValuesToSet = item.IVA.IsSetPending;
                        }
                        // else the given vpi VC is empty - do not trigger any local value changes in the item's IVA.
                    }
                    else
                    {
                        AbortReason = "Internal issue: no tracking item could be created for ID:{0}, Name:'{1}' from {2}".CheckedFormat(vpi.ID, vpi.Name, OtherEndRole);
                    }
                }
                else
                {
                    AbortReason = "Encountered Unexpected partial registration for ID:{0}, Name:'{1}' from {2}".CheckedFormat(vpi.ID, vpi.Name, OtherEndRole);
                }
            }
            else
            {
                // this must be an add item request from the client to the server

                if (vpi.IsAddNameRequestRecord)
                {
                    item = FindTrackingItem(vpi.Name, true);

                    if (item != null)
                    {
                        if (!vpi.VC.IsEmpty)
                        {
                            item.IVA.ValueContainer = vpi.VC;

                            if (!havePendingIVAValuesToSet)
                                havePendingIVAValuesToSet = item.IVA.IsSetPending;
                        }
                        // else the given vpi VC is empty - do not trigger any local value changes in the item's IVA.
                    }
                    else
                    {
                        AbortReason = "Internal issue: no tracking item could be created for ID:{0}, Name:'{1}' from {2}".CheckedFormat(vpi.ID, vpi.Name, OtherEndRole);
                    }
                }
                else
                {
                    AbortReason = "Encountered Unexpected add request for ID:{0}, Name:'{1}' from {2}".CheckedFormat(vpi.ID, vpi.Name, OtherEndRole);
                }
            }

            return item;
        }

        /// <summary>
        /// This method is used to handle each newly received, known non-zore, ackSeqNum from the sequence of incomming PushParameter items.
        /// This method loops from the last processed incomming ack seq to the new ack seq num and verifies that each new ack seq num that is processed in this range
        /// matches the first PushedPPItemTracker's PPItem.SeqNum in the pendingPushedPPItemTrackerQueue and then removes and releases it.
        /// </summary>
        private void HandlePPItemAckSeqNum(int newAckSeqNumValue)
        {
            if (lastProcessedIncommingAckSeqNum != newAckSeqNumValue)
            {
                int processingAckSeqNumValue = lastProcessedIncommingAckSeqNum + 1;

                /// handle acking each of the sequence numbers from the lastProcessedIncommingAckSeqNum through to the given ppItemAckSeqNum
                /// for each seq number in this range, verify that the seqNum matches the next PushedPPItemTracker one and then pull and release that PushedPPItemTracker.
                /// if any AckedSeqNum does not match the next pending SeqNum that was sent then abort the connection.
                /// 

                for (; ; )
                {
                    PushedPPItemTracker ppItemTracker = PeekFirstPendingPushedPPItemInQueue();
                    Int32 ppItemTrackerSeqNum = ((ppItemTracker != null) ? ppItemTracker.PPItem.SeqNum : 0);

                    if (processingAckSeqNumValue == ppItemTrackerSeqNum)
                    {
                        pendingPushedPPItemTrackerQueue.Dequeue();
                        Release(ref ppItemTracker);

                        if (processingAckSeqNumValue == newAckSeqNumValue)
                        {
                            // record that we just processed the newly given one (thus the loop is done)
                            lastProcessedIncommingAckSeqNum = processingAckSeqNumValue;

                            return;
                        }
                        else
                        {
                            // the ppItemTracker we just acked is still not the one that the caller passed to us.  Advance to start processing the next one.
                            processingAckSeqNumValue += 1;
                        }
                    }
                    else
                    {
                        AbortReason = "Recevied unexpeced AckSeqNum:{0} which does not match the next pending Pushed item SeqNum:{1}".CheckedFormat(newAckSeqNumValue, ppItemTrackerSeqNum);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This method accepts either a normal or an ack only PushParameter item and attempts to call the CommonAPIConnectionProxy's Push method to accept the given ppItem.
        /// If the ppItem has an assigned SeqNum value then this method also generates a PushedPPItemTracker object and adds it to the pendingPushedPPItemTrackerQueue for later
        /// processing in the incomming ppitem AckSeqNum handling code.
        /// </summary>
        private void PushGivenPPItem(ref PushParameter ppItem)
        {
            bool ppItemHasSeqNumAndNeedsToBeAcked = (ppItem.HasSeqNum);
            PushedPPItemTracker ppItemTracker = null;

            if (ppItemHasSeqNumAndNeedsToBeAcked)
            {
                ppItemTracker = pushedPPItemTrackerFreeList.Get();
                ppItemTracker.PPItem = ppItem;
                ppItemTracker.TimeStamp = QpcTimeStamp.Now;
            }

            try
            {
                CommunicationState commState = ICommunicationObject.State;

                if (commState == CommunicationState.Opened)
                    CommonAPIConnectionProxy.Push(ppItem);
                else
                    Logger.Debug.Emit("Cannot Push given PPItem.SeqNum:{0} on connection that is in state {1}", ppItem.SeqNum, commState);

                lastConnectionActivityTimeStamp.SetToNow();

                if (ppItemHasSeqNumAndNeedsToBeAcked)
                {
                    pendingPushedPPItemTrackerQueue.Enqueue(ppItemTracker);
                    ppItem = null;      // we have consumed this ppItem and will return it to the free list later.  So do not release it at the bottom of this method.
                }
            }
            catch (System.Exception ex)
            {
                int seqNum = (ppItemHasSeqNumAndNeedsToBeAcked ? ppItemTracker.PPItem.SeqNum : 0);
                AbortReason = "Connection failure: Unable to Push Item SeqNum:{0} [{1}]".CheckedFormat(seqNum, ex);
            }

            // if the above logic did not retain the given ppItem then explicitly release it at this point.
            if (ppItem != null)
            {
                ppFreeList.Release(ref ppItem);
            }
        }

        #endregion

        #region HelperState

        /// <summary>
        /// This enum is used to define the "State" of the helper as Registering or Connected.
        /// This enum is not used for any form of abort or error handling.
        /// </summary>
        public enum HelperState : int
        {
            /// <summary>This state is used by the Client to know when it is permitted to start generating VPI items, including Add Name Request items.</summary>
            Registering,
            /// <summary>This state represents the normal state in which both ends can send all item types.</summary>
            Connected,
        }

        /// <summary>Gives the current value of the "State" of the connection helper</summary>
        public HelperState State { get; private set; }

        /// <summary>A Server connection uses this to determine when it has generated all of the initially required Registartion Records.  It uses this to force generation of an empty PushParameter that tells the client that registration has completed so the client can start pushing items back.</summary>
        private bool allRegistrationItemsHaveBeenGenerated = false;

        /// <summary>Predicate returns true if State is Registering</summary>
        public bool IsStateInRegistrationPhase { get { return (State == HelperState.Registering); } }

        /// <summary>This method records that the state is now Connected and logs a message to indicate why the state was changed from Registering to Connected.</summary>
        private void CompleteRegistrationPhase(string reason)
        {
            HelperState entryState = State;
            State = HelperState.Connected;

            Logger.Debug.Emit("State changed to {0} [from {1}, reason:'{2}']", State, entryState, reason);
        }

        #endregion

        #region IVI, IVA, and IVATrackingItem: tracking and handling.

        /// <summary>
        /// This method is used by the main Service method to collect a set of IVA's from the set of all IVATrackingItem's where the IVA has its IsSetPending flag set.
        /// This sub-set is collected into the workingIVAArray and is then given to the IVI.Set method with optimizations turned off so that it will set each of the 
        /// corresponding Table entries from the given IVA contained values.
        /// </summary>
        private void SetAllPendingIVAs()
        {
            UpdateItemArraysIfNeeded();

            int workingCount = 0;

            foreach (IVATrackingItem item in allIVATrackingItemArray)
            {
                if (item != null && item.IsIDAssigned && item.IVA.IsSetPending)
                {
                    workingIVAArray[workingCount++] = item.IVA;
                }
            }

            // "null terminate" the working array for easier debugging
            if (workingCount < workingIVAArray.Length)
                workingIVAArray[workingCount] = null;

            if (workingCount > 0)
            {
                IVI.Set(workingIVAArray, workingCount, false);
            }
        }

        /// <summary>
        /// This method is used to generate and queue all ValuePropagationItems that this connection helper generates.  
        /// It does this with a sequence of sweeps through the set of all tracked items to collect the ones that need to be updated, or registered,
        /// Then it updates all of these items using a single IVI update call and finally it generates VPI items for each updated IVATrackedItem and enqueues
        /// them in the pendingVPIQueue which will eventually be pushed to the other side in a sequence of contiguous Push calls.
        /// </summary>
        private void ServiceIVATrackingItemsAndGeneratePendingVPIs()
        {
            UpdateItemArraysIfNeeded();

            // build a pair of arrays of the items (and IVAs) where the item has an assigned ID and 
            int workingCount = 0;

            foreach (IVATrackingItem item in allIVATrackingItemArray)
            {
                bool normalUpdateNeeded = (item != null && item.IsIDAssigned && item.IVA.IsUpdateNeeded);
                bool serverRegUpdateNeeded = (item != null && item.IsIDAssigned && (IsServer && !item.HasRegistrationBeenGenerated));
                bool clientRegUpdateNeeded = (item != null && !item.IsIDAssigned && (IsClient && !item.HasClientAddBeenGenerated));

                if (normalUpdateNeeded || serverRegUpdateNeeded || clientRegUpdateNeeded)
                {
                    workingIVAArray[workingCount] = item.IVA;
                    workingIVATrackingItemArray[workingCount++] = item;                
                }
            }

            // "null terminate" the working arrays for easier debugging
            if (workingCount < workingIVAArray.Length)
            {
                workingIVAArray[workingCount] = null;
                workingIVATrackingItemArray[workingCount] = null;
            }

            if (workingCount > 0)
            {
                IVI.Update(workingIVAArray, workingCount);

                for (int idx = 0; idx < workingCount; idx++)
                {
                    IVATrackingItem item = workingIVATrackingItemArray[idx];

                    ValuePropagationItem vpi = vpiFreeList.Get();

                    // determine if we should generate a server registration request, a client add name request or a normal update item.
                    if (IsServer && !item.HasRegistrationBeenGenerated)
                    {
                        // server registration record - includes AssignedID, Name, and VC
                        vpi.ID = item.AssignedID;
                        vpi.Name = item.ConnectionValueName;

                        item.HasRegistrationBeenGenerated = true;
                    }
                    else if (IsClient && !item.HasClientAddBeenGenerated)
                    {
                        // client add name request - includes Name and VC
                        vpi.Name = item.ConnectionValueName;

                        item.HasClientAddBeenGenerated = true;
                    }
                    else
                    {
                        // normal update request - includes AssignedID and VC
                        vpi.ID = item.AssignedID;
                    }

                    vpi.VC = (item.IVA.HasValueBeenSet ? item.IVA.ValueContainer : ValueContainer.Empty);

                    pendingVPIQueue.Enqueue(vpi);
                }
            }
        }

        /// <summary>
        /// Returns true if the state permits this object to handle IVI table additions now.  
        /// This is always true when Role == Server and it is true for Clients after the Registration phase is over.
        /// </summary>
        private bool CanConnectionHandleVPIItemsNow
        {
            get
            {
                return (IsServer || (IsClient && !IsStateInRegistrationPhase));
            }
        }

        /// <summary>
        /// This variable retains the length (number of value names) of the IVI table that this connection helper has already gone through.
        /// This allows the HandleAddedIVIItemsIfNeeded method to quickly determine if there are any new names to go through and to only obtain the set of those
        /// new names when needed (rather than re-optaining the full set of names each time).
        /// </summary>
        private int lastServicedIVITableLength = 0;

        /// <summary>
        /// This method checks to see if any new names have been added to the IVI since the last time that the connection helper has been through them,
        /// to obtain the set of such newly added names, to go through the set and filter out the ones that shall/should be regsitered/added for propagation through the connection
        /// and finally to find or create IVATrackingItems for each of them.
        /// </summary>
        private void HandleAddedIVIItemsIfNeeded()
        {
            if (lastServicedIVITableLength != IVI.ValueNamesArrayLength)
            {
                string[] addedIVINames = IVI.GetValueNamesRange(lastServicedIVITableLength, 0);

                foreach (string addedName in addedIVINames)
                {
                    if (!LocalNameMatchRuleSet.MatchesAny(addedName))
                        continue;

                    string connectionNameForThisItem = addedName;

                    if (!AddAndRemoveLocalPrefix.IsNullOrEmpty())
                    {
                        if (addedName.StartsWith(AddAndRemoveLocalPrefix))
                            connectionNameForThisItem = addedName.Substring(AddAndRemoveLocalPrefix.Length);
                        else
                            continue;
                    }

                    // check if there is already a tracker for this object and add it if needed.
                    IVATrackingItem item = FindTrackingItem(connectionNameForThisItem, true);
                }

                lastServicedIVITableLength += addedIVINames.Length;
            }
        }

        /// <summary>
        /// This class contains all of the information about a single value name that is needed to define its existance in the corresponding IVI (using a ValueAccessor object)
        /// and to track, propagate and update its value using the connection.
        /// </summary>
        private class IVATrackingItem
        {
            /// <summary>
            /// Constructor is given the ivi for the table from which to obtain, or in which to add, the corresonding value accessor, and the name of the table entry as it is expected
            /// to be given in this ivi.
            /// </summary>
            public IVATrackingItem(IValuesInterconnection ivi, string tableItemName)
            {
                IVA = ivi.GetValueAccessor(tableItemName);
            }

            /// <summary>This is the IValueAccessor instance that is being accessed and tracked using this tracking item.</summary>
            public IValueAccessor IVA { get; private set; }

            /// <summary>This give the connection version of the IVA's name which may be different from the name used to obtain/create the IVA itself</summary>
            public string ConnectionValueName { get; set; }

            /// <summary>
            /// When this value is non-zero, it contains the assigned ID that is used when refering to this item in conection traffic.  
            /// The Server assigns this value sequentially and passes it to the Client with a Registration Record.  
            /// The Client obtains if from the Server in the Registration Record and uses a local dictionary to find the tracking item for any given Update Record that it is processing
            /// from the Server.
            /// </summary>
            public Int32 AssignedID { get; set; }

            /// <summary>Returns true if the AssignedID property is not zero.</summary>
            public bool IsIDAssigned { get { return AssignedID != 0; } }

            /// <summary>Only used by Servers.  Set to true after the server has generated and enqueued a pending VPI containing a Registration Record for this item.</summary>
            public bool HasRegistrationBeenGenerated { get; set; }

            /// <summary>Only used by Clients.  Set to true after the client has generated and enqueued a pending VPI containing an Add item Record for this item.</summary>
            public bool HasClientAddBeenGenerated { get; set; }
        }

        /// <summary>
        /// This is the "working" array version of the allIVATrackingItemList.  
        /// The vast majority of the time this connection is accessing this array in place of the corresponding list for update scan and ID to item lookup work (on the Server end).
        /// The use of a pre-allocated array is expected to improve performance and allocation efficiency by allowing iterators and indexers to avoid needing to convert the list to an array each time it needs to be used.
        /// </summary>
        private IVATrackingItem[] allIVATrackingItemArray = null;

        /// <summary>
        /// This is a "working" array that is used to contain IValueAccessors (IVA) for use with IVI.Update and IVI.Set calls.  Generally this array is filled with a sub-set of the
        /// overall list of known IVA objects based on local logic that determines what sub-set is needed and then the array is passed, along with the number of items that have been put in it, to the
        /// corresponding IVI method.  This allows the connection to do optimized transfer of sets of IVA's with minimal lock/release load on the underlying table.  This array is always
        /// allocated to the same length as the allIVATrackignItemArray so that any sub-set, incluing all of the known IVAs, can be used without additional length checking and/or re-allocation.
        /// </summary>
        private IValueAccessor[] workingIVAArray = null;

        /// <summary>
        /// This is a "working" array version of the allIVATrackingItemArray.  
        /// It is used during scan and value packing as part of the logic used to generate VPIs.  
        /// On each appropriate Service iteration, it is re-filled contain the set of IVATrackingItems that correspond 
        /// to the set of IVA objects that are placed in the workingIVAArray.  Then once the workingIVAArray has been Updated, the IVATrackingItems are then used to 
        /// generate the VPI Update items that needs to be sent to the other end of the connection.  As with the workingIVAArray, this array is used to minimize the use of
        /// Lists for this high rate activity and thus to eliminate any amount of memory churn caused by the repeated use, and iteration through, variable length lists.
        /// </summary>
        private IVATrackingItem[] workingIVATrackingItemArray = null;

        /// <summary>
        /// This method is used to reset the "working" arrays any time a new item is added to the allIVATrackingItemList.  
        /// This forces the next call to UpdateItemArraysIfNeeded to rebuild/reallocate the working arrays so that their allocated length
        /// always matches the length of the allIVATrackingItemList.
        /// </summary>
        private void ResetArrays()
        {
            // trigger it to be rebuilt on next use.
            allIVATrackingItemArray = null;
            workingIVAArray = null;
            workingIVATrackingItemArray = null;
        }

        /// <summary>
        /// This method is used to regenerate the working arrays (and the quick iteration allIVATrackingItemArray) whenever the length of the allIVATrackingItemList changes/increases.
        /// Whenever a new IVATrackingItem is added to the tracking list, the allIVATrackingItemArray is set to null.  Then the next time that another method is about to need or use
        /// any of these arrays it calls this method to make certain that the arrays are regenerated/reallocated before the caller attempts to use them.
        /// </summary>
        private void UpdateItemArraysIfNeeded()
        {
            // if needed, rebuild the arrays used to process IVA's (happens whenever a new item is added to the allIVATrackingItemList.
            if (allIVATrackingItemArray == null || workingIVAArray == null || workingIVATrackingItemArray == null)
            {
                allIVATrackingItemArray = allIVATrackingItemList.ToArray();
                workingIVAArray = new IValueAccessor[allIVATrackingItemList.Count];
                workingIVATrackingItemArray = new IVATrackingItem[allIVATrackingItemList.Count];
            }
        }

        /// <summary>
        /// This list contains all of the tracking items for this session/connection.  The first element is always initialized to null reserve the default value of ID (0) as an invalid/reserved value.
        /// </summary>
        private List<IVATrackingItem> allIVATrackingItemList = new List<IVATrackingItem>() { null };

        /// <summary>
        /// This dictionary is used to find/determine if an IVATrackingItem already exists for a given connection name, typically so one can be created if it 
        /// does not already exist.  This dictionary is typically only used during the connection Registration phase and anytime thereafer when the connection detects that a
        /// new matching name has been added to the IVI that the connection is working from.
        /// </summary>
        private Dictionary<string, IVATrackingItem> connectionValueNameToTrackingItemDictionary = new Dictionary<string, IVATrackingItem>();

        /// <summary>This dictionary is used on Client connection ends to index from an AssignedID to a matching IVATrackingItem.  This done as part of processing of normal Update Records by the Client.</summary>
        private Dictionary<Int32, IVATrackingItem> assignedIDToTrackingItemDictionary = new Dictionary<int, IVATrackingItem>();

        /// <summary>
        /// This method attempts to find and return an IVATrackingItem from the given assignedID.  
        /// On the Server this is done by attempting to index the allIVATrackingItemArray (rebuilt if needed) using the given assignedID.
        /// On the Client this is done by looking in the assignedIDToTrackingItemDictionary for the given assignedID.
        /// In either case the item, if found, is returned or null is returned if no such matching tracking item was found.
        /// </summary>
        private IVATrackingItem FindTrackingItem(Int32 assignedID)
        {
            UpdateItemArraysIfNeeded();

            IVATrackingItem item = null;

            if (IsServer)
                item = allIVATrackingItemArray.SafeAccess(assignedID, null);
            else
                assignedIDToTrackingItemDictionary.TryGetValue(assignedID, out item);

            return item;
        }

        /// <summary>
        /// This method is used to Find, and optionally create, an IVATrackingItem from a given connectionValueName.
        /// If there is already a matching item in the connectionValueNameToTrackingItemDictionary then that value is returned.
        /// If not and the caller would like one created then the corresponding tableName is created (using the AddAndRemoveLocalPrefix property) and a new IVATrackingItem is
        /// created and added using AddNewTracingItem.
        /// The method returns the created IVATrackingItem or null if the caller indicated that one should not be created.
        /// </summary>
        private IVATrackingItem FindTrackingItem(string connectionValueName, bool createIfNeeded)
        {
            IVATrackingItem item = null;

            connectionValueNameToTrackingItemDictionary.TryGetValue(connectionValueName ?? String.Empty, out item);
            if (item != null)
                return item;

            if (createIfNeeded)
            {
                string tableName = (AddAndRemoveLocalPrefix.IsNullOrEmpty() ? connectionValueName : (AddAndRemoveLocalPrefix + connectionValueName));

                return AddNewTrackingItem(new IVATrackingItem(IVI, tableName) { ConnectionValueName = connectionValueName });
            }

            return null;
        }

        /// <summary>
        /// This is the common internal method that is used to handle the addition of new IVATrackingItem's.  
        /// In Servers it obtains and assigns the ID for this tracking item.
        /// It calls ResetArrays to force the working arrays to be rebuilt when they are next needed.
        /// It adds the item to the allIVATrackingItemList and adds it under its ConnectionValueName to the connectionValueNameToTrackingItemDictionary.
        /// Finally if the item has an AssignedID (either as a Server or as part of handling a Registration Record by a Client) then it adds the item under its AssignedID
        /// to the assignedIDToTrackingItemDictionary as well.
        /// </summary>
        private IVATrackingItem AddNewTrackingItem(IVATrackingItem item)
        {
            if (IsServer && !item.IsIDAssigned)
                item.AssignedID = allIVATrackingItemList.Count;

            ResetArrays();
            allIVATrackingItemList.Add(item);

            connectionValueNameToTrackingItemDictionary[item.ConnectionValueName] = item;

            if (item.IsIDAssigned)
                assignedIDToTrackingItemDictionary[item.AssignedID] = item;

            return item;
        }

        #endregion

        #region ProviderFacetTracking

        private class ServiceActionProviderFacetTracking
        {
            public IProviderFacet Action { get; set; }

            public string ActionUUID { get; set; }

            public bool RequestCancelHasBeenEnqueued { get; set; }

            public void Clear()
            {
                Action = null;
                ActionUUID = null;
                RequestCancelHasBeenEnqueued = false;
            }
        }

        private ServiceActionProviderFacetTracking[] workingServiceActionProviderFacetTrackingArray = null;
        private Dictionary<string, ServiceActionProviderFacetTracking> uuidToServiceActionProviderFacetTrackingDictinary = new Dictionary<string, ServiceActionProviderFacetTracking>();

        private void CreateStartAndAddActionTracker(IProviderFacet providerFacet, string serviceName)
        {
            ServiceActionProviderFacetTracking actionTracking = sapftFreeList.Get();

            actionTracking.ActionUUID = Guid.NewGuid().ToString();
            actionTracking.Action = providerFacet;

            RemoteServiceActionRequest rsar = rsarFreeList.Get();
            rsar.ActionUUID = actionTracking.ActionUUID;
            rsar.ServiceName = serviceName;
            rsar.NamedParamValues = providerFacet.NamedParamValues.MapEmptyToNull().ConvertToReadOnly();

            pendingRSARQueue.Enqueue(rsar);

            AddServiceActionProviderFacetTracking(actionTracking);
        }

        private void AddServiceActionProviderFacetTracking(ServiceActionProviderFacetTracking actionTracking)
        {
            uuidToServiceActionProviderFacetTrackingDictinary[actionTracking.ActionUUID] = actionTracking;
            workingServiceActionProviderFacetTrackingArray = null;
        }

        private void ServiceForwardPendingServiceActionCancelRequests()
        {
            if (workingServiceActionProviderFacetTrackingArray == null)
                workingServiceActionProviderFacetTrackingArray = uuidToServiceActionProviderFacetTrackingDictinary.Values.ToArray();

            if (workingServiceActionProviderFacetTrackingArray.Length > 0)
            {
                foreach (ServiceActionProviderFacetTracking actionTracker in workingServiceActionProviderFacetTrackingArray)
                {
                    if (actionTracker.Action.IsCancelRequestActive && !actionTracker.RequestCancelHasBeenEnqueued)
                    {
                        RemoteServiceActionRequest rsar = rsarFreeList.Get();
                        rsar.ActionUUID = actionTracker.ActionUUID;
                        rsar.RequestCancel = true;
                        pendingRSARQueue.Enqueue(rsar);
                    }
                }
            }
        }

        private void ProcessRemoteServiceActionUpdate(RemoteServiceActionUpdate rsau)
        {
            if (rsau == null || rsau.ActionUUID == null || rsau.ActionState == null)
            {
                AbortReason = "Recieved null or invalid RemoteServiceActionUpdate";
                return;
            }

            ServiceActionProviderFacetTracking actionTracking = null;
            uuidToServiceActionProviderFacetTrackingDictinary.TryGetValue(rsau.ActionUUID, out actionTracking);

            if (actionTracking != null)
            {
                if (rsau.ActionState.IsComplete)
                {
                    actionTracking.Action.CompleteRequest(rsau.ActionState.ResultCode, rsau.ActionState.NamedValues);

                    uuidToServiceActionProviderFacetTrackingDictinary.Remove(actionTracking.ActionUUID);
                    workingServiceActionProviderFacetTrackingArray = null;

                    sapftFreeList.Release(ref actionTracking);
                }
                else
                {
                    if (!actionTracking.Action.IsCancelRequestActive && rsau.ActionState.IsCancelRequested)
                        actionTracking.Action.RequestCancel();

                    if (!rsau.ActionState.NamedValues.IsNullOrEmpty())
                        actionTracking.Action.UpdateNamedValues(rsau.ActionState.NamedValues);
                }
            }
            else
            {
                Logger.Debug.Emit("Unexpected action update for {0}, '{1}'.  Discarded", rsau.ActionUUID, rsau.ActionState);
            }
        }

        #endregion

        #region ClientFacetTracking

        private class ServiceActionClientFacetTracking : INotifyable
        {
            public IStringParamAction Action { get; set; }
            public string ActionUUID { get; set; }
            public INotifyable NotifyableParentPart { get; set; }
            public volatile bool sendUpdate;

            public void Notify()
            {
                sendUpdate = true;
                NotifyableParentPart.Notify();
            }

            public void Clear()
            {
                Action = null;
                ActionUUID = null;
                NotifyableParentPart = null;
                sendUpdate = false;
            }
        }

        /// <summary>
        /// This is the service name that will be interpreted as a ping, rather than as a target part name and sub-service request name
        /// <para/>"$WcfServicePing$"
        /// </summary>
        public readonly static string PingRequestServiceName = "$WcfServicePing$";

        private void ProcessRemoteServiceActionRequest(RemoteServiceActionRequest rsas)
        {
            if (rsas == null || rsas.ActionUUID == null)
            {
                AbortReason = "Recieved null or invalid RemoteServiceActionRequest";
                return;
            }

            ServiceActionClientFacetTracking actionTracking = null;

            uuidToServiceActionClientFacetTrackingDictionary.TryGetValue(rsas.ActionUUID, out actionTracking);

            if (actionTracking == null && !rsas.RequestCancel)
            {
                string requestServiceName = (rsas.ServiceName ?? String.Empty);
                INamedValueSet requestNamedParamValues = rsas.NamedParamValues.MapNullToEmpty();
                bool isPingRequest = (requestServiceName == PingRequestServiceName);
                bool isLocalRequest = (isPingRequest);

                IStringParamAction action = null;

                if (!isLocalRequest)
                {
                    StringScanner ss = new StringScanner(requestServiceName);

                    string targetPartName = ss.ExtractToken();
                    string targetServiceName = ss.Rest;

                    action = Interconnect.Parts.Parts.Instance.CreateServiceAction(targetPartName, targetServiceName, rsas.NamedParamValues, false);
                }

                RemoteServiceActionUpdate rsau = rsauFreeList.Get();
                rsau.ActionUUID = rsas.ActionUUID;

                if (action != null)
                {
                    actionTracking = sacftFreeList.Get();
                    actionTracking.ActionUUID = rsas.ActionUUID;
                    actionTracking.Action = action;
                    actionTracking.NotifyableParentPart = NotifyableParentPart;
                    actionTracking.Action.NotifyOnUpdate.AddItem(actionTracking);
                    actionTracking.sendUpdate = true;

                    uuidToServiceActionClientFacetTrackingDictionary[rsas.ActionUUID] = actionTracking;
                    workingServiceActionClientFacetTrackingArray = null;

                    actionTracking.Action.Start();

                    rsau.ActionState = new ActionStateCopy(action.ActionState);
                }
                else if (isPingRequest)
                {
                    NamedValueSet pingResponseNamedValues = new NamedValueSet();

                    if (requestNamedParamValues["GetSummary"].VC.GetValue<bool ?>(false).GetValueOrDefault())
                    {
                        pingResponseNamedValues.SetValue("IsServer", new ValueContainer(IsServer))
                                                .SetValue("State", new ValueContainer(State))
                                                ;
                    }

                    rsau.ActionState = new ActionStateCopy(ActionStateCode.Complete, String.Empty, pingResponseNamedValues);
                }
                else
                {
                    // send an immediate failure message...
                    rsau.ActionState = new ActionStateCopy(ActionStateCode.Complete, "Cannot perform Service Request '{0}': target PartID is not known or local request was not recognized".CheckedFormat(requestServiceName), null);
                }

                EnqueuePendingRSAU(rsau);
            }
            else if (actionTracking != null && rsas.RequestCancel)
            {
                actionTracking.Action.RequestCancel();
            }
            else
            {
                Logger.Debug.Emit("RemoteServiceActionRequest for uuid:{0} not understood or redudant [Ignored]", rsas.ActionUUID);
            }
        }

        /// <summary>
        /// Enqueues the given rsau in either the pendingRemoteServiceActionUpdateCompleteQueue or the pendingRemoteServiceActionUpdateQueue depending on whether the 
        /// rsau's ActionState IsComplete, or not.
        /// </summary>
        private void EnqueuePendingRSAU(RemoteServiceActionUpdate rsau)
        {
            if (rsau.ActionState.IsComplete)
                pendingRSAUCompleteQueue.Enqueue(rsau);
            else
                pendingRSAUQueue.Enqueue(rsau);
        }

        private void ServiceClientFacetTracking()
        {
            if (workingServiceActionClientFacetTrackingArray == null)
                workingServiceActionClientFacetTrackingArray = uuidToServiceActionClientFacetTrackingDictionary.Values.ToArray();

            bool removedAny = false;
            int arrayLength = workingServiceActionClientFacetTrackingArray.Length;
            for (int idx = 0; idx < arrayLength; idx++)
            {
                ServiceActionClientFacetTracking actionTracking = workingServiceActionClientFacetTrackingArray[idx];

                if (actionTracking.sendUpdate)
                {
                    actionTracking.sendUpdate = false;

                    RemoteServiceActionUpdate rsau = rsauFreeList.Get();

                    rsau.ActionUUID = actionTracking.ActionUUID;
                    rsau.ActionState = new ActionStateCopy(actionTracking.Action.ActionState);

                    EnqueuePendingRSAU(rsau);

                    if (rsau.ActionState.IsComplete)
                    {
                        // remove actionTracker objects for completed actions.
                        uuidToServiceActionClientFacetTrackingDictionary.Remove(actionTracking.ActionUUID);
                        sacftFreeList.Release(ref actionTracking);
                        removedAny = true;      // will cause the working array to be nulled/reset (and rebuilt on the next service call).
                    }
                }
            }

            if (removedAny)
                workingServiceActionClientFacetTrackingArray = null;
        }

        private ServiceActionClientFacetTracking[] workingServiceActionClientFacetTrackingArray = null;
        private Dictionary<string, ServiceActionClientFacetTracking> uuidToServiceActionClientFacetTrackingDictionary = new Dictionary<string, ServiceActionClientFacetTracking>();

        #endregion

        #region Pending vpi list, pending rsas and rsau queues, sent ppItem tracker list, ack and seq num tracking fields

        /// <summary>this is the list of the last set of VPI's that have been generated by scanning the IVI but which have not been assigned to a VPIList and posted in a PushParameter object.</summary>
        private Queue<ValuePropagationItem> pendingVPIQueue = new Queue<ValuePropagationItem>();

        /// <summary>Predicate returns true if the pendingVPIQueue.Count is zero</summary>
        private bool PendingVPIQueueIsEmpty { get { return (pendingVPIQueue.Count == 0); } }

        /// <summary>this is the list of pending RemoteServiceActionRequest that need to be sent</summary>
        private Queue<RemoteServiceActionRequest> pendingRSARQueue = new Queue<RemoteServiceActionRequest>();

        /// <summary>Predicate returns true if the pendingRemoteServiceActionRequestQueue.Count is zero</summary>
        private bool PendingRSARQueueIsEmpty { get { return (pendingRSARQueue.Count == 0); } }

        /// <summary>This is the list of pending RemoteServiceActionUpdates that need to be sent</summary>
        private Queue<RemoteServiceActionUpdate> pendingRSAUQueue = new Queue<RemoteServiceActionUpdate>();

        /// <summary>Predicate returns true if the pendingRemoteServiceActionUpdateQueue.Count is zero</summary>
        private bool PendingRSAUQueueIsEmpty { get { return (pendingRSAUQueue.Count == 0); } }

        /// <summary>This is the list of pending RemoteServiceActionUpdates that need to be sent</summary>
        private Queue<RemoteServiceActionUpdate> pendingRSAUCompleteQueue = new Queue<RemoteServiceActionUpdate>();

        /// <summary>Predicate returns true if the pendingRemoteServiceActionUpdateCompleteQueue.Count is zero</summary>
        private bool PendingRSAUCompleteQueueIsEmpty { get { return (pendingRSAUCompleteQueue.Count == 0); } }

        /// <summary>This is a queue of all of the PushedPPItemTrackers that track ppItems that have been pushed to the client but which have not been acknowledged yet.</summary>
        private Queue<PushedPPItemTracker> pendingPushedPPItemTrackerQueue = new Queue<PushedPPItemTracker>();

        /// <summary>
        /// Returns true if the pendingPushedPPItemTrackerQueue either has at least MaxPendingPushCount items in it, or its total EstimatedContentSizeInBytes is at least MaxPendingEstimatedPushedByteCount
        /// </summary>
        private bool IsPendingPushedPPItemTrackerQueueLimitReached
        {
            get
            {
                if (pendingPushedPPItemTrackerQueue.Count == 0)
                    return false;
                if (pendingPushedPPItemTrackerQueue.Count >= MaxPendingPushCount)
                    return true;
                if (pendingPushedPPItemTrackerQueue.Sum((qItem) => (qItem.PPItem.EstimatedContentSizeInBytes)) >= MaxPendingEstimatedPushedByteCount)
                    return true;

                return false;
            }
        }

        /// <summary>This method checks if the pendingPushedPPItemTrackerQueue is empty, or not, and either returns the Peek of the first item in the queue or null when the queue is empty.</summary>
        private PushedPPItemTracker PeekFirstPendingPushedPPItemInQueue()
        {
            PushedPPItemTracker ppItemTracker = ((pendingPushedPPItemTrackerQueue.Count > 0) ? pendingPushedPPItemTrackerQueue.Peek() : null);
            return ppItemTracker;
        }

        /// <summary>This class contains the information used to track if a given PushParameter has been processed and acknoweldged by the remote end of this connection.</summary>
        private class PushedPPItemTracker
        {
            /// <summary>Gives the PPItem that is being tracked using this object.  This allows the PPItem to be released once the expected acknowledgement has been received.</summary>
            public PushParameter PPItem { get; set; }

            /// <summary>This gives the TimeStamp taken right after the correpsonding connection Push call completed.  The Age of this timestamp is used to determine if the corresponding acknowledgement has been received in time.</summary>
            public QpcTimeStamp TimeStamp { get; set; }

            /// <summary>Method used when returning a PushedPPItemTracker object to its free list.</summary>
            public void Clear()
            {
                PPItem = null;
                TimeStamp = QpcTimeStamp.Zero;
            }
        }

        /// <summary>This value is used with th elastPushedAckSeqNum to know when this end has processed an PushParameter but has not otherwise sent an Ack for it and thus needs to.</summary>
        private Int32 lastSuccessfullyProcessedPushParameterSeqNum = 0;

        /// <summary>
        /// This value gives the AckSeqNum that was was last pushed (in the main Service method).  
        /// This allows the connection to detect when it needs to generate an AckOnly PushParameter Item and push it in order to let the other side know that
        /// it was received even in the absence of other outgoing traffic.
        /// </summary>
        private Int32 lastPushedAckSeqNum = 0;

        /// <summary>
        /// This value gives the last AckSeqNum value that was processed from an incomming PushParameter Item using the HandlePPItemAckSeqNum method.
        /// This allows the HandlePPItemAckSeqNum method to handle cases where the incoming AckSeqNum value skips over some values so that it can process such gaps as 
        /// indicating that all SeqNum values in the gap are treated has having been acknowledged.
        /// </summary>
        private Int32 lastProcessedIncommingAckSeqNum = 0;

        /// <summary>This method generates a new outgoing SeqNum value and returns it.  Returned values do not include zero (as they are generated using AtomicInt32.IncrementSkipZero).</summary>
        private Int32 GetNextSeqNum() { return seqNumGen.IncrementSkipZero(); }

        /// <summary>internal object used to generate outgoing SeqNum values.</summary>
        private AtomicInt32 seqNumGen = new AtomicInt32();

        /// <summary>
        /// This field contains the timestamp taken the last time that this connection's service loop processed one or more newly received PPItems or 
        /// that it successfully pushed a PPItem into its side of the connection.  This allows the helper to determine how long it has been idle.
        /// (which is the same as meaning that it has neither sent nor received something).
        /// </summary>
        private QpcTimeStamp lastConnectionActivityTimeStamp = QpcTimeStamp.Now;

        /// <summary>This public property gives the amount of time that has elpased since the connection was last used to send or receive something.</summary>
        private TimeSpan ConnectionIdleAge { get { return lastConnectionActivityTimeStamp.Age; } }

        #endregion

        #region Connetion Abort handling.

        /// <summary>Returns true if the current AbortReason is no null or empty.  Generally this indicates that the connection is being aborted.</summary>
        public bool IsAbortRequested { get { return !AbortReason.IsNullOrEmpty(); } }

        /// <summary>
        /// Property is non-null if the connection helper has detected a reason that should cause the connection to be closed/aborted.
        /// </summary>
        public string AbortReason
        {
            get
            {
                return abortReason;
            }
            private set
            {
                if (!value.IsNullOrEmpty())
                {
                    if (abortReason.IsNullOrEmpty())
                    {
                        Logger.Debug.Emit("Connection abort requested '{0}'", value);
                        abortReason = value;
                    }
                    else if (abortReason != value)
                    {
                        Logger.Debug.Emit("Connection reported additional abort reason '{0}' [retaining original one:'{1}']", value, abortReason);
                    }
                }
            }
        }

        /// <summary>Internal backing store for hte AbortReason get/set property.</summary>
        private string abortReason = null;

        #endregion

        #region FreeLists and Release methods

        /// <summary>
        /// This is the Release helper method for PushedPPItemTracker objects.
        /// It Releases any PushParamter that this tracker contains and then it Releases the tracker onto the corresponding free list and
        /// sets the caller's ppItemTracker parameter to null to indicate that it has been released and cannot be used further by the caller.
        /// </summary>
        private void Release(ref PushedPPItemTracker ppItemTracker)
        {
            PushParameter ppItem = ppItemTracker.PPItem;
            ppItemTracker.PPItem = null;

            Release(ref ppItem);

            pushedPPItemTrackerFreeList.Release(ref ppItemTracker);
        }

        /// <summary>
        /// This is the Release helper method for PushParameter objects.  
        /// This method Clears and Releases any of the sub-objects that this item referes to (ValuePropragationItemList and contained ValuePropagationItems).
        /// It also Clears the given ppItem and sets the caller's ppItem parameter to null to indicate that it has been released and cannot be used further
        /// by the caller.
        /// </summary>
        private void Release(ref PushParameter ppItem)
        {
            ValuePropagationItemList vpiList = null;
            RemoteServiceActionRequestList rsasList = null;
            RemoteServiceActionUpdateList rsauList = null;

            if (ppItem != null)
            {
                vpiList = ppItem.VPIList;
                rsasList = ppItem.RSARList;
                rsauList = ppItem.RSAUList;

                ppItem.VPIList = null;
                ppItem.RSARList = null;
                ppItem.RSAUList = null;
            }

            ppFreeList.Release(ref ppItem);

            if (vpiList != null)
            {
                ReleaseListContents(vpiList, vpiFreeList);
                vpilFreeList.Release(ref vpiList);
            }

            if (rsasList != null)
            {
                ReleaseListContents(rsasList, rsarFreeList);
                rsarlFreeList.Release(ref rsasList);
            }

            if (rsauList != null)
            {
                ReleaseListContents(rsauList, rsauFreeList);
                rsaulFreeList.Release(ref rsauList);
            }
        }

        /// <summary>
        /// Generic release helper method - encapsulates the logic used to shuffle used list items back to their own free lists for a variety of item types.
        /// </summary>
        private static void ReleaseListContents<TItemType>(List<TItemType> fromList, BasicFreeList<TItemType> intoFreeList)
            where TItemType : class
        {
            int listCount = fromList.Count;
            for (int idx = 0; idx < listCount; idx++)
            {
                TItemType item = fromList[idx];
                fromList[idx] = null;

                intoFreeList.Release(ref item);
            }
        }

        /// <summary>This is the BasicFreeList of Released PushedPPItemTracker objects.  Each connection will keep of to 30 of these in their Cleared state.</summary>
        private BasicFreeList<PushedPPItemTracker> pushedPPItemTrackerFreeList = new BasicFreeList<PushedPPItemTracker>() { MaxItemsToKeep = 30, FactoryDelegate = () => new PushedPPItemTracker(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released PushParameter items.  Each connection will keep up to 30 of these in their Cleared state.</summary>
        private BasicFreeList<PushParameter> ppFreeList = new BasicFreeList<PushParameter>() { MaxItemsToKeep = 30, FactoryDelegate = () => new PushParameter(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released ValuePropagationItemLists.  Each connection will keep up to 30 of these in their Cleared/empty state.</summary>
        private BasicFreeList<ValuePropagationItemList> vpilFreeList = new BasicFreeList<ValuePropagationItemList>() { MaxItemsToKeep = 30, FactoryDelegate = () => new ValuePropagationItemList(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released ValuePropagationItems.  Each connection will keep up to 5000 of these items in their Cleared state.</summary>
        private BasicFreeList<ValuePropagationItem> vpiFreeList = new BasicFreeList<ValuePropagationItem>() { MaxItemsToKeep = 5000, FactoryDelegate = () => new ValuePropagationItem(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released RemoteServiceActionRequestList.  Each connection will keep up to 30 of these in their Cleared/empty state.</summary>
        private BasicFreeList<RemoteServiceActionRequestList> rsarlFreeList = new BasicFreeList<RemoteServiceActionRequestList>() { MaxItemsToKeep = 30, FactoryDelegate = () => new RemoteServiceActionRequestList(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released RemoteServiceActionRequest.  Each connection will keep up to 5000 of these items in their Cleared state.</summary>
        private BasicFreeList<RemoteServiceActionRequest> rsarFreeList = new BasicFreeList<RemoteServiceActionRequest>() { MaxItemsToKeep = 100, FactoryDelegate = () => new RemoteServiceActionRequest(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released RemoteServiceActionRequestList.  Each connection will keep up to 30 of these in their Cleared/empty state.</summary>
        private BasicFreeList<RemoteServiceActionUpdateList> rsaulFreeList = new BasicFreeList<RemoteServiceActionUpdateList>() { MaxItemsToKeep = 30, FactoryDelegate = () => new RemoteServiceActionUpdateList(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released ValuePropagationItems.  Each connection will keep up to 5000 of these items in their Cleared state.</summary>
        private BasicFreeList<RemoteServiceActionUpdate> rsauFreeList = new BasicFreeList<RemoteServiceActionUpdate>() { MaxItemsToKeep = 500, FactoryDelegate = () => new RemoteServiceActionUpdate(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released ServiceActionProviderFacetTracking.  Each connection will keep up to 30 of these in their Cleared/empty state.</summary>
        private BasicFreeList<ServiceActionProviderFacetTracking> sapftFreeList = new BasicFreeList<ServiceActionProviderFacetTracking>() { MaxItemsToKeep = 30, FactoryDelegate = () => new ServiceActionProviderFacetTracking(), ClearDelegate = (item) => item.Clear() };

        /// <summary>This is the BasicFreeList of Released ServiceActionProviderFacetTracking.  Each connection will keep up to 30 of these in their Cleared/empty state.</summary>
        private BasicFreeList<ServiceActionClientFacetTracking> sacftFreeList = new BasicFreeList<ServiceActionClientFacetTracking>() { MaxItemsToKeep = 30, FactoryDelegate = () => new ServiceActionClientFacetTracking(), ClearDelegate = (item) => item.Clear() };

        #endregion
    }

    #endregion
}

//-------------------------------------------------------------------
