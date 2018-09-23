//-------------------------------------------------------------------
/*! @file Interconnect/Remoting/MessageStreamTools.cs
 *  @brief Common Message related definitions for Modular.Interconnect.Remoting.StreamTools
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Remoting.Buffers;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Modular.Common.CustomSerialization;

// Please note: see comments in for MosaicLib.Modular.Interconnect.Remoting in Remoting.cs

namespace MosaicLib.Modular.Interconnect.Remoting.MessageStreamTools
{
    #region IMessageStreamTool

    public interface IMessageStreamTool : IServiceable
    {
        void ResetState(QpcTimeStamp qpcTimeStamp, ResetType resetType, string reason = null);
        void HandleInboundMessage(QpcTimeStamp qpcTimeStamp, Messages.Message mesg);
        Messages.Message ServiceAndGenerateNextMessageToSend(QpcTimeStamp qpcTimeStamp);
    }

    /// <summary>
    /// This enum defines the different types of Resets being done to a message stream tool.  
    /// <para/>None (0), ClientConstruction, ServerConstruction, MessageDeliveryFailure
    /// </summary>
    public enum ResetType : int
    {
        None = 0,
        ClientConstruction,
        ServerConstruction,
        ClientMessageDeliveryFailure,
        ServerMessageDeliveryFailure,
        ClientSessionLost,
        ServerSessionLost,
        ClientSessionClosed,
        ServerSessionClosed,
        ServerSessionTerminated,
        ClientReleased,
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given <paramref name="resetType"/> is ClientConstruction or ServerConstruction (based on given values of <paramref name="includeClientTypes"/> and <paramref name="includeServerTypes"/>)
        /// </summary>
        public static bool IsConstruction(this ResetType resetType, bool includeClientTypes = true, bool includeServerTypes = true)
        {
            return (includeClientTypes && resetType == ResetType.ClientConstruction)
                    || (includeServerTypes && resetType == ResetType.ServerConstruction);
        }

        /// <summary>
        /// Returns true if the given <paramref name="resetType"/> is ClientSessionClosed or ServerSessionClosed (based on given values of <paramref name="includeClientTypes"/> and <paramref name="includeServerTypes"/>)
        /// </summary>
        public static bool IsClose(this ResetType resetType, bool includeClientTypes = true, bool includeServerTypes = true)
        {
            return (includeClientTypes && resetType == ResetType.ClientSessionClosed)
                    || (includeServerTypes && (resetType == ResetType.ServerSessionClosed || resetType == ResetType.ServerSessionTerminated));
        }

        /// <summary>
        /// Returns true if the given <paramref name="resetType"/> is a failure code (based on given values of <paramref name="includeClientTypes"/> and <paramref name="includeServerTypes"/>)
        /// </summary>
        public static bool IsFailure(this ResetType resetType, bool includeClientTypes = true, bool includeServerTypes = true)
        {
            return (includeClientTypes && (resetType == ResetType.ClientSessionLost || resetType == ResetType.ClientMessageDeliveryFailure))
                    || (includeServerTypes && (resetType == ResetType.ServerSessionLost || resetType == ResetType.ServerMessageDeliveryFailure));
        }

        /// <summary>
        /// Returns true if the given <paramref name="resetType"/> is ClientSessionClosed
        /// </summary>
        public static bool IsClientClose(this ResetType resetType)
        {
            return resetType.IsClose(includeClientTypes: true, includeServerTypes: false);
        }

        /// <summary>
        /// Returns true if the given <paramref name="resetType"/> is a client failure code (ClientMessageDeliveryFailure, ClientSessionLost, ClientSessionTimedOut)
        /// </summary>
        public static bool IsClientFailure(this ResetType resetType)
        {
            return resetType.IsFailure(includeClientTypes: true, includeServerTypes: false);
        }
    }

    #endregion

    #region MessageStreamToolConfigBase

    /// <summary>Base class for configuration (and selection) objects used to create various types of remoting stream tools.</summary>
    public abstract class MessageStreamToolConfigBase : ICopyable<MessageStreamToolConfigBase>
    {
        public virtual NamedValueSet AddValues(NamedValueSet nvs) 
        {
            nvs = nvs.ConvertToWritable();

            nvs.SetValue("ToolTypeStr", ToolTypeStr);

            return nvs; 
        }

        public virtual MessageStreamToolConfigBase ApplyValues(INamedValueSet nvs) 
        { 
            return this; 
        }

        public abstract MessageStreamToolConfigBase MakeCopyOfThis(bool deepCopy = true);

        public abstract string ToolTypeStr { get; }
    }

    #endregion

    #region ActionRelayMessageStreamToolConfig, IActionRelayMessageStreamTool

    /// <summary>
    /// This object is used to select remoting message stream to be used to relay actions that are delivered to a Remoting client part over the remoting connection
    /// At most one stream can be configured for action relay use.
    /// </summary>
    public class ActionRelayMessageStreamToolConfig : MessageStreamToolConfigBase
    {
        public static readonly string toolTypeStr = "ActionRelay";
        public override string ToolTypeStr { get { return toolTypeStr; } }

        public Parts.IPartsInterconnection LocalIPartsInterconnection { get; set; }

        public override MessageStreamToolConfigBase MakeCopyOfThis(bool deepCopy = true) { return (MessageStreamToolConfigBase)this.MemberwiseClone(); }
    }

    /// <summary>This intercace allows a Remoting part to make use of an Action Relay Stream Tool to relay actions that are give to the part to the other end.</summary>
    public interface IActionRelayMessageStreamTool
    {
        /// <summary>Consumes, and relays, the given <paramref name="action"/>'s <paramref name="requestStr"/> and NamedParamValues to the other end and relays all action state udpates back to this end until the action is complete.</summary>
        void RelayServiceAction(string requestStr, IProviderFacet action);
    }

    #endregion

    #region SetRelayMessageStreamToolConfigBase, SetRelayMessageStreamToolConfig<TSetItemType>

    /// <summary>
    /// This is an intermediate base class used to support creation of templatized SetRelayMessageStreamToolConfig objects.
    /// </summary>
    public class SetRelayMessageStreamToolConfigBase : MessageStreamToolConfigBase
    {
        public static readonly string toolTypeStr = "SetRelay";
        public override string ToolTypeStr { get { return toolTypeStr; } }

        public Sets.ISetsInterconnection LocalISetsInstance { get; set; }
        public Sets.SetID SetID { get; set; }
        public int MaximumItemsPerMessage { get; set; }
        public TimeSpan MinimumUpdateInterval { get; set; }
        internal virtual string SetItemTypeStr { get; set; }
        public bool ClearClientSetOnCloseOrFailure { get; set; }

        public override NamedValueSet AddValues(NamedValueSet nvs) 
        {
            nvs = base.AddValues(nvs).ConvertToWritable();

            Sets.SetID setID = SetID ?? Sets.SetID.Empty;

            nvs.SetValue("SetID.Name", setID.Name);
            nvs.SetValue("SetID.UUID", setID.UUID);
            nvs.ConditionalSetValue("MaximumItemsPerMessage", MaximumItemsPerMessage != 0, MaximumItemsPerMessage);
            nvs.ConditionalSetValue("MinimumUpdateInterval", MinimumUpdateInterval != TimeSpan.Zero, MinimumUpdateInterval);
            nvs.SetValue("SetItemTypeStr", SetItemTypeStr);
            nvs.ConditionalSetKeyword("ClearClientSetOnCloseOrFailure", ClearClientSetOnCloseOrFailure);

            return nvs;
        }

        public override MessageStreamToolConfigBase ApplyValues(INamedValueSet nvs) 
        {
            nvs = nvs ?? NamedValueSet.Empty;

            SetID = new Sets.SetID(nvs["SetID.Name"].VC.GetValue<string>(rethrow: false), nvs["SetID.UUID"].VC.GetValue<string>(rethrow: false).MapEmptyTo());
            MaximumItemsPerMessage = nvs["MaximumItemsPerMessage"].VC.GetValue<int>(rethrow: false);
            MinimumUpdateInterval = nvs["MinimumUpdateInterval"].VC.GetValue<TimeSpan>(rethrow: false);
            SetItemTypeStr = nvs["SetItemTypeStr"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
            ClearClientSetOnCloseOrFailure = nvs.Contains("ClearClientSetOnCloseOrFailure");

            return this;
        }

        public override MessageStreamToolConfigBase MakeCopyOfThis(bool deepCopy = true) { return (MessageStreamToolConfigBase)this.MemberwiseClone(); }

        internal virtual Sets.ITrackingSet CreateReferenceTrackingSet() { throw new System.NotImplementedException(); }
    }

    /// <summary>
    /// This object is used to select and configure a remoting message stream to be used to relay changes from a remote registered ITrackable set into a locally created (and registered) ITrackable set
    /// </summary>
    public class SetRelayMessageStreamToolConfig<TSetItemType> : SetRelayMessageStreamToolConfigBase
    {
        internal override String SetItemTypeStr { get { return typeof(TSetItemType).ToString(); } set { } }

        public override MessageStreamToolConfigBase MakeCopyOfThis(bool deepCopy = true) { return (MessageStreamToolConfigBase)this.MemberwiseClone(); }

        internal override Sets.ITrackingSet CreateReferenceTrackingSet() { return new Sets.TrackingSet<TSetItemType>(SetID, Sets.SetType.Tracking, registerSelfWithSetsInstance: (LocalISetsInstance ?? Sets.Sets.Instance), createMutex: true); }
    }

    #endregion

    #region ConfigProxyProviderMessageStreamToolConfig

#if (false)
    /// <summary>
    /// This object is used to select and configure a remoting message stream to be used to register a proxy provoider (of the given name) with the given IConfig instance and to use it to support relaying config keys and activity
    /// from the remote server's IConfig instance back to the locally added provider.  
    /// </summary>
    public class ConfigProxyProviderMessageStreamToolConfig : MessageStreamToolConfigBase
    {
        public static readonly string toolTypeStr = "ConfigProxyProvider";
        public override string ToolTypeStr { get { return toolTypeStr; } }

        public IConfig LocalIConfigToAddProviderTo { get; set; }
        public string ProviderName { get; set; }
        public INamedValueSet KeyPrefixAndConversionFilterNVS { get; set; }
        public INamedValueSet MetaDataFilterNVS { get; set; }

        public override MessageStreamToolConfigBase MakeCopyOfThis(bool deepCopy = true) 
        {
            return new ConfigProxyProviderMessageStreamToolConfig() 
            {
                LocalIConfigToAddProviderTo = LocalIConfigToAddProviderTo,
                ProviderName = ProviderName,
                KeyPrefixAndConversionFilterNVS = KeyPrefixAndConversionFilterNVS.ConvertToReadOnly(mapNullToEmpty: true), 
                MetaDataFilterNVS = MetaDataFilterNVS.ConvertToReadOnly(mapNullToEmpty: true),
            };
        }
    }
#endif

    #endregion

    #region IVIRelayMessageStreamToolConfig

    /// <summary>
    /// This object is used to select and configure a remoting message stream to be used to cyclicly transfer selected contents between a remote IVI (name may be specified explicitly if registered) and the local one.
    /// This object may specify the direction of transfer, name filtering and conversion information, and meta data filtering criteria.
    /// </summary>
    public class IVIRelayMessageStreamToolConfig : MessageStreamToolConfigBase
    {
        public static readonly string toolTypeStr = "IVIRelay";
        public override string ToolTypeStr { get { return toolTypeStr; } }

        public Values.IValuesInterconnection ClientIVI { get; set; }

        public string RemoteIVIName { get; set; }
        public IVIRelayDirection IVIRelayDirection { get; set; }
        public TimeSpan MinimumUpdateInterval { get; set; }
        public int MaxItemsPerMessage { get; set; }
        public string ServerToClientFromNamePrefix { get; set; }
        public string ServerToClientToNamePrefix { get; set; }
        public INamedValueSet ServerToClientMetaDataFilterNVS { get; set; }
        public INamedValueSet ClientToServerMetaDataFilterNVS { get; set; }
        public bool ResetClientSideIVAsOnCloseOrFailure { get; set; }

        public override NamedValueSet AddValues(NamedValueSet nvs)
        {
            nvs = base.AddValues(nvs).ConvertToWritable();

            nvs.ConditionalSetValue("RemoteIVIName", !RemoteIVIName.IsNullOrEmpty(), RemoteIVIName);
            nvs.SetValue("IVIRelayDirection", IVIRelayDirection);
            nvs.ConditionalSetValue("MinimumUpdateInterval", !MinimumUpdateInterval.IsZero(), MinimumUpdateInterval);
            nvs.ConditionalSetValue("MaxItemsPerMessage", MaxItemsPerMessage != 0, MaxItemsPerMessage);
            nvs.ConditionalSetValue("ServerToClientFromNamePrefix", !ServerToClientFromNamePrefix.IsNullOrEmpty(), ServerToClientFromNamePrefix);
            nvs.ConditionalSetValue("ServerToClientToNamePrefix", !ServerToClientToNamePrefix.IsNullOrEmpty(), ServerToClientToNamePrefix);
            nvs.ConditionalSetValue("ServerToClientMetaDataFilterNVS", !ServerToClientMetaDataFilterNVS.IsNullOrEmpty(), ServerToClientMetaDataFilterNVS);
            nvs.ConditionalSetValue("ClientToServerMetaDataFilterNVS", !ClientToServerMetaDataFilterNVS.IsNullOrEmpty(), ClientToServerMetaDataFilterNVS);
            nvs.ConditionalSetKeyword("ResetClientSideIVAsOnCloseOrFailure", ResetClientSideIVAsOnCloseOrFailure);

            return nvs;
        }

        public override MessageStreamToolConfigBase ApplyValues(INamedValueSet nvs)
        {
            nvs = nvs ?? NamedValueSet.Empty;

            RemoteIVIName = nvs["RemoteIVIName"].VC.GetValue<string>(rethrow: false);
            IVIRelayDirection = nvs["IVIRelayDirection"].VC.GetValue<IVIRelayDirection>(rethrow: false);
            MinimumUpdateInterval = nvs["MinimumUpdateInterval"].VC.GetValue<TimeSpan>(rethrow: false);
            MaxItemsPerMessage = nvs["MaxItemsPerMessage"].VC.GetValue<int>(rethrow: false);
            ServerToClientFromNamePrefix = nvs["ServerToClientFromNamePrefix"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
            ServerToClientToNamePrefix = nvs["ServerToClientToNamePrefix"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
            ServerToClientMetaDataFilterNVS = nvs["ServerToClientMetaDataFilterNVS"].VC.GetValue<INamedValueSet>(rethrow: false);
            ClientToServerMetaDataFilterNVS = nvs["ClientToServerMetaDataFilterNVS"].VC.GetValue<INamedValueSet>(rethrow: false);
            ResetClientSideIVAsOnCloseOrFailure = nvs.Contains("ResetClientSideIVAsOnCloseOrFailure");

            return this;
        }

        public override MessageStreamToolConfigBase MakeCopyOfThis(bool deepCopy = true)
        {
            return new IVIRelayMessageStreamToolConfig()
            {
                ClientIVI = ClientIVI,
                RemoteIVIName = RemoteIVIName,
                IVIRelayDirection = IVIRelayDirection,
                ServerToClientFromNamePrefix = ServerToClientFromNamePrefix,
                ServerToClientToNamePrefix = ServerToClientToNamePrefix,
                ServerToClientMetaDataFilterNVS = ServerToClientMetaDataFilterNVS.ConvertToReadOnly(mapNullToEmpty: true),
                ClientToServerMetaDataFilterNVS = ClientToServerMetaDataFilterNVS.ConvertToReadOnly(mapNullToEmpty: true),
            };
        }
    }

    /// <summary>
    /// Defines the direction of IVA propagation for an IVIRelay
    /// <para/>FromServer (0 - default), ToServer, Bidirectional,
    /// </summary>
    public enum IVIRelayDirection : int
    {
        /// <summary>IVA propagation is from the Server to the Client</summary>
        FromServer = 0,

        /// <summary>IVA propagation is from the Client to the Server</summary>
        ToServer,

        /// <summary>IVA propagation is from the Server to the Client and from the Client to the Server.  Client to Server registration only starts after first pass of Server to Client registration has been performed.</summary>
        Bidirectional,
    }

    #endregion

    #region InitialMessageStreamTool

    /// <summary>
    /// This message stream tool is used for message based communications between the client and the server.  
    /// Initially it is used to carry the ServerInfoNVS from the server to the client on session setup.
    /// </summary>
    public class BaseMessageStreamTool : MessageStreamToolBase, IMessageStreamTool
    {
        #region Construction and related fields/properties

        /// <summary>
        /// Client construction
        /// </summary>
        public BaseMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool)
            : base("Base", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: true)
        {
            ResetState(QpcTimeStamp.Now, ResetType.ClientConstruction);
        }

        /// <summary>
        /// Constructor used by RemotingServer
        /// </summary>
        public BaseMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, Messages.Message streamSetupMessage, INamedValueSet streamSetupMessageNVS)
            : base("Base", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: false)
        {
            ResetState(QpcTimeStamp.Now, ResetType.ServerConstruction);
        }

        /// <summary>
        /// This property is set by the RemotingServer on the server side and is set by this instance on the client side.  
        /// On the server side this tool monitors this property for changes and will re-send its contents as needed on any change.
        /// </summary>
        public INamedValueSet ServerInfoNVS { get {return _serverInfoNVS; } set { _serverInfoNVS = value.ConvertToReadOnly(); _serverInfoNVSSendPending = true; } }
        private NamedValueSet _serverInfoNVS = null;
        private bool _serverInfoNVSSendPending = false;

        /// <summary>
        /// This property is generally set by the RemotingClient on the client side.  When a new ServerInfoNVS is received, this delegate will be passed the received value, if the delegate is non-null
        /// </summary>
        public Action<INamedValueSet> HandleNewServerInfoNVSDelegate { get; set; }

        #endregion

        #region MessageStreamToolBase implementation

        public override void InnerHandleResetState(QpcTimeStamp qpcTimeStamp, ResetType resetType, string reason)
        { }

        #endregion

        #region Config

        public class Config : MessageStreamToolConfigBase
        {
            public static readonly string toolTypeStr = "Base";
            public override string ToolTypeStr { get { return toolTypeStr; } }

            public override MessageStreamToolConfigBase MakeCopyOfThis(bool deepCopy = true) { return (MessageStreamToolConfigBase)this.MemberwiseClone(); }
        }

        #endregion

        #region Receiver (HandleInboundMessage)

        public void HandleInboundMessage(QpcTimeStamp qpcTimeStamp, Messages.Message mesg)
        {
            try
            {
                using (var mesgIStream = mesg.MessageReadingStream)
                {
                    PushItem pushItem = pushItemDCA.ReadObject(mesgIStream);

                    if (pushItem.ServerInfoNVS != null)
                    {
                        _serverInfoNVS = pushItem.ServerInfoNVS.ConvertToReadOnly();

                        logger.Debug.EmitWith("Received ServerInfoNVS", nvs: _serverInfoNVS);

                        Action<INamedValueSet> handleNewServerInfoNVSDelegate = HandleNewServerInfoNVSDelegate;
                        if (handleNewServerInfoNVSDelegate != null)
                            HandleNewServerInfoNVSDelegate(_serverInfoNVS);
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger.Debug.Emit("{0}: encountered unexpected exception while generating message: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
        }
        #endregion

        #region Transmitter (ServiceAndGenerateNextMessageToSend)

        public Messages.Message ServiceAndGenerateNextMessageToSend(QpcTimeStamp qpcTimeStamp)
        {
            Service(qpcTimeStamp);

            Messages.Message mesg = null;

            if (sendSetupMessage)
            {
                logger.Trace.Emit("Sending setup message");

                mesg = new Messages.Message(BufferPool).Update(buildPayloadDataFromNVS: new Config().AddValues(null), orInFlags: BufferHeaderFlags.MessageContainsStreamSetup);
                sendSetupMessage = false;
            }
            else
            {
                try
                {
                    if (_serverInfoNVSSendPending && isServerSide)
                    {
                        logger.Trace.EmitWith("Sending ServerInfoNVS", nvs: _serverInfoNVS);

                        PushItem pushItem = new PushItem()
                        {
                            ServerInfoNVS = _serverInfoNVS,
                        };

                        mesg = new Messages.Message(BufferPool);

                        using (var mesgOStream = mesg.MessageBuildingStream)
                        {
                            pushItemDCA.WriteObject(pushItem, mesgOStream);
                        }

                        _serverInfoNVSSendPending = false;
                    }
                }
                catch (System.Exception ex)
                {
                    logger.Debug.Emit("{0}: encountered unexpected exception while generating message: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    mesg = null; // discard any partial message we were building
                }
            }

            return mesg;
        }

        #endregion

        #region Serialization (pushItemDCA and PushItem class)

        IDataContractAdapter<PushItem> pushItemDCA = new DataContractJsonAdapter<PushItem>();

        [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
        public class PushItem
        {
            [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
            public NamedValueSet ServerInfoNVS { get; set; }

            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
                if (ServerInfoNVS != null)
                    ServerInfoNVS.MakeReadOnly();
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                if (ServerInfoNVS != null)
                    sb.CheckedAppendFormatWithDelimiter(" ", "ServerInfoNVS:{0}", ServerInfoNVS.ToStringSML());

                if (sb.Length > 0)
                    return sb.ToString();

                return "[Empty]";
            }
        }

        #endregion
    }

    #endregion

    #region ActionRelayMessageStreamTool

    public class ActionRelayMessageStreamTool : MessageStreamToolBase, IMessageStreamTool, IActionRelayMessageStreamTool
    {
        /// <summary>
        /// This is the service name that will be interpreted as a ping, rather than as a target part name and sub-service request name
        /// <para/>"$WcfServicePing$"
        /// </summary>
        public readonly static string PingRequestServiceName = "$RemotingServicePing$";

        #region Construction and related fields

        /// <summary>
        /// Constructor used by RemotingClient
        /// </summary>
        public ActionRelayMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, ActionRelayMessageStreamToolConfig config)
            : base("ActionRelay", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: true)
        {
            Config = config;
            IPartsInterconnection = config.LocalIPartsInterconnection ?? Parts.Parts.Instance;

            ResetState(QpcTimeStamp.Now, ResetType.ClientConstruction);
        }

        /// <summary>
        /// Constructor used by RemotingServer
        /// </summary>
        public ActionRelayMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, Messages.Message streamSetupMessage, INamedValueSet streamSetupMessageNVS, Parts.IPartsInterconnection iPartsInterconnection)
            : base("ActionRelay", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: false)
        {
            (Config = new ActionRelayMessageStreamToolConfig()).ApplyValues(streamSetupMessageNVS);
            IPartsInterconnection = iPartsInterconnection ?? Parts.Parts.Instance;

            ResetState(QpcTimeStamp.Now, ResetType.ServerConstruction);
        }

        public ActionRelayMessageStreamToolConfig Config { get; private set; }
        public Parts.IPartsInterconnection IPartsInterconnection { get; private set; }

        #endregion

        #region ProviderActionTracker, ClientActionTracker and related dictionaries, arrays, ...

        public class ProviderActionTracker
        {
            public ulong actionID;
            public string requestStr;
            public INamedValueSet npv;
            public IProviderFacet ipf;
            public bool cancelHasBeenRequested;

            public PushItem pushItem;
            public RemoteServiceActionRequest request;
        }

        public class ClientActionTracker
        {
            public RemoteServiceActionRequest request;

            public ulong actionID;
            public IClientFacet icf;
            public IActionState lastActionState;

            public PushItem pushItem;

            public bool isComplete;
        }

        ulong providerFacetActionIDGen = 0;
        IDictionaryWithCachedArrays<ulong, ProviderActionTracker> providerFacetActionDictionary = new IDictionaryWithCachedArrays<ulong,ProviderActionTracker>();
        IDictionaryWithCachedArrays<ulong, ClientActionTracker> clientFacetActionDictionary = new IDictionaryWithCachedArrays<ulong,ClientActionTracker>();

        #endregion

        #region IActionRelayMessageStreamTool implementation

        /// <summary>Consumes, and relays, the given <paramref name="action"/>'s <paramref name="requestStr"/> and NamedParamValues to the other end and relays all action state udpates back to this end until the action is complete.</summary>
        public void RelayServiceAction(string requestStr, IProviderFacet action)
        {
            ulong actionID = ++providerFacetActionIDGen;
            NamedValueSet npv = action.NamedParamValues.ConvertToReadOnly(mapNullToEmpty: false).MapEmptyToNull();

            var request = new RemoteServiceActionRequest()
            {
                requestStr = requestStr,
                namedParamValues = npv,
            };

            var at = new ProviderActionTracker() 
            { 
                actionID = actionID,
                requestStr = requestStr,
                npv = npv,
                ipf = action, 
                pushItem = new PushItem()
                {
                    actionID = actionID,
                    itemType = PushItemType.Request,
                    request = request,
                    pending = true,
                },
                request = request,
            };

            providerFacetActionDictionary[actionID] = at;

            pendingPushItemList.Add(at.pushItem);
        }

        #endregion

        #region InnerHandleResetState

        public override void InnerHandleResetState(QpcTimeStamp qpcTimeStamp, ResetType resetType, string reason)
        {
            providerFacetActionDictionary.ValueArray.DoForEach(item => item.ipf.CompleteRequest("{0}: message stream state has been reset [{1}]".CheckedFormat(logger.Name, reason)));

            providerFacetActionDictionary.Clear();
            clientFacetActionDictionary.Clear();
        }

        #endregion

        #region Receiver: HandleInboundMessage, HandleInboundRequest, HandleInboundCancelRequest, HandleInboundUpdate, HandleInboundFailure

        const int copyBufferLength = 4096;
        byte[] copyBuffer = new byte[copyBufferLength];
        PushItem loopPushItem = new PushItem();

        public void HandleInboundMessage(QpcTimeStamp qpcTimeStamp, Messages.Message mesg)
        {
            try
            {
                using (var mesgIStream = mesg.MessageReadingStream)
                using (var mesgIByteReader = new System.IO.BinaryReader(mesgIStream, binaryEncoder))
                {
                    int pushCount = mesgIByteReader.ReadInt32();

                    PushItem pushItem = loopPushItem;

                    foreach (var idx in Enumerable.Range(0, pushCount))
                    {
                        pushItem.ReadPartsFrom(mesgIByteReader);

                        System.IO.MemoryStream ms = GetEmptyMemoryStreamIfNeeded();

                        if (pushItem.itemLength > 0)
                        {
                            for (int offset = 0; offset < pushItem.itemLength; )
                            {
                                int copyCount = Math.Min(pushItem.itemLength - offset, copyBuffer.Length);
                                mesgIStream.Read(copyBuffer, 0, copyCount);
                                ms.Write(copyBuffer, 0, copyCount);

                                offset += copyCount;
                            }

                            ms.Position = 0;
                        }

                        string requestFailureCode = null;
                        string clientFailureCode = null;

                        switch (pushItem.itemType)
                        {
                            case PushItemType.Request:
                                try
                                {
                                    requestFailureCode = HandleInboundRequest(pushItem.actionID, requestDCA.ReadObject(ms));
                                }
                                catch (System.Exception ex)
                                {
                                    requestFailureCode = ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace);
                                }
                                break;

                            case PushItemType.CancelRequest:
                                requestFailureCode = HandleInboundCancelRequest(pushItem.actionID);
                                break;

                            case PushItemType.Update:
                                try 
                                {
                                    clientFailureCode = HandleInboundUpdate(pushItem.actionID, actionStateDCA.ReadObject(ms));
                                }
                                catch (System.Exception ex)
                                {
                                    clientFailureCode = ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace);
                                }
                                break;

                            case PushItemType.Failure:
                                try
                                {
                                    clientFailureCode = stringDCA.ReadObject(ms);
                                }
                                catch (System.Exception ex)
                                {
                                    clientFailureCode = ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace);
                                }
                                break;

                            default:
                                requestFailureCode = "{0}: '{1}' is not supported here".CheckedFormat(Fcns.CurrentMethodName, pushItem);
                                break;
                        }

                        if (!requestFailureCode.IsNullOrEmpty())
                        {
                            logger.Debug.Emit("{0}: {1} failed: {2}", Fcns.CurrentMethodName, pushItem, requestFailureCode);

                            PushItem requestFailurePushBackItem = new PushItem() { itemType = PushItemType.Failure, actionID = pushItem.actionID, failureCode = requestFailureCode, pending = true };
                            pendingPushItemList.Add(requestFailurePushBackItem);
                        }

                        if (!clientFailureCode.IsNullOrEmpty())
                        {
                            if (pushItem.itemType != PushItemType.Failure)
                                logger.Debug.Emit("{0}: {1} failed: {2}", Fcns.CurrentMethodName, pushItem, clientFailureCode);
                            else
                                logger.Debug.Emit("{0}: {1} failed: {2}", Fcns.CurrentMethodName, pushItem, clientFailureCode);

                            HandleInboundFailure(pushItem.actionID, clientFailureCode);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger.Debug.Emit("{0}: encountered unexpected exception while generating message: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }

            // don't hold on to really large item buffers
            ClearMemoryStreamIfNeeded(shortenIfNeeded: true);
        }

        private string HandleInboundRequest(ulong actionID, RemoteServiceActionRequest request)
        {
            StringScanner ss = new StringScanner(request.requestStr);
            string partID = ss.ExtractToken(TokenType.ToNextWhiteSpace);

            ClientActionTracker cat = new ClientActionTracker()
            {
                request = request,
                actionID = actionID,
                pushItem = new PushItem() { actionID = actionID, },
            };

            IActionState initialActionState = ActionStateCopy.Empty;

            if (partID == PingRequestServiceName)
            {
                logger.Debug.Emit("Responding to ping request");
                initialActionState = new ActionStateCopy(ActionStateCode.Complete, string.Empty, request.namedParamValues);
            }

            IActivePartBase part = (!initialActionState.IsComplete) ? IPartsInterconnection.FindPart(partID, throwOnNotFound: false) : null;
            string failureCode = string.Empty;

            if (initialActionState.IsComplete)
            { }
            else if (part != null)
            {
                IClientFacet icf = (cat.icf = part.CreateServiceAction(ss.Rest, request.namedParamValues));

                initialActionState = icf.ActionState.ConvertToActionStateCopy(autoRemoveResultCode: true);

                failureCode = icf.Start();
            }
            else
            {
                failureCode = "PartID '{0}' not found".CheckedFormat(partID);
            }

            if (failureCode.IsNullOrEmpty())
            {
                cat.lastActionState = initialActionState;
                cat.isComplete = initialActionState.IsComplete;

                cat.pushItem.itemType = PushItemType.Update;
                cat.pushItem.actionState = initialActionState.ConvertToActionStateCopy(autoRemoveResultCode: true);
                cat.pushItem.pending = true;

                clientFacetActionDictionary[actionID] = cat;        // if the other end re-uses actionID values then the prior ones will be lost.

                pendingPushItemList.Add(cat.pushItem);
            }

            return failureCode;
        }

        private string HandleInboundCancelRequest(ulong actionID)
        {
            ProviderActionTracker pat = providerFacetActionDictionary.SafeTryGetValue(actionID);
            IProviderFacet ipf = ((pat != null) ? pat.ipf : null);

            if (ipf != null)
            {
                IActionState ipfActionState = ipf.ActionState;

                if (!ipfActionState.IsCancelRequested)
                    ipf.RequestCancel();
                else
                    logger.Debug.Emit("{0}: redundant request for id:{1} [ignored]", Fcns.CurrentMethodName, actionID);

                return string.Empty;
            }
            else
            {
                return "No matching action was found for the given id";
            }
        }

        private string HandleInboundUpdate(ulong actionID, ActionStateCopy updateActionState)
        {
            ProviderActionTracker pat = providerFacetActionDictionary.SafeTryGetValue(actionID);
            IProviderFacet ipf = ((pat != null) ? pat.ipf : null);

            if (ipf != null)
            {
                IActionState currentActionState = ipf.ActionState;
                INamedValueSet updateNVS = updateActionState.DC_NamedValues;

                if (updateActionState.IsCancelRequested && !currentActionState.IsCancelRequested)
                    ipf.RequestCancel();

                if (!updateActionState.IsComplete)
                {
                    logger.Debug.Emit("{0}: note received action state update id:{1} '{2}' state:{3}", Fcns.CurrentMethodName, actionID, pat.requestStr, updateActionState);

                    if (updateNVS != null && !updateNVS.Equals(currentActionState.NamedValues))
                        ipf.UpdateNamedValues(updateNVS);
                    // else - we cannot reflect all action state changes back into the provider facet as some would violate the state model of the parent action.  As such they just get logged as debug messages.
                }
                else
                {
                    if (updateNVS != null)
                        ipf.CompleteRequest(updateActionState.ResultCode, updateNVS);
                    else
                        ipf.CompleteRequest(updateActionState.ResultCode);
                }

                return string.Empty;
            }
            else
            {
                return "No matching action was found for the given id";
            }
        }

        private void HandleInboundFailure(ulong actionID, string failureCode)
        {
            string ec = HandleInboundUpdate(actionID, new ActionStateCopy(ActionStateCode.Complete, failureCode, null));

            if (!ec.IsNullOrEmpty())
                logger.Debug.Emit("{0} id:{1} '{2}' failed: {3}", Fcns.CurrentMethodName, actionID, failureCode, ec);
        }

        #endregion

        #region Transmitter: ServiceAndGenerateNextMessageToSend, Service

        List<PushItem> pendingPushItemList = new List<PushItem>();

        public Messages.Message ServiceAndGenerateNextMessageToSend(QpcTimeStamp qpcTimeStamp)
        {
            Service(qpcTimeStamp);

            Messages.Message mesg = null;

            if (sendSetupMessage)
            {
                logger.Trace.Emit("Sending setup message");

                mesg = new Messages.Message(BufferPool).Update(buildPayloadDataFromNVS: Config.AddValues(null), orInFlags: BufferHeaderFlags.MessageContainsStreamSetup);
                sendSetupMessage = false;
            }
            else if (pendingPushItemList.Count > 0)
            {
                try
                {
                    mesg = new Messages.Message(BufferPool);

                    using (var mesgOStream = mesg.MessageBuildingStream)
                    using (var mesgOByteWriter = new System.IO.BinaryWriter(mesgOStream, binaryEncoder))
                    {
                        int pushCount = pendingPushItemList.Count;

                        mesgOByteWriter.Write(pushCount);

                        int pendingPushItemListCount = pendingPushItemList.Count;

                        foreach (var pushItem in pendingPushItemList)
                        {
                            pushItem.pending = false;       // clear the pending at the start.  If we throw to the outer catch then we will only remove the items that have been completed already and the one that was being processed when we threw.

                            System.IO.MemoryStream ms = null;

                            try
                            {
                                if (pushItem.itemType == PushItemType.Request)
                                {
                                    ms = GetEmptyMemoryStreamIfNeeded();
                                    requestDCA.WriteObject(pushItem.request, ms);
                                }
                                else if (pushItem.itemType == PushItemType.Update)
                                {
                                    ms = GetEmptyMemoryStreamIfNeeded();
                                    actionStateDCA.WriteObject(pushItem.actionState, ms);
                                }
                                else if (pushItem.itemType == PushItemType.Failure)
                                {
                                    ms = GetEmptyMemoryStreamIfNeeded();
                                    stringDCA.WriteObject(pushItem.failureCode, ms);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                string failureCode = "Serializing Push Item {0} generated exception: {1}".CheckedFormat(pushItem, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                                logger.Debug.Emit("{0}: {1}", Fcns.CurrentMethodName, failureCode);

                                pushItem.itemType = PushItemType.Failure;
                                pushItem.failureCode = failureCode;

                                ms = GetEmptyMemoryStreamIfNeeded();

                                stringDCA.WriteObject(pushItem.failureCode, ms);
                            }

                            pushItem.itemLength = (ms != null) ? unchecked((int) ms.Length) : 0;

                            pushItem.WritePartsInto(mesgOByteWriter);

                            if (ms != null)
                                ms.WriteTo(mesgOStream);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    logger.Debug.Emit("{0}: encountered unexpected exception while generating message: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    mesg = null; // discard any partial message we were building
                }

                pendingPushItemList.RemoveAll(item => !item.pending);
            }

            return mesg;
        }

        public override int Service(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            foreach (var at in providerFacetActionDictionary.ValueArray)
            {
                if (!at.pushItem.pending && at.ipf != null && !at.cancelHasBeenRequested && at.ipf.IsCancelRequestActive)
                {
                    at.cancelHasBeenRequested = true;
                    at.pushItem.itemType = PushItemType.CancelRequest;
                    at.pushItem.request = null;
                    at.pushItem.pending = true;
                    count++;

                    pendingPushItemList.Add(at.pushItem);
                }
            }

            foreach (var at in clientFacetActionDictionary.ValueArray)
            {
                if (at.icf != null)
                {
                    IActionState actionState = at.icf.ActionState;

                    if (!at.pushItem.pending && !object.ReferenceEquals(actionState, at.lastActionState))
                    {
                        at.lastActionState = actionState;
                        at.pushItem.itemType = PushItemType.Update;
                        at.pushItem.actionState = actionState.ConvertToActionStateCopy(autoRemoveResultCode: true);
                        at.pushItem.pending = true;
                        at.isComplete = actionState.IsComplete;
                        count++;

                        pendingPushItemList.Add(at.pushItem);
                    }
                }
            }

            return count;
        }

        #endregion

        #region Serialization: DCAs, PushItem, PushItemType, RemoteServiceActionRequest

        IDataContractAdapter<RemoteServiceActionRequest> requestDCA = new DataContractJsonAdapter<RemoteServiceActionRequest>();
        IDataContractAdapter<ActionStateCopy> actionStateDCA = new DataContractJsonAdapter<ActionStateCopy>();
        IDataContractAdapter<string> stringDCA = new DataContractJsonAdapter<string>();

        public enum PushItemType : byte
        {
            None = 0,
            Request,
            CancelRequest,
            Update,
            Failure,
        }

        public class PushItem
        {
            #region Fields that are read and written to BinaryWriter/Reader

            public PushItemType itemType;
            public ulong actionID;
            public int itemLength;

            public void WritePartsInto(System.IO.BinaryWriter bw)
            {
                bw.Write(unchecked((byte)itemType));
                bw.Write(actionID);
                bw.Write(itemLength);
            }

            public void ReadPartsFrom(System.IO.BinaryReader br, bool andClearRest = true)
            {
                itemType = unchecked((PushItemType)br.ReadByte());
                actionID = br.ReadUInt64();
                itemLength = br.ReadInt32();

                if (andClearRest)
                {
                    pending = false;
                    request = null;
                    actionState = null;
                    failureCode = null;
                }
            }

            #endregion

            #region fields that are used for tracking and maintenance.

            public bool pending;
            public RemoteServiceActionRequest request;
            public ActionStateCopy actionState;
            public string failureCode;

            #endregion

            public override string ToString()
            {
                if (itemType != PushItemType.Failure)
                    return "{0} id:{1} len:{2}".CheckedFormat(itemType, actionID, itemLength);
                else
                    return "{0} id:{1} len:{2} [{3}]".CheckedFormat(itemType, actionID, itemLength, failureCode);
            }
        }

        [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
        public class RemoteServiceActionRequest
        {
            /// <summary>Gives the ServiceName that was passed in with the IServiceAction</summary>
            [DataMember(Order = 20, Name="req", EmitDefaultValue = false, IsRequired = false)]
            public string requestStr;

            /// <summary>Carries the, potentialy null or empty, set of NamedValue prameters that were provided with the IServiceAction when it was last Started.</summary>
            [DataMember(Order = 30, Name = "npv", EmitDefaultValue = false, IsRequired = false)]
            public NamedValueSet namedParamValues;
        }

        #endregion
    }

    #endregion

    #region SetRelayMessageStreamTool

    public class SetRelayMessageStreamTool : MessageStreamToolBase, IMessageStreamTool
    {
        #region Construction and related fields

        /// <summary>
        /// Constructor used by RemotingClient
        /// </summary>
        public SetRelayMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, SetRelayMessageStreamToolConfigBase config)
            : base ("SetRelay.Rx", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: true)
        {
            Config = config;
            sets = Config.LocalISetsInstance ?? Sets.Sets.Instance;

            trackingSet = config.CreateReferenceTrackingSet();

            ResetState(QpcTimeStamp.Now, ResetType.ClientConstruction);
        }

        /// <summary>
        /// Constructor used by RemotingServer
        /// </summary>
        public SetRelayMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, Messages.Message streamSetupMessage, INamedValueSet streamSetupMessageNVS, Sets.ISetsInterconnection iSetsInstance)
            : base("SetRelay.Tx", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: false)
        {
            (Config = new SetRelayMessageStreamToolConfigBase()).ApplyValues(streamSetupMessageNVS);
            sets = iSetsInstance ?? Sets.Sets.Instance;

            QpcTimeStamp qpcNow = QpcTimeStamp.Now;

            ResetState(qpcNow, ResetType.ServerConstruction);

            CheckForSets(qpcNow, forceRunNow: true);
        }

        public SetRelayMessageStreamToolConfigBase Config { get; private set; }

        Sets.ITrackingSet trackingSet;

        Sets.ISetsInterconnection sets;
        Sets.ITrackableSet referenceSet;
        QpcTimeStamp lastCheckForSetsTimeStamp;

        #endregion

        #region InnerHandleResetState

        public bool clientSetupComplete = false;

        public override void InnerHandleResetState(QpcTimeStamp qpcTimeStamp, ResetType resetType, string reason)
        {
            if (isClientSide)
            {
                clientSetupComplete = false;

                if (Config.ClearClientSetOnCloseOrFailure && (resetType.IsClientFailure() || resetType.IsClientClose()))
                {
                    logger.Debug.Emit("Clearing set [{0}]", reason);
                    trackingSet.ApplyDeltas(new Sets.SetDelta<object>() { ClearSetAtStart = true, SetID = trackingSet.SetID });
                }

            }
            else if (isServerSide)
            {
                // force server to look up the set again - this path may be used in some error cases.
                trackingSet = null;
            }
        }

        #endregion

        #region Receiver: HandleInboundMessage

        private QpcTimer errorHoldoffTimer = new QpcTimer() { TriggerIntervalInSec = 10.0, AutoReset = true };

        public void HandleInboundMessage(QpcTimeStamp qpcTimeStamp, Messages.Message mesg)
        {
            if (isClientSide)
            {
                try
                {
                    var setDelta = trackingSet.Deserialize(mesg.MessageReadingStream);
                    trackingSet.ApplyDeltas(setDelta);

                    errorHoldoffTimer.StopIfNeeded();

                    if (!clientSetupComplete)
                    {
                        logger.Debug.Emit("{0}: client setup is complete for TrackingSet {1} [initial count:{2}]", Fcns.CurrentMethodName, trackingSet.SetID, trackingSet.Count);
                        clientSetupComplete = true;
                    }
                }
                catch (System.Exception ex)
                {
                    /// Todo: we may need to have some status indication for this case to allow screen display of message stream tool's "state"
                    bool log = !errorHoldoffTimer.Started || errorHoldoffTimer.GetIsTriggered(qpcTimeStamp);

                    logger.Debug.Emit("{0} encountered unexpected exception: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                    errorHoldoffTimer.StartIfNeeded();
                }
            }
            else
            {
                logger.Debug.Emit("Ignoring unexpected inbound message: {0}", mesg);
            }

            mesg.ReturnBuffersToPool(qpcTimeStamp);
        }

        #endregion

        #region Transmitter: ServiceAndGenerateNextMessageToSend, Service

        QpcTimer updateIntervalTimer = new QpcTimer() { SelectedBehavior = QpcTimer.Behavior.NewAutoReset };

        public Messages.Message ServiceAndGenerateNextMessageToSend(QpcTimeStamp qpcTimeStamp)
        {
            Messages.Message mesg = null;

            if (isServerSide)
            {
                if (trackingSet == null)
                    CheckForSets(qpcTimeStamp);

                if (trackingSet != null && trackingSet.IsUpdateNeeded && updateIntervalTimer.GetIsTriggered(qpcTimeStamp))
                {
                    try
                    {
                        Sets.ISetDelta setDelta = trackingSet.PerformUpdateIteration(Config.MaximumItemsPerMessage, generateSetDelta: true);

                        mesg = new Messages.Message(BufferPool);

                        trackingSet.Serialize(setDelta, mesg.MessageBuildingStream);

                        errorHoldoffTimer.StopIfNeeded();
                    }
                    catch (System.Exception ex)
                    {
                        bool log = !errorHoldoffTimer.Started || errorHoldoffTimer.GetIsTriggered(qpcTimeStamp);

                        logger.Debug.Emit("{0} encountered unexpected exception: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                        errorHoldoffTimer.StartIfNeeded();
                    }
                }
            }
            else if (isClientSide && sendSetupMessage)
            {
                logger.Trace.Emit("Sending setup message");

                mesg = new Messages.Message(BufferPool).Update(buildPayloadDataFromNVS: Config.AddValues(null), orInFlags: BufferHeaderFlags.MessageContainsStreamSetup);
                sendSetupMessage = false;
            }
            
            return mesg;
        }

        #endregion

        #region utilities: CheckForSets

        private int CheckForSets(QpcTimeStamp qpcTimeStamp, bool forceRunNow = false)
        {
            if (!forceRunNow && !lastCheckForSetsTimeStamp.IsZero && (qpcTimeStamp - lastCheckForSetsTimeStamp) < (5.0).FromSeconds())
                return 0;

            lastCheckForSetsTimeStamp = qpcTimeStamp;

            if (referenceSet == null)
            {
                if (!Config.SetID.UUID.IsNullOrEmpty())
                    referenceSet = sets.FindSetByUUID(Config.SetID.UUID);

                if (referenceSet == null)
                    referenceSet = sets.FindSetsByName(Config.SetID.Name).SafeAccess(0);
            }

            if (trackingSet == null && referenceSet != null)
            {
                trackingSet = referenceSet.CreateTrackingSet();

                updateIntervalTimer.TriggerInterval = Config.MinimumUpdateInterval;
                updateIntervalTimer.Reset(triggerImmediately: true);
            }

            if (referenceSet == null)
            {
                if (forceRunNow)
                    logger.Debug.Emit("Was not able to find reference set with id {0}", Config.SetID);
            }
            else
            {
                logger.Debug.Emit("Found reference set {0}{1}", referenceSet.SetID, (forceRunNow ? "" : " [while waiting]"));
            }

            return (referenceSet != null) ? 1 : 0;
        }

        #endregion
    }

    #endregion

    #region IVIRelayMessageStreamTool

    public class IVIRelayMessageStreamTool : MessageStreamToolBase, IMessageStreamTool
    {
        #region Construction and related fields

        /// <summary>
        /// Constructor used by RemotingClient
        /// </summary>
        public IVIRelayMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, IVIRelayMessageStreamToolConfig config)
            : base("IVIRelay", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: true)
        {
            Config = config;
            ivi = Config.ClientIVI ?? Values.Values.Instance;

            switch (Config.IVIRelayDirection)
            {
                case IVIRelayDirection.FromServer: isInitiator = false; isAcceptor = true; waitBeforeRegistration = false; break;
                case IVIRelayDirection.ToServer: isInitiator = true; isAcceptor = false; waitBeforeRegistration = false; break;
                case IVIRelayDirection.Bidirectional: isInitiator = true; isAcceptor = true; waitBeforeRegistration = true; break;
                default:
                    throw new System.ArgumentException("IVIRelayDirection", "{0} is not a supported value".CheckedFormat(Config.IVIRelayDirection));
            }

            localNamePrefix = Config.ServerToClientToNamePrefix.MapNullToEmpty();
            localMetaDataFilterNVS = Config.ClientToServerMetaDataFilterNVS.ConvertToReadOnly();
            mdKeywordFilterArray = localMetaDataFilterNVS.Select(nv => nv.Name).ToArray();

            ResetState(QpcTimeStamp.Now, ResetType.ClientConstruction);
        }

        /// <summary>
        /// Constructor used by RemotingServer
        /// </summary>
        public IVIRelayMessageStreamTool(string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, Messages.Message streamSetupMessage, INamedValueSet streamSetupMessageNVS, IIVIRegistration iIVIRegistration, IValuesInterconnection defaultIVI)
            : base("IVIRelay", hostPartID, stream, hostNotifier, bufferPool, isClientSideIn: false)
        {
            (Config = new IVIRelayMessageStreamToolConfig()).ApplyValues(streamSetupMessageNVS);
            IIVIRegistration = iIVIRegistration ?? Values.IVIRegistration.Instance;

            if (Config.RemoteIVIName.IsNullOrEmpty())
                ivi = defaultIVI ?? Values.Values.Instance;
            else
                ivi = IIVIRegistration.FindIVI(Config.RemoteIVIName, addNewTableIfMissing: true);

            switch (Config.IVIRelayDirection)
            {
                case IVIRelayDirection.FromServer: isInitiator = true; isAcceptor = false; waitBeforeRegistration = false; break;
                case IVIRelayDirection.ToServer: isInitiator = false; isAcceptor = true; waitBeforeRegistration = false; break;
                case IVIRelayDirection.Bidirectional: isInitiator = true; isAcceptor = true; waitBeforeRegistration = false; break;
                default: break;
            }

            localNamePrefix = Config.ServerToClientFromNamePrefix;
            localMetaDataFilterNVS = Config.ServerToClientMetaDataFilterNVS.ConvertToReadOnly();
            mdKeywordFilterArray = localMetaDataFilterNVS.Select(nv => nv.Name).ToArray();

            ResetState(QpcTimeStamp.Now, ResetType.ServerConstruction);
        }

        public IVIRelayMessageStreamToolConfig Config { get; private set; }
        public IIVIRegistration IIVIRegistration { get; private set; }
        public readonly bool isInitiator, isAcceptor, waitBeforeRegistration;

        private readonly IValuesInterconnection ivi;
        private string localNamePrefix;
        private INamedValueSet localMetaDataFilterNVS;
        private string[] mdKeywordFilterArray;

        #endregion

        #region IVATracker and related tables

        public class IVATracker
        {
            /// <summary>true then the tracker is in the locally assigned set.  false when the tracker is in the remotely assigned set</summary>
            public bool isInitiator;
            public int id;
            public string suffixName;

            /// <summary>used in incoming regisrations to indicate that there is already an outgoing registration for this item.  They will both share the same iva instance and only the locally registered version will be scanned for value propagation toward the other end.</summary>
            public bool isDuplicate;        // 

            public IValueAccessor iva;

            public UInt32 lastValueSeqNum;
            public UInt32 lastMDSeqNum;

            public PushItem pushItem;
        }

        int localIVILastProcessedValueNamesArrayLength = 0;
        bool enableInitiatingRegistration = false;
        QpcTimer checkForNewValueNamesIntervalTimer = new QpcTimer() { TriggerIntervalInSec = 0.2, AutoReset = true };

        Dictionary<string, IVATracker> stringToIVATrackerDictionary = new Dictionary<string, IVATracker>();
        IListWithCachedArray<IVATracker> locallyAssignedIVATrackerList = new IListWithCachedArray<IVATracker>();
        IListWithCachedArray<IVATracker> remotelyAssignedIVATrackerList = new IListWithCachedArray<IVATracker>();

        IValueAccessor[] locallyAssignedUpdateIVAArray = Utils.Collections.EmptyArrayFactory<IValueAccessor>.Instance;
        IValueAccessor[] remotelyAssignedUpdateIVAArray = Utils.Collections.EmptyArrayFactory<IValueAccessor>.Instance;
        IVATracker[] locallyAssignedUpdateIVATrackerArray = Utils.Collections.EmptyArrayFactory<IVATracker>.Instance;
        IVATracker[] remotelyAssignedUpdateIVATrackerArray = Utils.Collections.EmptyArrayFactory<IVATracker>.Instance;

        private void UpdateTrackerArrays(bool forceRebuildArrays = false)
        {
            if (locallyAssignedUpdateIVAArray.Length != locallyAssignedIVATrackerList.Count || forceRebuildArrays)
            {
                locallyAssignedUpdateIVAArray = new IValueAccessor[locallyAssignedIVATrackerList.Count];
                locallyAssignedUpdateIVATrackerArray = new IVATracker[locallyAssignedIVATrackerList.Count];
            }

            if (remotelyAssignedUpdateIVAArray.Length != remotelyAssignedIVATrackerList.Count || forceRebuildArrays)
            {
                remotelyAssignedUpdateIVAArray = new IValueAccessor[remotelyAssignedIVATrackerList.Count];
                remotelyAssignedUpdateIVATrackerArray = new IVATracker[remotelyAssignedIVATrackerList.Count];
            }
        }

        #endregion

        #region InnerHandleResetState

        public override void InnerHandleResetState(QpcTimeStamp qpcTimeStamp, ResetType resetType, string reason)
        {
            if (Config.ResetClientSideIVAsOnCloseOrFailure && (resetType.IsClientFailure() || resetType.IsClientClose()))
            {
                logger.Debug.Emit("Clearing server assigned IVAs [{0}]", reason);
                remotelyAssignedIVATrackerList.Array.DoForEach(ivaTracker => ivaTracker.iva.Reset());
            }

            localIVILastProcessedValueNamesArrayLength = 0;
            enableInitiatingRegistration = isInitiator && !waitBeforeRegistration;
            checkForNewValueNamesIntervalTimer.Reset(triggerImmediately: true);

            stringToIVATrackerDictionary.Clear();
            locallyAssignedIVATrackerList.Clear();
            remotelyAssignedIVATrackerList.Clear();

            UpdateTrackerArrays();
        }

        #endregion

        #region Receiver: HandleInboundMessage, HandleInboundRequest, HandleInboundCancelRequest, HandleInboundUpdate, HandleInboundFailure

        const int copyBufferLength = 4096;
        byte[] copyBuffer = new byte[copyBufferLength];
        PushItem loopPushItem = new PushItem();

        public void HandleInboundMessage(QpcTimeStamp qpcTimeStamp, Messages.Message mesg)
        {
            bool updateArraysNeeded = false;

            try
            {
                using (var mesgIStream = mesg.MessageReadingStream)
                using (var mesgIByteReader = new System.IO.BinaryReader(mesgIStream, binaryEncoder))
                {
                    int pushCount = mesgIByteReader.ReadInt32();

                    PushItem pushItem = loopPushItem;

                    foreach (var idx in Enumerable.Range(0, pushCount))
                    {
                        pushItem.ReadPartsFrom(mesgIByteReader);

                        System.IO.MemoryStream ms = GetEmptyMemoryStreamIfNeeded();

                        if (pushItem.itemLength > 0)
                        {
                            for (int offset = 0; offset < pushItem.itemLength; )
                            {
                                int copyCount = Math.Min(pushItem.itemLength - offset, copyBuffer.Length);
                                mesgIStream.Read(copyBuffer, 0, copyCount);
                                ms.Write(copyBuffer, 0, copyCount);

                                offset += copyCount;
                            }

                            ms.Position = 0;
                        }

                        try
                        {
                            if (pushItem.itemLength > 0)
                                pushItem.data = dataDCA.ReadObject(ms);
                        }
                        catch (System.Exception ex)
                        {
                            string failureCode = "Push item {0} deserialize generated exception: {1}".CheckedFormat(pushItem, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                            PushItemData data = pushItem.data ?? new PushItemData();

                            data.failureCode = data.failureCode.MapNullOrEmptyTo(failureCode);

                            pushItem.data = data;
                        }

                        logger.Trace.Emit("Processing received PushItem: {0}", pushItem);

                        int id = Math.Abs(pushItem.idPlusOne) - 1;

                        switch (pushItem.itemType)
                        {
                            case PushItemType.Register:
                                {
                                    string localFullName = (pushItem.data.suffixName != null ? String.Concat(localNamePrefix, pushItem.data.suffixName) : null);
                                    string suffixName = pushItem.data.suffixName.MapNullToEmpty();

                                    IVATracker preexistingIVATracker = stringToIVATrackerDictionary.SafeTryGetValue(suffixName);

                                    IValueAccessor iva = (preexistingIVATracker != null ? preexistingIVATracker.iva : ivi.GetValueAccessor(localFullName));

                                    if (pushItem.ItemFlagsHasInlineVC)
                                        iva.Set(pushItem.inlineVC);
                                    else if ((pushItem.itemFlags & PushItemFlags.HasVCE) != 0)
                                        iva.Set(pushItem.data.GetVC());

                                    IVATracker ivaTracker = new IVATracker()
                                    {
                                        id = id,
                                        suffixName = suffixName,
                                        iva = iva,
                                        isInitiator = false,
                                        isDuplicate = (preexistingIVATracker != null),
                                        lastValueSeqNum = iva.ValueSeqNum,
                                        lastMDSeqNum = iva.MetaDataSeqNum,
                                        pushItem = new PushItem()
                                        {
                                            idPlusOne = pushItem.idPlusOne,
                                        },
                                    };

                                    if ((pushItem.itemFlags & PushItemFlags.HasMDNVS) != 0)
                                    {
                                        if (pushItem.data.mdNVS != null)
                                            iva.SetMetaData(pushItem.data.mdNVS, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate);
                                        ivaTracker.lastMDSeqNum = iva.MetaDataSeqNum;
                                    }

                                    while (id >= remotelyAssignedIVATrackerList.Count)
                                        remotelyAssignedIVATrackerList.Add(null);

                                    remotelyAssignedIVATrackerList[id] = ivaTracker;
                                    if (preexistingIVATracker == null)
                                        stringToIVATrackerDictionary[suffixName] = ivaTracker;

                                    updateArraysNeeded = true;
                                }
                                break;

                            case PushItemType.EndCurrentRegistrationPass:
                                if (isClientSide && isInitiator && waitBeforeRegistration)
                                    enableInitiatingRegistration = true;
                                break;

                            case PushItemType.InitiatorUpdate:
                            case PushItemType.AcceptorUpdate:
                                {
                                    if (updateArraysNeeded)
                                    {
                                        UpdateTrackerArrays();
                                        updateArraysNeeded = false;
                                    }

                                    IVATracker ivaTracker = ((pushItem.itemType == PushItemType.InitiatorUpdate) ? remotelyAssignedIVATrackerList.Array : locallyAssignedIVATrackerList.Array).SafeAccess(id);

                                    if (ivaTracker != null && pushItem.data != null)
                                    {
                                        IValueAccessor iva = ivaTracker.iva;
                                        PushItemData data = pushItem.data;

                                        if (pushItem.ItemFlagsHasInlineVC)
                                        {
                                            iva.SetIfDifferent(pushItem.inlineVC);
                                            ivaTracker.lastValueSeqNum = iva.ValueSeqNum;
                                        }
                                        else if ((pushItem.itemFlags & PushItemFlags.HasVCE) != 0)
                                        {
                                            iva.SetIfDifferent(data.GetVC());
                                            ivaTracker.lastValueSeqNum = iva.ValueSeqNum;
                                        }

                                        if ((pushItem.itemFlags & PushItemFlags.HasMDNVS) != 0)
                                        {
                                            if (data.mdNVS != null)
                                                ivaTracker.iva.SetMetaData(data.mdNVS, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate);
                                            ivaTracker.lastMDSeqNum = iva.MetaDataSeqNum;
                                        }

                                        if ((pushItem.itemFlags & PushItemFlags.HasFailureCode) != 0)
                                        {
                                            logger.Debug.Emit("{0}: push item {1} received with (or generated) failureCode: {2}", Fcns.CurrentMethodName, pushItem, data.failureCode);

                                            if (data.vce == null)
                                            {
                                                iva.Set("Received failure code: {0}".CheckedFormat(data.failureCode ?? "[NullFailureCode]"));
                                                ivaTracker.lastValueSeqNum = iva.ValueSeqNum;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        logger.Debug.Emit("{0}: push item {1} failed: id is not valid [ignored]", Fcns.CurrentMethodName, pushItem);
                                    }
                                }
                                break;

                            default:
                                logger.Debug.Emit("{0}: push item {1} is not valid [ignored]", Fcns.CurrentMethodName, pushItem);
                                break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger.Debug.Emit("{0}: encountered unexpected exception while generating message: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }

            // don't hold on to really large item buffers
            ClearMemoryStreamIfNeeded(shortenIfNeeded: true);

            if (updateArraysNeeded)
                UpdateTrackerArrays();
        }

        #endregion

        #region Transmitter: ServiceAndGenerateNextMessageToSend, Service

        List<PushItem> pendingPushItemList = new List<PushItem>();

        public Messages.Message ServiceAndGenerateNextMessageToSend(QpcTimeStamp qpcTimeStamp)
        {
            Service(qpcTimeStamp);

            Messages.Message mesg = null;

            if (sendSetupMessage)
            {
                logger.Trace.Emit("Sending setup message");

                mesg = new Messages.Message(BufferPool).Update(buildPayloadDataFromNVS: Config.AddValues(null), orInFlags: BufferHeaderFlags.MessageContainsStreamSetup);
                sendSetupMessage = false;
            }
            else if (pendingPushItemList.Count > 0)
            {
                try
                {
                    mesg = new Messages.Message(BufferPool);

                    using (var mesgOStream = mesg.MessageBuildingStream)
                    using (var mesgOByteWriter = new System.IO.BinaryWriter(mesgOStream, binaryEncoder))
                    {
                        int pushCount = pendingPushItemList.Count;

                        if (Config.MaxItemsPerMessage > 0 && pushCount > Config.MaxItemsPerMessage)
                            pushCount = Config.MaxItemsPerMessage;

                        mesgOByteWriter.Write(pushCount);

                        int pendingPushItemListCount = pendingPushItemList.Count;

                        foreach (var pushItem in pendingPushItemList.Take(pushCount))
                        {
                            pushItem.pending = false;       // clear the pending at the start.  If we throw to the outer catch then we will only remove the items that have been completed already and the one that was being processed when we threw.

                            System.IO.MemoryStream ms = null;

                            try
                            {
                                if (pushItem.ItemFlagsIncludesSerializedPayload)
                                {
                                    ms = GetEmptyMemoryStreamIfNeeded();

                                    dataDCA.WriteObject(pushItem.data, ms);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                string failureCode = "Serializing Push Item {0} generated exception: {1}".CheckedFormat(pushItem, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                                logger.Debug.Emit("{0}: {1}", Fcns.CurrentMethodName, failureCode);

                                ms = GetEmptyMemoryStreamIfNeeded();

                                pushItem.itemFlags = PushItemFlags.HasFailureCode;

                                PushItemData failureCodePushItemData = new PushItemData() { failureCode = failureCode };

                                dataDCA.WriteObject(failureCodePushItemData, ms);
                            }

                            pushItem.itemLength = (ms != null) ? unchecked((int)ms.Length) : 0;

                            pushItem.WritePartsInto(mesgOByteWriter);

                            if (ms != null)
                            {
                                ms.WriteTo(mesgOStream);
                            }

                            logger.Trace.Emit("Serialized PushItem: {0}", pushItem);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    logger.Debug.Emit("{0}: encountered unexpected exception while generating message: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
                    mesg = null; // discard any partial message we were building
                }

                pendingPushItemList.RemoveAll(item => !item.pending);
            }

            return mesg;
        }

        public override int Service(QpcTimeStamp qpcTimeStamp)
        {
            int count = 0;

            if (enableInitiatingRegistration && checkForNewValueNamesIntervalTimer.IsTriggered)
                ServiceRegistration();

            if (isInitiator)
            {
                int updateCount = 0;

                foreach (var ivaTracker in locallyAssignedIVATrackerList.Array)
                {
                    IValueAccessor iva = ivaTracker.iva;

                    if (iva.IsUpdateNeeded && !ivaTracker.pushItem.pending)
                    {
                        int updateIdx = updateCount++;
                        locallyAssignedUpdateIVATrackerArray[updateIdx] = ivaTracker;
                        locallyAssignedUpdateIVAArray[updateIdx] = iva;
                    }
                }

                if (updateCount > 0)
                {
                    ivi.Update(locallyAssignedUpdateIVAArray, numEntriesToUpdate: updateCount);

                    UpdateAndEnqueueUpdatePushItems(locallyAssignedUpdateIVATrackerArray, updateCount);

                    count += updateCount;
                }
            }

            if (isAcceptor)
            {
                int updateCount = 0;

                foreach (var ivaTracker in remotelyAssignedIVATrackerList.Array)
                {
                    IValueAccessor iva = ivaTracker.iva;

                    if (iva.IsUpdateNeeded && !ivaTracker.pushItem.pending && !ivaTracker.isDuplicate)
                    {
                        int updateIdx = updateCount++;
                        remotelyAssignedUpdateIVATrackerArray[updateIdx] = ivaTracker;
                        remotelyAssignedUpdateIVAArray[updateIdx] = iva;
                    }
                }

                if (updateCount > 0)
                {
                    ivi.Update(remotelyAssignedUpdateIVAArray, numEntriesToUpdate: updateCount);

                    UpdateAndEnqueueUpdatePushItems(remotelyAssignedUpdateIVATrackerArray, updateCount);

                    count += updateCount;
                }
            }

            return count;
        }

        private void UpdateAndEnqueueUpdatePushItems(IVATracker[] locallyAssignedUpdateIVATrackerArray, int updateCount)
        {
            for (int idx = 0; idx < updateCount; idx++)
            {
                UpdateAndEnqueueUpdatePushItem(locallyAssignedUpdateIVATrackerArray[idx]);
            };
        }

        private void UpdateAndEnqueueUpdatePushItem(IVATracker ivaTracker)
        {
            var iva = ivaTracker.iva;
            var pushItem = ivaTracker.pushItem;
            var pushItemData = pushItem.data;

            pushItem.itemType = (ivaTracker.isInitiator ? PushItemType.InitiatorUpdate : PushItemType.AcceptorUpdate);

            UInt32 ivaValueSeqNum = iva.ValueSeqNum;
            UInt32 ivaMetaDataSeqNum = iva.MetaDataSeqNum;

            ValueContainer vc = iva.VC;
            bool useInlineVC = UseInlineVC(vc.cvt);

            pushItem.itemFlags = PushItemFlags.None;

            if (ivaTracker.lastValueSeqNum != ivaValueSeqNum)
            {
                if (useInlineVC)
                {
                    pushItem.itemFlags |= PushItemFlags.HasInlineVC;
                    pushItem.inlineVC = vc;
                    pushItemData.vce = null;
                }
                else
                {
                    pushItem.itemFlags |= PushItemFlags.HasVCE;
                    if (ivaTracker.lastValueSeqNum != ivaValueSeqNum)
                        pushItemData.SetFrom(iva.VC);
                    else
                        pushItemData.ClearContainedValue();
                    pushItem.inlineVC = default(ValueContainer);
                }
            }
            else
            {
                pushItem.inlineVC = default(ValueContainer);
                pushItemData.vce = null;
            }

            if (ivaTracker.lastMDSeqNum != ivaMetaDataSeqNum)
            {
                pushItem.itemFlags |= PushItemFlags.HasMDNVS;
                pushItemData.mdNVS = iva.MetaData.ConvertToReadOnly().MapEmptyToNull();
            }

            pushItemData.suffixName = null;
            pushItemData.failureCode = null;

            ivaTracker.lastValueSeqNum = ivaValueSeqNum;
            ivaTracker.lastMDSeqNum = ivaMetaDataSeqNum;

            pushItem.pending = true;

            pendingPushItemList.Add(pushItem);

            logger.Trace.Emit("Update enqueued PushItem: {0}", pushItem);
        }

        private void ServiceRegistration()
        {
            int capturedValueNamesArrayLength = ivi.ValueNamesArrayLength;

            if (localIVILastProcessedValueNamesArrayLength >= capturedValueNamesArrayLength)
                return;

            ivaItemFilterDelegate = ivaItemFilterDelegate ?? IVAItemFilter;

            int availableCount = capturedValueNamesArrayLength - localIVILastProcessedValueNamesArrayLength;

            filterApplicationCount = 0;

            IValueAccessor[] filteredIVAArray = ivi.GetFilteredValueAccessors(ivaItemFilterDelegate, localIVILastProcessedValueNamesArrayLength, availableCount);

            localIVILastProcessedValueNamesArrayLength += filterApplicationCount;

            foreach (var iva in filteredIVAArray)
            {
                int id = locallyAssignedIVATrackerList.Count;
                string suffixName = iva.Name.Substring(localNamePrefix.Length);

                if (stringToIVATrackerDictionary.ContainsKey(suffixName))
                    continue;       // we have already seen this key - there is no need to add it again (it could be either in the locally assigned or the remotely assigned set - either is sufficient to prevent it from being added again)

                ValueContainer vc = iva.VC;
                NamedValueSet mdNVS = iva.MetaData.ConvertToReadOnly().MapEmptyToNull();

                bool useInlineVC = UseInlineVC(vc.cvt);
                int idPlusOne = id + 1;
                IVATracker ivaTracker = new IVATracker()
                {
                    id = id,
                    suffixName = suffixName,
                    isInitiator = true,
                    iva = iva,
                    lastValueSeqNum = iva.ValueSeqNum,
                    lastMDSeqNum = iva.MetaDataSeqNum,
                    pushItem = new PushItem()
                    {
                        idPlusOne = isClientSide ? idPlusOne : -idPlusOne,
                        itemType = PushItemType.Register,
                        itemFlags = PushItemFlags.HasSuffixName | (useInlineVC ? PushItemFlags.HasInlineVC : PushItemFlags.HasVCE) | (mdNVS != null ? PushItemFlags.HasMDNVS : PushItemFlags.None),
                        inlineVC = (useInlineVC ? vc : default(ValueContainer)),
                        data = new PushItemData()
                        {
                            suffixName = suffixName,
                            mdNVS = mdNVS,
                        },
                        pending = true,
                    },
                };

                if (!useInlineVC)
                    ivaTracker.pushItem.data.SetFrom(vc);

                pendingPushItemList.Add(ivaTracker.pushItem);

                logger.Trace.Emit("Registration enqueued PushItem: {0}", ivaTracker.pushItem);

                locallyAssignedIVATrackerList.Add(ivaTracker);
                stringToIVATrackerDictionary[suffixName] = ivaTracker;
            }

            if (filteredIVAArray.Length > 0)
            {
                var endOfRegistrationPassPushItem = new PushItem()
                {
                    itemType = PushItemType.EndCurrentRegistrationPass,
                    pending = true,
                };

                pendingPushItemList.Add(endOfRegistrationPassPushItem);
            }

            UpdateTrackerArrays();
        }

        private static bool UseInlineVC(ContainerStorageType cst) { return (cst.IsValueType() || cst.IsNone()); }

        int filterApplicationCount = 0;

        Func<string, INamedValueSet, bool> ivaItemFilterDelegate = null;

        bool IVAItemFilter(string name, INamedValueSet md)
        {
            filterApplicationCount++;

            if (!localNamePrefix.IsNullOrEmpty() && !name.StartsWith(localNamePrefix))
                return false;

            if (mdKeywordFilterArray.IsNullOrEmpty())
                return true;

            foreach (var keyword in mdKeywordFilterArray)
            {
                if (md.Contains(keyword))
                    return true;
            }

            return false;
        }

        #endregion

        #region Serialization: DCAs, PushItem, PushItemType, PushItemBody

        IDataContractAdapter<PushItemData> dataDCA = new DataContractJsonAdapter<PushItemData>();

        /// <summary>
        /// The type of item that is being pushed.
        /// <para/>None (0), Register, EndCurrentRegistrationPass, InitiatorUpdate, AcceptorUpdate.
        /// </summary>
        public enum PushItemType : byte
        {
            None = 0,
            Register,
            EndCurrentRegistrationPass,
            InitiatorUpdate,
            AcceptorUpdate,
        }

        [Flags]
        public enum PushItemFlags : byte
        {
            None = 0x00,
            HasSuffixName = 0x01,
            HasVCE = 0x02,
            HasMDNVS = 0x04,
            HasFailureCode = 0x08,
            HasInlineVC = 0x10,
            UseShortID = 0x20,
            UseIntID = 0x40,
            UseByteID = 0x80,
        }

        public class PushItem
        {
            #region Fields that are read and written to BinaryWriter/Reader

            public PushItemType itemType;
            public PushItemFlags itemFlags;
            /// <summary>
            /// Please note: the sign of this value is generally used as a debugging clue of which end assigned the number.  
            /// Generally clients assign this as possitive values and servers assign this as negative values.
            /// However it is important to note that at the protocol level the sign of this field is ignored.  
            /// The message processing engine knows which direction the message is going in based on the PushItemType being either InitiatorUpdate (from IVA's registering end), or AcceptorUpdate (from IVAs other end).
            /// </summary>
            public int idPlusOne;
            public ValueContainer inlineVC;
            public int itemLength;

            public bool ItemFlagsHasInlineVC { get { return ((itemFlags & PushItemFlags.HasInlineVC) != 0); } }
            public bool ItemFlagsIncludesSerializedPayload { get { return ((itemFlags & ~PushItemFlags.HasInlineVC) != PushItemFlags.None); } }

            public void WritePartsInto(System.IO.BinaryWriter bw)
            {
                // the following id size decode logic is expected to be used exactly once per push item.
                if ((itemFlags & (PushItemFlags.UseIntID | PushItemFlags.UseShortID)) == 0)
                {
                    if (!idPlusOne.IsInRange(-32768, 32767))
                        itemFlags |= PushItemFlags.UseIntID;
                    else if (!idPlusOne.IsInRange(-128, 127))
                        itemFlags |= PushItemFlags.UseShortID;
                    else
                        itemFlags |= PushItemFlags.UseByteID;
                }

                bw.Write(unchecked((byte)itemType));
                bw.Write(unchecked((byte)itemFlags));

                switch (itemFlags & (PushItemFlags.UseIntID | PushItemFlags.UseShortID | PushItemFlags.UseByteID))
                {
                    case PushItemFlags.UseByteID: bw.Write(unchecked((sbyte)idPlusOne)); break;
                    case PushItemFlags.UseShortID: bw.Write(unchecked((short)idPlusOne)); break;
                    case PushItemFlags.UseIntID: bw.Write(idPlusOne); break;
                    default: bw.Write(idPlusOne); break;
                }

                if (ItemFlagsHasInlineVC)
                {
                    bw.Write(unchecked((byte) inlineVC.cvt));
                    switch (inlineVC.cvt)
                    {
                        case ContainerStorageType.None: break;
                        case ContainerStorageType.Bo: bw.Write(inlineVC.u.b); break;
                        case ContainerStorageType.Bi: bw.Write(inlineVC.u.bi); break;
                        case ContainerStorageType.I1: bw.Write(inlineVC.u.i8); break;
                        case ContainerStorageType.I2: bw.Write(inlineVC.u.i16); break;
                        case ContainerStorageType.I4: bw.Write(inlineVC.u.i32); break;
                        case ContainerStorageType.I8: bw.Write(inlineVC.u.i64); break;
                        case ContainerStorageType.U1: bw.Write(inlineVC.u.u8); break;
                        case ContainerStorageType.U2: bw.Write(inlineVC.u.u16); break;
                        case ContainerStorageType.U4: bw.Write(inlineVC.u.u32); break;
                        case ContainerStorageType.U8: bw.Write(inlineVC.u.u64); break;
                        case ContainerStorageType.F4: bw.Write(inlineVC.u.f32); break;
                        case ContainerStorageType.F8: bw.Write(inlineVC.u.f64); break;
                        case ContainerStorageType.TimeSpan: bw.Write(inlineVC.u.i64); break;        // TimeSpan values are stored internally as i64 values
                        case ContainerStorageType.DateTime: bw.Write(inlineVC.u.i64); break;        // DateTime values are stored internally as i64 values
                        default: break;
                    }
                }

                if (ItemFlagsIncludesSerializedPayload)
                    bw.Write(itemLength);
            }

            public void ReadPartsFrom(System.IO.BinaryReader br, bool andClearRest = true)
            {
                itemType = unchecked((PushItemType)br.ReadByte());
                itemFlags = unchecked((PushItemFlags)br.ReadByte());

                switch (itemFlags & (PushItemFlags.UseIntID | PushItemFlags.UseShortID | PushItemFlags.UseByteID))
                {
                    case PushItemFlags.UseByteID: idPlusOne = br.ReadSByte(); break;
                    case PushItemFlags.UseShortID: idPlusOne = br.ReadInt16(); break;
                    case PushItemFlags.UseIntID: idPlusOne = br.ReadInt32(); break;
                    default: idPlusOne = br.ReadInt32(); break;
                }

                if (ItemFlagsHasInlineVC)
                {
                    switch (inlineVC.cvt = unchecked((ContainerStorageType)br.ReadByte()))
                    {
                        case ContainerStorageType.None: break;
                        case ContainerStorageType.Bo: inlineVC.u.b = br.ReadBoolean(); break;
                        case ContainerStorageType.Bi: inlineVC.u.bi = br.ReadByte(); break;
                        case ContainerStorageType.I1: inlineVC.u.i8 = br.ReadSByte(); break;
                        case ContainerStorageType.I2: inlineVC.u.i16 = br.ReadInt16(); break;
                        case ContainerStorageType.I4: inlineVC.u.i32 = br.ReadInt32(); break;
                        case ContainerStorageType.I8: inlineVC.u.i64 = br.ReadInt64(); break;
                        case ContainerStorageType.U1: inlineVC.u.u8 = br.ReadByte(); break;
                        case ContainerStorageType.U2: inlineVC.u.u16 = br.ReadUInt16(); break;
                        case ContainerStorageType.U4: inlineVC.u.u32 = br.ReadUInt32(); break;
                        case ContainerStorageType.U8: inlineVC.u.u64 = br.ReadUInt64(); break;
                        case ContainerStorageType.F4: inlineVC.u.f32 = br.ReadSingle(); break;
                        case ContainerStorageType.F8: inlineVC.u.f64 = br.ReadDouble(); break;
                        case ContainerStorageType.TimeSpan: inlineVC.u.i64 = br.ReadInt64(); break;        // TimeSpan values are stored internally as i64 values
                        case ContainerStorageType.DateTime: inlineVC.u.i64 = br.ReadInt64(); break;        // DateTime values are stored internally as i64 values
                    }
                }
                else if (!inlineVC.IsEmpty)
                {
                    inlineVC = default(ValueContainer);
                }

                itemLength = ItemFlagsIncludesSerializedPayload ? br.ReadInt32() : 0;

                if (andClearRest)
                {
                    data.suffixName = null;
                    data.vce = null;
                    data.mdNVS = null;
                    data.failureCode = null;
                }
            }

            #endregion

            #region fields that are used for tracking and maintenance.

            public PushItemData data = new PushItemData();

            public bool pending;

            #endregion

            public override string ToString()
            {
                string inlineVCStr = (itemFlags.IsSet(PushItemFlags.HasInlineVC) ? " "+inlineVC.ToStringSML() : "");
                string dataStr = (ItemFlagsIncludesSerializedPayload ? " data:[{0}]".CheckedFormat(data) : "");
                return "{0} idp1:{1} flags:[{2}] len:{3}{4}{5}".CheckedFormat(itemType, idPlusOne, itemFlags, itemLength, inlineVCStr, dataStr);
            }
        }

        [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
        public class PushItemData
        {
            [DataMember(Order = 100, EmitDefaultValue = false, IsRequired = false)]
            public string suffixName;

            [DataMember(Order = 200, EmitDefaultValue = false, IsRequired = false)]
            public ValueContainerEnvelope vce;

            [DataMember(Order = 300, EmitDefaultValue = false, IsRequired = false)]
            public NamedValueSet mdNVS;

            [DataMember(Order = 400, EmitDefaultValue = false, IsRequired = false)]
            public string failureCode;

            public bool IsEmpty { get { return (suffixName == null && vce == null && mdNVS == null && failureCode == null); } }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                if (suffixName != null)
                    sb.CheckedAppendFormat("suffix:'{0}'", suffixName);
                if (vce != null)
                    sb.CheckedAppendFormatWithDelimiter(" ", "vce:{0}", vce);
                if (mdNVS != null)
                    sb.CheckedAppendFormatWithDelimiter(" ", "md:{0}", mdNVS.ToStringSML());
                if (failureCode != null)
                    sb.CheckedAppendFormatWithDelimiter(" ", "failureCode:'{0}'", failureCode);
                if (sb.Length == 0)
                    sb.Append("[Empty]");

                return sb.ToString();
            }

            /// <summary>
            /// This method attempts to generate a new VCE for the value contained in the given <paramref name="vc"/>
            /// </summary>
            public void SetFrom(ValueContainer vc)
            {
                try
                {
                    vce = new ValueContainerEnvelope() { VC = vc };
                }
                catch (System.Exception ex)
                {
                    vce = new ValueContainerEnvelope() { VC = ValueContainer.Create(ex.ToString(ExceptionFormat.TypeAndMessage)) };
                }
            }

            /// <summary>
            /// This method checks if the data includes a vce, and if so returns the vce.VC, otherwise this method returns ValueContainer.Empty
            /// </summary>
            public ValueContainer GetVC()
            {
                if (vce != null)
                    return vce.VC;
                else
                    return ValueContainer.Empty;
            }

            /// <summary>
            /// Clears the contained vce.
            /// </summary>
            public void ClearContainedValue()
            {
                vce = null;
            }
        }

        #endregion
    }

    #endregion

    #region MessageStreamToolBase

    public abstract class MessageStreamToolBase : DisposableBase, IServiceable
    {
        public MessageStreamToolBase(string loggingSourceStrPart, string hostPartID, int stream, INotifyable hostNotifier, Buffers.BufferPool bufferPool, bool isClientSideIn)
        {
            logger = new Logging.Logger("{0}.{1}.s{2}".CheckedFormat(hostPartID, loggingSourceStrPart, stream), groupName: Logging.LookupDistributionGroupName);

            HostPartID = hostPartID;
            Stream = stream;
            HostNotifier = hostNotifier;
            BufferPool = bufferPool;

            isClientSide = isClientSideIn;
            isServerSide = !isClientSideIn;

            AddExplicitDisposeAction(Release);
        }

        /// <summary>Client initiates message stream tool creation in the server.  Different tools have different directionalities of communication based on type and configuration.</summary>
        public readonly bool isClientSide;

        /// <summary>Server create message stream tool by client request.  Different tools have different directionalities of communication based on type and configuration.</summary>
        public readonly bool isServerSide;

        public virtual void Release()
        {
            Fcns.DisposeOfObject(ref memoryStream);
        }

        public void ResetState(QpcTimeStamp qpcTimeStamp, ResetType resetType, string reason = null)
        {
            reason = (reason.IsNullOrEmpty() ? resetType.ToString() : "{0}:{1}".CheckedFormat(resetType, reason));

            if (resetType.IsFailure())
            {
                if (!sendSetupMessage)
                    logger.Info.Emit("{0}: {1}", Fcns.CurrentMethodName, reason);
                else
                    logger.Debug.Emit("{0}: {1}", Fcns.CurrentMethodName, reason);
            }
            else if (resetType.IsClose())
                logger.Debug.Emit("{0}: {1}", Fcns.CurrentMethodName, reason);
            else
                logger.Trace.Emit("{0}: {1}", Fcns.CurrentMethodName, reason);

            Service(qpcTimeStamp);

            InnerHandleResetState(qpcTimeStamp, resetType, reason);

            if (isClientSide)
                sendSetupMessage = true;
        }

        public bool sendSetupMessage = false;

        public virtual int Service(QpcTimeStamp tsNow) 
        { 
            return 0; 
        }

        public abstract void InnerHandleResetState(QpcTimeStamp qpcTimeStamp, ResetType resetType, string reason);

        public Logging.Logger logger;

        public string HostPartID { get; private set; }
        public int Stream { get; private set; }
        public INotifyable HostNotifier { get; private set; }
        public BufferPool BufferPool { get; private set; }

        #region memoryStream and binaryEncoder: GetEmptyMemoryStreamIfNeeded, ClearMemoryStreamIfNeeded

        public System.IO.MemoryStream memoryStream = null;

        public const int defaultMemoryStreamCapacity = 128;
        public const int maximumRetainedMemoryStreamCapacity = 16384;

        public readonly System.Text.Encoding binaryEncoder = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true);

        protected System.IO.MemoryStream GetEmptyMemoryStreamIfNeeded(bool clearContents = true)
        {
            if (memoryStream == null)
                memoryStream = new System.IO.MemoryStream(defaultMemoryStreamCapacity);

            if (clearContents && memoryStream.Length > 0)
                memoryStream.SetLength(0);

            return memoryStream;
        }

        protected void ClearMemoryStreamIfNeeded(bool shortenIfNeeded = false)
        {
            if (memoryStream != null)
            {
                if (memoryStream.Length > 0)
                    memoryStream.SetLength(0);

                if (shortenIfNeeded && memoryStream.Capacity > maximumRetainedMemoryStreamCapacity)
                    memoryStream.Capacity = defaultMemoryStreamCapacity;
            }
        }

        #endregion

    }

    #endregion
}

//-------------------------------------------------------------------
