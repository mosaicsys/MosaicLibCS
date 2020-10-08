//-------------------------------------------------------------------
/*! @file E005Manager.cs
 *  @brief This file defines the Manager related aspects of the E005 Message handling.
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
using System.Linq;

using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Semi.E005.Port;

namespace MosaicLib.Semi.E005.Manager
{
    //-------------------------------------------------------------------

    /// <summary>
    /// This delegate type is used by Stream/Function handling entities when registering their interest in processing or observing receieved messages in a specific stream function or set thereof.
    /// Only one such processing delegate may be registered for any given stream/function, but many observing delegates may do so.
    /// <para/>The delegate is passed the <paramref name="mesg"/> instance that has been received and which is to be processed/observed.
    /// For processing cases where the <paramref name="mesg"/> expects a reply, the delegate may assign the reply to send to the given <paramref name="mesg"/> instance and the caller will send the reply for it, 
    /// however the delegate cannot make use of internally blocking or time consumptive paths in this case as doing so would significantly decrease the performance of the host interface and
    /// could lead to deadlock conditions.
    /// <para/>For processing cases where the <paramref name="mesg"/> expects a reply but where the delegate cannot immediately generate the reply, the delegate shall arrange for the reply to be sent at a
    /// later time and shall return instead of assigning the reply message to the given <paramref name="mesg"/> directly.
    /// </summary>
    public delegate void ReceivedMessageProcessingDelegate(IMessage mesg);

    //-------------------------------------------------------------------

    /// <summary>
    /// This interface is used by a port to generate message sequence numbers that are unique accross all of the related ports (those that belong to the same manager)
    /// and to distribute primary messages into the manager for handling there.
    /// </summary>
	public interface IManagerPortFacet
    {
        /// <summary>Returns a sequence number to use for a message.  Will be unique accross all ports that belong to this manager.</summary>
		UInt32 GetNextMessageSequenceNum();

        /// <summary>
        /// Method that is used by IPort objects to tell the manager about primary messages that have been received.  
        /// Manager is reponsible for implementing all handler logic for this message including error checking, and distribution to the Stream/Function and port appropriate handler
        /// </summary>
		void PrimaryMesgReceivedFromPort(IMessage mesg);

        /// <summary>Returns a sequence number to use as DATAID values in client code.  Will be unique accross all of the ports that make use of this manager instance.</summary>
        UInt32 GetNextDATAID();

        /// <summary>Returns true if the manager believes that the given stream function is a high rate stream function</summary>
        bool IsHighRateSF(StreamFunction sf);
    }

    /// <summary>
    /// This interface defines the client usable methods for registering and using E005.IPort instances to send and recive messages.
    /// </summary>
    public interface IManager : IDisposable, IPartBase
    {
        /// <summary>
        /// Creates and adds a port to this manager.  The given <paramref name="portConfigNVS"/> contains the set of parameters that are used to configure the type and operation of the created port.
        /// <para/>The caller may explicitly specify the "PortName" and "PortType" or these values may be obtained from the given <paramref name="portConfigNVS"/>.  
        /// If the caller includes the makeDefault or includes the "MakeDefault" keyword in the <paramref name="portConfigNVS"/> then the manager will attempt to make this port be the default one, unless another port has already been defined (or inferred) to be the default one.
        /// <para/>Different port types also typically make use of addition required and/or optional parameters which are defined on a port type specific basis
        /// </summary>
        IPort CreatePort(string portName = null, PortType portType = default(PortType), bool makeDefault = false, INamedValueSet portConfigNVS = null, bool goOnline = false);

        /// <summary>
        /// Starts all of the created ports, as needed, and optionally initializes them.
        /// <para/>Note: ports must be explicitly started before messages can be sent using them.
        /// </summary>
        string StartPortsIfNeeded(bool initializePorts = true);

        /// <summary>Stops all of the created ports as needed.  If <paramref name="goOffline"/> is true then a GoOffline action will be run first.</summary>
        void StopPortsIfNeeded(bool goOffline = true);

        /// <summary>Returns the default port for this manager.  The first call to this property getter will either return the first explicitly defined default port or it will assign the default port to be the first one (PortNum 1).  Throws ManagerException if no ports have been added when this property getter is first used.</summary>
        IPort DefaultPort { get; }

        /// <summary>Returns the set of ports that currently belong to this manager instance.</summary>
        ReadOnlyIList<IPort> PortSet { get; }

        /// <summary>Attempts to find and return the port with the given <paramref name="portName"/>.  Either throws ManagerException or returns null, depending on <paramref name="throwOnNotFound"/> if no port is found with the given name.</summary>
        IPort GetPort(string portName, bool throwOnNotFound = true);

        /// <summary>
        /// Attempts to register the given <paramref name="handler"/> to handle the given <paramref name="streamFunctionParamsArray"/> set of streams/functions/Wbits to Process messages for.
        /// <para/>Note that both bytes of the stream/function in the header are indexed on so that the caller can use a different handler for a stream/function with and without the W bit set.
        /// <para/>Note that to register a hanlder for an entire stream use function 0 in the registration call.  Function specific registrations take priority over whole stream handlers so the use of the two types can be mixed.
        /// </summary>
        void RegisterSFProcessingHandler(ReceivedMessageProcessingDelegate handler, params StreamFunction[] streamFunctionParamsArray);

        /// <summary>Flags that the given set of stream/function values are considered high rate.</summary>
        void SetSFSetAsHighRate(params StreamFunction[] streamFunctionParamsArray);

        /// <summary>Returns a sequence number to use as DATAID values in client code.  Will be unique accross all of the ports that make use of this manager instance.</summary>
        UInt32 GetNextDATAID();
    }

    /// <summary>
    /// Exception type thrown by an IManager when reporting a condition that cannot be handled.
    /// </summary>
    public class ManagerException : System.Exception
    {
        /// <summary>
        /// ManagerExecption constructor
        /// </summary>
        public ManagerException(string reason, System.Exception innerExecption = null)
            : base(message: reason, innerException: innerExecption)
        { }
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Attempts to register the given <paramref name="handler"/> to handle the parsed results from the given <paramref name="streamFunctionStrParamsArray"/> set of StreamFunction strings to Process messages for.
        /// <para/>Note that both bytes of the stream/function in the header are indexed on so that the caller can use a different handler for a stream/function with and without the W bit set.
        /// <para/>Note that to register a hanlder for an entire stream use function 0 in the registration call.  Function specific registrations take priority over whole stream handlers so the use of the two types can be mixed.
        /// </summary>
        public static void RegisterSFProcessingHandler(this IManager manager, ReceivedMessageProcessingDelegate handler, params string[] streamFunctionStrParamsArray)
        {
            manager.RegisterSFProcessingHandler(handler, streamFunctionStrParamsArray.Select(sfStr => sfStr.ParseAsStreamFunction()).ToArray());
        }

        /// <summary>Flags that the given set of stream/function values are considered high rate.</summary>
        public static void SetSFSetAsHighRate(this IManager manager, params string[] streamFunctionStrParamsArray)
        {
            manager.SetSFSetAsHighRate(streamFunctionStrParamsArray.Select(sfStr => sfStr.ParseAsStreamFunction()).ToArray());
        }
    }

    //-------------------------------------------------------------------

    /// <summary>
    /// This is a static namespace class for the Manager pattern under E005.  It currently contains the IManager singleton instance.
    /// </summary>
	public static class Manager
    {
        /// <summary>Returns an AutoConstructIfNeeded (and thus changable) IManager singleton instance.  When replacing a previously provided, or constructed, instance this setter must be set to null (to dispose of the current instance) before the new instance can be assigned.</summary>
        public static IManager Instance { get { return singletonHelper.Instance; } set { singletonHelper.Instance = value; } }

        private static Utils.SingletonHelperBase<IManager> singletonHelper = new MosaicLib.Utils.SingletonHelperBase<IManager>(Utils.SingletonInstanceBehavior.AutoConstructIfNeeded, () => new ManagerBase("E005DefaultManager"));

        /// <summary>
        /// This method accepts a given <paramref name="mesg"/>.  If the given <paramref name="mesg"/> expects a reply then this method
        /// creates a default reply for the message and sets it as the <paramref name="mesg"/>'s Reply using the SetReply method.  
        /// When done in the primary registered message handler, this will cause the calling manager/port to automatically send the created reply without additional effort.
        /// </summary>
        public static void GenericAutoEmptyReplyHandler(IMessage mesg)
        {
            if (mesg.SF.ReplyExpected)
                mesg.SetReply(mesg.CreateReply());
        }
    }

    //-------------------------------------------------------------------

    /// <summary>Manager basic implementation class</summary>
    public class ManagerBase : Modular.Part.SimplePartBase, IManager, IManagerPortFacet
    {
        //-------------------------------------------------------------------
        #region Construction/Destruction

        public ManagerBase(string partID)
            : base(partID)
        {
            AddExplicitDisposeAction(HandleExplicitDispose);
        }

        #endregion

        #region IManager

        private readonly object mainAPIMutex = new object();

        IPort IManager.CreatePort(string portName, PortType portType, bool makeDefault, INamedValueSet portConfigNVS, bool goOnline)
        {
            portName = (portName ?? portConfigNVS["PortName"].VC.GetValueA(rethrow: true)).Sanitize();

            if (portType == PortType.None)
                portType = portConfigNVS["PortType"].VC.GetValue<PortType>(rethrow: true);

            if (!makeDefault)
                makeDefault = portConfigNVS.Contains("MakeDefault");

            IPort port = null;

            lock (mainAPIMutex)
            {
                if (portByNameDictionary.ContainsKey(portName))
                    new ManagerException("Cannot create Port '{0}': another port with this name already exists".CheckedFormat(portName)).Throw();

                if (makeDefault && defaultPort != null)
                    new ManagerException("Cannot create Port '{0}': MakeDefault requested and there is already another default port: '{1}' [{2}]".CheckedFormat(portName, defaultPort.PartID, defaultPort.PortNum)).Throw();

                int portNum = portByNameDictionary.Count + 1;

                switch (portType)
                {
                    //case PortType.E004_Master: 
                    //case PortType.E004_Slave:
                    //    port = new E004.E004Port(portName, portNum, portType, portConfigNVS, this); 
                    //    break;

                    case PortType.E037_Active_SingleSession:
                    case PortType.E037_Passive_SingleSession:
                        port = new E037.E037Port(portName, portNum, portType, portConfigNVS, this);
                        break;

                    default:
                        new ManagerException("Cannot create Port '{0}': the specified port type {1} is not supported or valid".CheckedFormat(portName, portType)).Throw();
                        break;
                }

                portByNameDictionary[portName] = port;
                portSet = null;

                if (makeDefault)
                    defaultPort = port;
            }

            if (port != null && goOnline)
                port.RunGoOnlineAction();

            return port;
        }

        string IManager.StartPortsIfNeeded(bool initializePorts)
        {
            lock (mainAPIMutex)
            {
                IPort[] portArray = portByNameDictionary.ValueArray;

                foreach (var port in portArray)
                {
                    port.StartPartIfNeeded();
                }

                string ec = string.Empty;

                IPort[] relevantPortArray = EmptyArrayFactory<IPort>.Instance;
                if (initializePorts)
                    relevantPortArray = portArray.Where(port => { var baseState = port.BaseState; return !baseState.IsOnlineAndConnected(); }).ToArray();
                //else
                //    relevantPortArray = portArray.Where(port => { var baseState = port.BaseState; return (baseState.IsOffline() || baseState.IsUninitialized() || baseState.UseState == UseState.AttemptOnlineFailed); }).ToArray();

                IClientFacet[] icfArray = relevantPortArray.Select(port => port.CreateGoOnlineAction(initializePorts).StartInline()).ToArray();

                var firstFailedICF = icfArray.WaitUntilSetComplete(completionType: Modular.Action.ExtentionMethods.WaitForSetCompletionType.All).FirstOrDefault(icf => icf.ActionState.Failed);
                ec = (firstFailedICF != null) ? firstFailedICF.ActionState.ResultCode : string.Empty;

                return ec;
            }
        }

        void IManager.StopPortsIfNeeded(bool goOffline)
        {
            lock (mainAPIMutex)
            {
                IPort[] portArray = portByNameDictionary.ValueArray;

                IClientFacet[] icfArray = portArray.Where(port => { var baseState = port.BaseState; return baseState.IsOnlineOrAttemptOnline; }).Select(port => port.CreateGoOfflineAction().StartInline()).ToArray();

                var firstFailedICF = icfArray.WaitUntilSetComplete(completionType: Modular.Action.ExtentionMethods.WaitForSetCompletionType.All).FirstOrDefault(icf => icf.ActionState.Failed);
                string ec = (firstFailedICF != null) ? firstFailedICF.ActionState.ResultCode : string.Empty;

                foreach (var port in portArray)
                {
                    port.StopPart();
                }
            }
        }

        public IPort DefaultPort
        {
            get
            {
                lock (mainAPIMutex)
                {
                    if (defaultPort == null)
                        defaultPort = portByNameDictionary.ValueArray.FirstOrDefault();

                    if (defaultPort == null)
                        new ManagerException("Cannot get default port: there are no defined ports").Throw();

                    return defaultPort;
                }
            }
        }

        /// <summary>Returns the set of ports that currently belong to this manager instance.</summary>
        public ReadOnlyIList<IPort> PortSet
        {
            get
            {
                lock (mainAPIMutex)
                {
                    if (portSet == null)
                        portSet = portByNameDictionary.ValueArray.ConvertToReadOnly();

                    return portSet;
                }
            }
        }

        IPort IManager.GetPort(string portName, bool throwOnNotFound)
        {
            IPort port;

            lock (mainAPIMutex)
            {
                port = portByNameDictionary.SafeTryGetValue(portName);
            }

            if (port == null && throwOnNotFound)
                new ManagerException("Port '{0}' was not found".CheckedFormat(portName)).Throw();

            return port;
        }

        /// <summary>
        /// Attempts to register the given <paramref name="handler"/> to handle the given <paramref name="streamFunctionParamsArray"/> set of streams/functions/Wbits to Process messages for.
        /// <para/>Note that both bytes of the stream/function in the header are indexed on so that the caller can use a different handler for a stream/function with and without the W bit set.
        /// <para/>Note that to register a hanlder for an entire stream use function 0 in the registration call.  Function specific registrations take priority over whole stream handlers so the use of the two types can be mixed.
        /// </summary>
        void IManager.RegisterSFProcessingHandler(ReceivedMessageProcessingDelegate handler, params StreamFunction[] streamFunctionParamsArray)
        {
            lock (mainAPIMutex)
            {
                foreach (var sf in streamFunctionParamsArray.MapNullToEmpty())
                {
                    string ec = InnerAddHandler(handler, sf);
                    if (ec.IsNeitherNullNorEmpty())
                        new ManagerException(ec).Throw();
                }
            }
        }

        /// <summary>Flags that the given set of stream/function values are considered high rate.</summary>
        void IManager.SetSFSetAsHighRate(params StreamFunction[] streamFunctionParamsArray)
        {
            lock (mainAPIMutex)
            {
                highRateSFSet.SafeAddRange(streamFunctionParamsArray.Select(sf => sf.B2B3));
                roHighRateSFSet = new ReadOnlyHashSet<ushort>(highRateSFSet);
            }
        }

        HashSet<ushort> highRateSFSet = new HashSet<ushort>();
        ReadOnlyHashSet<ushort> roHighRateSFSet = ReadOnlyHashSet<ushort>.Empty;

        #endregion

        #region GetNextDATAID for both IManager and IManagerPortFacet interfaces

        Utils.AtomicUInt32 dataidNumGenerator = new MosaicLib.Utils.AtomicUInt32(0);

        /// <summary>Returns a sequence number to use as DATAID values in client code.  Will be unique accross all of the ports that make use of this manager instance.</summary>
        public UInt32 GetNextDATAID()
        {
            return dataidNumGenerator.IncrementSkipZero();
        }

        #endregion

        #region Port list and cleanup (HandleExplicitDispose)

        IDictionaryWithCachedArrays<string, IPort> portByNameDictionary = new IDictionaryWithCachedArrays<string, IPort>();
        IPort defaultPort = null;
        ReadOnlyIList<IPort> portSet = null;

        private void HandleExplicitDispose()
        {
            Utils.Collections.DisposableList<IPort> capturedPortsList = null;

            lock (mainAPIMutex)
            {
                capturedPortsList = new Utils.Collections.DisposableList<IPort>(portByNameDictionary.ValueArray);
                portByNameDictionary.Clear();
            }

            Utils.Fcns.DisposeOfObject(ref capturedPortsList);
        }


        #endregion

        #region IManagerPortFacet explicit implementation

        Utils.AtomicUInt32 mesgSeqNumGenerator = new MosaicLib.Utils.AtomicUInt32(0);

        UInt32 IManagerPortFacet.GetNextMessageSequenceNum()
        {
            return mesgSeqNumGenerator.IncrementSkipZero();
        }

        void IManagerPortFacet.PrimaryMesgReceivedFromPort(IMessage mesg)
        {
            ReceivedMessageProcessingDelegate handler = AsyncGetProcessHandler(mesg.SF);

            bool sendFaultReply = false;
            if (handler != null)
            {
                try
                {
                    handler(mesg);
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit("Handler failed on Mesg {0} generated unexpected exception: {1}", mesg, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                    sendFaultReply = mesg.SF.ReplyExpected;
                }

                // the calling port will send the reply
            }
            else
            {
                if (mesg.SF.ReplyExpected)
                {
                    // send an S<n>/F0 reply (header only) to indicate that the message was rejected
                    Log.Trace.Emit("No handler found for Mesg {0} received from port:{1}.  Sending reject notice to F0", mesg.TenByteHeader, mesg.Port.PortNum);

                    sendFaultReply = true;
                }
                else
                {
                    Log.Debug.Emit("No handler found for Mesg {0} received from port:{1}.  Ignoring", mesg.TenByteHeader, mesg.Port.PortNum);
                }
            }

            if (sendFaultReply)
            {
                var faultReplyMesg = mesg.Port.CreateMessage(mesg.SF.TransactionAbortReplySF);
                faultReplyMesg.SeqNum = mesg.SeqNum;

                mesg.SetReply(faultReplyMesg, replaceReply: true);
            }
        }

        bool IManagerPortFacet.IsHighRateSF(StreamFunction sf)
        {
            return roHighRateSFSet.Contains(sf.B2B3);
        }

        #endregion

        #region StreamFunction Handler lookup table and related logic (InnerAddHandler, AsyncGetProcessHandler)

        private readonly PerStreamHandlerLookupTableItem[] perStreamHandlerLookupTableArray = new PerStreamHandlerLookupTableItem[256];

        private class PerStreamHandlerLookupTableItem
        {
            public volatile ReceivedMessageProcessingDelegate[] perFunctionHandlerArray = new ReceivedMessageProcessingDelegate[256];
            public volatile ReceivedMessageProcessingDelegate fallbackStreamHandler;
        }

        string InnerAddHandler(ReceivedMessageProcessingDelegate handler, StreamFunction sf)
        {
            if (handler == null)
                return "handler is null";

            byte streamAndWByte = sf.Byte2;
            byte functionByte = sf.FunctionByte;
            bool forEntireStream = (functionByte == 0);

            if (streamAndWByte == 0 || !perStreamHandlerLookupTableArray.IsSafeIndex(streamAndWByte))
                return "{0} is not valid".CheckedFormat(sf);

            PerStreamHandlerLookupTableItem level1Item = perStreamHandlerLookupTableArray[streamAndWByte];
            if (level1Item == null)
                perStreamHandlerLookupTableArray[streamAndWByte] = level1Item = new PerStreamHandlerLookupTableItem();

            if (!forEntireStream)
            {
                if (!level1Item.perFunctionHandlerArray.IsSafeIndex(functionByte))
                    return "{0} is not valid".CheckedFormat(sf);
                if (level1Item.perFunctionHandlerArray[functionByte] == handler)
                { } // do not complain if the code registers the same handler instance twice.
                else if (level1Item.perFunctionHandlerArray[functionByte] != null)
                    return "{0} already has a registered handler".CheckedFormat(sf);

                level1Item.perFunctionHandlerArray[functionByte] = handler;
            }
            else
            {
                if (level1Item.fallbackStreamHandler != null)
                    return "{0} already has a fallback handler".CheckedFormat(sf);

                level1Item.fallbackStreamHandler = handler;
            }

            return string.Empty;
        }

        private ReceivedMessageProcessingDelegate AsyncGetProcessHandler(StreamFunction sf)
        {
            PerStreamHandlerLookupTableItem asyncLevel1Item = perStreamHandlerLookupTableArray.SafeAccess(sf.Byte2);

            if (asyncLevel1Item == null)
                return null;

            return asyncLevel1Item.perFunctionHandlerArray.SafeAccess(sf.FunctionByte) ?? asyncLevel1Item.fallbackStreamHandler;
        }

        #endregion
    }
    //-------------------------------------------------------------------
}

