//-------------------------------------------------------------------
/*! @file PartBase.cs
 *  @brief This file contains common definitions, interface and base classes that are used to define the common characteristics of Parts in this library.
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
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.Modular.Part
{
    //-----------------------------------------------------------------
    #region enums UseState and ConnState

	/// <summary>
    /// Generic summary state for current usability of a part
    /// <para/>This enum provides a set of state codes that are helpfull in determining if a Part is usable and if not, then why, particularly whether the part is online or not.
    /// All Parts support a UseState but they may only use the specific subset of the defined state codes that are relevant for that part and its capabilities.
    /// <para/>Undefined (0), Initial, AttemptOnline, OnlineUninitialized, Online, OnlineBusy, OnlineFailure, Offline, AttemptOnlineFailed, FailedToOffline, Shutdown
    /// </summary>
    [DataContract(Namespace=Constants.ModularNameSpace)]
	public enum UseState
	{
		/// <summary>no valid value has been given or UseState is not supported by this part (0)</summary>
        [EnumMember]
		Undefined = 0,
		
        /// <summary>part has not been started (1)</summary>
        [EnumMember]
        Initial = 1,
		
        /// <summary>part is attempting to go online (either automatically after start of by request) (2)</summary>
        [EnumMember]
        AttemptOnline = 2,
		
        /// <summary>part is online but it requires initialization before it can be used normally. (3)</summary>
        [EnumMember]
        [Obsolete("Please switch to using the correctly spelled version (2018-05-23)")]
        OnlineUnintialized = 3,

        /// <summary>part is online but it requires initialization before it can be used normally. (3)</summary>
        [EnumMember]
        OnlineUninitialized = 3,
		
        /// <summary>part is online (4)</summary>
        [EnumMember]
        Online = 4,
		
        /// <summary>part is online and is performing some action (5)</summary>
        [EnumMember]
        OnlineBusy = 5,
		
        /// <summary>part is online but is not functioning correctly.  May include cases where a GoOnline action partially succeeded. (6)</summary>
        [EnumMember]
        OnlineFailure = 6,
		
        /// <summary>part was explicitly told to go offline (7)</summary>
        [EnumMember]
        Offline = 7,
		
        /// <summary>part failed its attempt to go online (8)</summary>
        [EnumMember]
        AttemptOnlineFailed = 8,
		
        /// <summary>part had an urecoverable error (or exception recovery) that forced it to the offline state - may try to AttemptOnline depending on configuration (9)</summary>
        [EnumMember]
        FailedToOffline = 9,

        /// <summary>part has been shutdown prior to application exit. (10)</summary>
        [EnumMember]
        Shutdown = 10,
	}

	/// <summary>
    /// Generic summary state for current connection state of part for parts that support a single connection.
    /// <para/>This enum defines a set of state codes that are helpfull in determining if a Part is connected to some related entity (or not), especially when the Part
    /// either implements the connection itself or depends on such a connection for it correct operation.  All parts support a ConnState but they may only use a 
    /// specific subset of the defined state codes that are relevant for that part and its specific operation.
    /// <para/>Undefined (0), Initial, NotApplicable, Connecting, ConnectionFailed, ConnectFailed, Disconnected, DisconnectedByOtherEnd
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace)]
    public enum ConnState
	{
		/// <summary>no valid value has been given or ConnState is not supported by this part (0)</summary>
        [EnumMember]
        Undefined = 0,

        /// <summary>Either stays here until device has been put online explicitly or attempts autoOnline if appropriate (1)</summary>
        [EnumMember]
        Initial = 1,

        /// <summary>this part does not (directly) support any known connection (2)</summary>
        [EnumMember]
        NotApplicable = 2,

        /// <summary>the part is connecting (has initiated a connection but the connection is not complete and has not failed) (3)</summary>
        [EnumMember]
        Connecting = 3,

        /// <summary>the part is waiting for another party to try to connect (4)</summary>
        [EnumMember]
        WaitingForConnect = 4,

        /// <summary>Connection to remote device is believed to be available and/or operating correctly (5)</summary>
        [EnumMember]
        Connected = 5,

        /// <summary>Connection to remote device is not available (rejected or failed while in use).  State supports automatic reconnection attempt (6)</summary>
        [EnumMember]
        ConnectionFailed = 6,

        /// <summary>Attempt to connect failed.  State supports automatic reconnection attempt (7)</summary>
        [EnumMember]
        ConnectFailed = 7,

        /// <summary>Connection has been explicitly stopped (8)</summary>
        [EnumMember]
        Disconnected = 8,

        /// <summary>Connection has been explicitly stopped by the other end (9)</summary>
        [EnumMember]
        DisconnectedByOtherEnd = 9,
    }

	#endregion

    //-----------------------------------------------------------------
    #region static class for static query (extension) methods on UseState and ConnState enum values (used by BaseState)

    /// <summary>
    /// Static utility method class used in relation to UseState and ConnState enums
    /// </summary>
    public static class BaseStateFcns
    {
        /// <summary>
        /// Returns true if the given UseState value is any of the Online states.
        /// <para/>Accepts Online, OnlineBusy
        /// <para/>Accepts OnlineUninitialized if <paramref name="acceptUninitialized"/> is true
        /// <para/>Accepts OnlineFailure if <paramref name="acceptOnlineFailure"/> is true
        /// <para/>Accepts AttemptOnline if <paramref name="acceptAttemptOnline"/> is true
        /// </summary>
        public static bool IsOnline(this UseState useState, bool acceptAttemptOnline = false, bool acceptUninitialized = true, bool acceptOnlineFailure = true)
        {
            switch (useState)
            {
                case UseState.OnlineUninitialized:
                    return acceptUninitialized;
                case UseState.Online:
                case UseState.OnlineBusy:
                    return true;
                case UseState.OnlineFailure:
                    return acceptOnlineFailure;
                case UseState.AttemptOnline:
                    return acceptAttemptOnline;
                default:
                    return false;
            }
        }

        /// <summary>Returns true if the given UseState value is any of the Busy states (OnlineBusy and AttemptOnline)</summary>
        public static bool IsBusy(this UseState useState)
        {
            return (useState == UseState.OnlineBusy || useState == UseState.AttemptOnline);
        }

        /// <summary>Returns true if the given ConnState value is any of the states that represent an active connection attempt being in progress (Conneting, WaitingForConnect)</summary>
        public static bool IsConnecting(this ConnState connState)
        {
            return (connState == ConnState.Connecting || connState == ConnState.WaitingForConnect);
        }

        /// <summary>Returns true if the given ConnState value is any of the connected states (Connected)</summary>
        public static bool IsConnected(this ConnState connState)
        {
            return (connState == ConnState.Connected);
        }

        /// <summary>Returns true if the given ConnState value is IsConnected or is IsConnecting</summary>
        public static bool IsConnectedOrConnecting(this ConnState connState)
        {
            return (IsConnected(connState) || IsConnecting(connState));
        }
    }

       #endregion

    //-----------------------------------------------------------------
	#region IBaseState, IPartBase interface(s)

	/// <summary>
	/// This interface defines the basic set of properties that are available clients of any type of Part to inquire about that part's state of opertion.
	/// </summary>
	/// <remarks>
	/// Objects that implement this interface for use outside of a part must be invariant once a client has access to the underlying state object.  As such
	/// the implementation of this interface shall be done in such a manner that once a client has access to an object that implements this interface, the
	/// client shall always get the same, self consistant, set of results from invoking methods on this interface, regardless of when they invoked such methods
	/// (properties) or in what order.  To obtain new values for the results of invoking such methods, the client must explicitly request a new IBaseState object
	/// whic will then contain the more current results.
	/// </remarks>
	public interface IBaseState : IEquatable<IBaseState>
	{
        /// <summary>gives the PartID of the part that created this base state, or null if a part did not create this object</summary>
        string PartID { get; }

        /// <summary>return true if the part is simulated</summary>
		bool IsSimulated { get; }

		/// <summary>return true if the part is a primary part.  secondary parts are parts that provide secondary functional facade on a common underlying part but where the secondary facade should not be used to manage the part's online/offline state or to start or stop it.</summary>
		bool IsPrimaryPart { get; }

		/// <summary>reports the UseState of the part at the time the client obtained this state object</summary>
		UseState UseState { get; }

		/// <summary>reports the ConnState of the part at the time the client obtained this state object</summary>
		ConnState ConnState { get; }

        /// <summary>reports the, possibly empty, name of any action that part is currently performing</summary>
        string ActionName { get; }

        /// <summary>reports the, possibly empty, reported reason for the last BaseState change</summary>
        string Reason { get; }

		/// <summary>reports the timestamp at which the new contents or state object was generated.</summary>
		QpcTimeStamp TimeStamp { get; }

		/// <summary>summary property reports true if the UseState is in an Online state</summary>
		bool IsOnline { get; }

        /// <summary>summary property reports true if the UseState is in an Online or AttemptOnline states</summary>
        bool IsOnlineOrAttemptOnline { get; }

        /// <summary>summary property reports true if the UseState indicates that the device is busy.  Details of functionality depends on internal part behavior.</summary>
		bool IsBusy { get; }

		/// <summary>summary property reports true if the ConnState indicates that the part is in the process of making a connection.</summary>
		bool IsConnecting { get; }

        /// <summary>summary property reports true if the ConnState indicates that the part's connection (if any) is connected.</summary>
		bool IsConnected { get; }

        /// <summary>summary property reports true if the ConnState indicates that the part's connection is connected or is making a connection</summary>
		bool IsConnectedOrConnecting { get; }

        /// <summary>returns true if either the UseState or the ConnState are not at their Undefined value.  Typically this means that the part has not be started or that it has not generated its initial value.</summary>
        bool IsDefined { get; }

        /// <summary>Returns true if the given rhs is non-null and if the contents of this and the given rhs IBaseState are equal to each other.</summary>
        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        bool IsEqualTo(IBaseState rhs);

        /// <summary>
        /// Gives selectable ToString output
        /// </summary>
        string ToString(BaseState.ToStringSelect toStringSelect);
	}

	/// <summary>
	/// This inteface defines the standard set of methods and properties that are supported by all types of Parts.  
	/// It gives the client access to the part's ID, Type and to its base state (via BaseState property).
	/// </summary>
	public interface IPartBase : IDisposable
	{
		/// <summary>Reports the PartID, or name of the part</summary>
		string PartID { get; }

		/// <summary>Reports the PartType, or the name of the type of part </summary>
		string PartType { get; }

		/// <summary>Returns a reference to the last published BaseState from the part</summary>
		IBaseState BaseState { get; }

        /// <summary>Property gives client access to the part's Notification Object for the part's BaseState property.</summary>
        INotificationObject<IBaseState> BaseStateNotifier { get; }
    }

	#endregion

    #region extension methods

    /// <summary>Standard extension methods wrapper class/namespace</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given baseState is non-null and its UseState is IsOnline or its ConnState is Connected
        /// </summary>
        public static bool IsOnlineAndConnected(this IBaseState baseState)
        {
            return (baseState != null && baseState.IsOnline && baseState.IsConnected);
        }

        /// <summary>
        /// Returns true if the given baseState is non-null and its UseState is Initial or its ConnState is Initial
        /// </summary>
        public static bool IsUninitialized(this IBaseState baseState)
        {
            return (baseState != null && (baseState.UseState == UseState.Initial || baseState.ConnState == ConnState.Initial));
        }

        /// <summary>
        /// Returns true if the given baseState is non-null and its UseState is Offline or Shutdown
        /// </summary>
        public static bool IsOffline(this IBaseState baseState)
        {
            return (baseState != null && (baseState.UseState == UseState.Offline || baseState.UseState == UseState.Shutdown));
        }

        /// <summary>
        /// Returns true if the given baseState is null or its UseState is any Failure/Failed state or its ConnState is any Failed state.
        /// <para/>Includes UseStates: AttemptOnlineFailed, FailedToOffline, OnlineFailure
        /// <para/>includes ConnStates: ConnectFailed, ConnectionFailed
        /// </summary>
        public static bool IsFaulted(this IBaseState baseState)
        {
            if (baseState == null)
                return true;

            switch (baseState.UseState)
            {
                case UseState.AttemptOnlineFailed:
                case UseState.FailedToOffline:
                case UseState.OnlineFailure:
                    return true;
                default:
                    break;
            }

            switch (baseState.ConnState)
            {
                case ConnState.ConnectFailed:
                case ConnState.ConnectionFailed:
                    return true;
            }

            return false;
        }
    }

    #endregion

    //-----------------------------------------------------------------
	#region Active part interface

    /// <summary>This interface is a client side view of an Action that has no parameter and no result.</summary>
	public interface IBasicAction : IClientFacet { }

    /// <summary>This interface is a client side view of an Action that has a boolean parameter and no result.</summary>
	public interface IBoolParamAction : IClientFacetWithParam<bool>, IBasicAction { }

    /// <summary>This interface is a client side view of an Action that has a string parameter and no result.</summary>
    public interface IStringParamAction : IClientFacetWithParam<string>, IBasicAction { }

	/// <summary>
	/// This interface is implemeneted by all ActiveParts.  
	/// It provides the ability for a client to be notified when the part publishes a new BaseState value.
	/// It provides the ability for the client to create actions that can request tha the part 
	/// Go Online, Go Online and Initialize, or to Go Offline.  It also provides a common interface for a client
	/// to create a service action for the part.  Details of useful/legal Service actions and their functions are 
	/// entirely part dependant
	/// </summary>
	public interface IActivePartBase : IPartBase
	{
		/// <summary>
        /// Method used to Start the part.  Some parts automatically start whenever a GoOnlineAction is created.
        /// This method may only safely be used on a part that has not already been started and which is not already being
        /// started on another thread.
        /// </summary>
		void StartPart();

        /// <summary>
        /// This method determines if the part has already been started and starts the part if not.
        /// This method is serialized through the use of a lock so that it gives consistent behavior even
        /// if it is called from multiple threads.  Use of this method cannot be safely combined with the use of the direct StartPart method.
        /// Locks the startIfNeeded mutex and then calls StartPart if the part's HasBeenStarted flag is false.
        /// Use of mutex protects against re-enterant use of CreateGoOnlineAction but not against concurrent patterns where 
        /// CreateGoOnlineAction is used concurrently with an explicit call to StartPart.
        /// <para/>Supports call chaining
        /// </summary>
        IActivePartBase StartPartIfNeeded();

		/// <summary>Method used to explicitly stop the part.  Parts also self stop when they are explicilty Disposed.</summary>
		void StopPart();

        /// <summary>Returns true if the part has been successfully started with StartPart.</summary>
        bool HasBeenStarted { get; }

        /// <summary>Returns true if the part has been stopped with StopPart or if its primary action Q has been disabled.</summary>
        bool HasBeenStopped { get; }

        /// <summary>Returns true if the part HasBeenStarted and not it HasBeenStopped</summary>
        bool IsRunning { get; }

		// All active parts provide the following basic actions which they can perform

		/// <summary>Creates a GoOnline action.  When action is run, it will cause the part to attempt to transition to an Online state.</summary>
		IBasicAction CreateGoOnlineAction();

		/// <summary>Creates a GoOnline with the andInitialize parameter.  When action is run, it will cause the part to fully reset its internal state (if andInitialize is true) and then attempt to go online.</summary>
		/// <param name="andInitialize">pass this flag as true to force full reset of part's internal state as part of GoOnline action.  Normally this implies that the parts Goes offline before it goes online.</param>
		IBoolParamAction CreateGoOnlineAction(bool andInitialize);

		/// <summary>Creates a GoOffline action.  When action is run, it will cause the part to enter an offline state and to discontinue any actions it is currently performing.</summary>
		IBasicAction CreateGoOfflineAction();

		// All active parts provide a CreateServiceAction.  This action type is
		//	designed to allow each part to provide a common means for generating
		//	and invoking service methods using pre-agreed, externally specified, 
		//	string identifiers for each such service action that is provided by the 
		//	part.  This allows a given part to define its essential interface 
		//	elements and methods and then to use this single mechanism to accept 
		//	and perform multiple service requests from the GUI (or some other equivilent).
		//	

		/// <summary>Creates a blank Service Action.  Service actions require a string parameter value which must be provided to the created action before it is started.  All service action functionality is Part specific.</summary>
		IStringParamAction CreateServiceAction();

        /// <summary>Creates a Service Action and preinitializes its parameter to the given serviceName value.  All service action functionality is Part specific.</summary>
		IStringParamAction CreateServiceAction(string serviceName);

        /// <summary>Method creates a Service Action with the serviceName and NamedParamValues preconfigured with the given values.</summary>
        IStringParamAction CreateServiceAction(string serviceName, INamedValueSet namedParamValues);
    }

	#endregion

	//-----------------------------------------------------------------
	#region PartBaseBase class

	/// <summary>
	/// This abstract base class is generally used as a base class for all Part base classes (active or otherwise).
    /// <seealso cref="SimplePartBase"/> and <seealso cref="SimpleActivePartBase"/>
    /// <para/>This class is derived from <see cref="DisposableBase"/>
	/// </summary>
	public abstract class PartBaseBase : DisposableBase, IPartBase
	{
		#region IPartBase Members

        /// <summary>Reports the PartID, or name of the part</summary>
        public string PartID { get; private set; }

        /// <summary>Reports the PartType, or the name of the type of part </summary>
        public string PartType { get; private set; }

        /// <summary>Returns a reference to the last published BaseState from the part.  This property is abstract and must be implemented in a derived class.</summary>
        public abstract IBaseState BaseState { get; }

        /// <summary>Property gives client access to the part's Notification Object for the part's BaseState property.  This property is abstract and must be implemented in a derived class.</summary>
        public abstract INotificationObject<IBaseState> BaseStateNotifier { get; }

		#endregion

        #region Protected Construction (for use by derived classes only)

        /// <summary>
        /// Protected base class constructor: initializes the PartID and PartType fields
        /// </summary>
        /// <param name="partID">This gives the name that the part will carry</param>
        /// <param name="partType">This gives the type that the part will carry</param>
		protected PartBaseBase(string partID, string partType)
        {
            if (!partID.IsNullOrEmpty())
                PartID = partID;
            else
                PartID = "[DefaultPartID:${0:x4}]".CheckedFormat(partIDSeqNumGenerator.Increment());

            if (!partType.IsNullOrEmpty())
                PartType = partType;
            else
                PartType = this.GetType().ToString();
        }

        private static AtomicInt64 partIDSeqNumGenerator = new AtomicInt64();

        #endregion

        // NOTE: Dispose specific behavior related to the use of the Explicit Dispose Action and the use of the AddExplicitDipsoseAction method have been moved to
        //  DisposeableBase class to support more common use of this delegate list based approach to disposal.

        #region More utility properties and methods (CurrentStackFrame, CurrentMethod, CurrentMethodName, CurrentClassName, CurrentClassLeafName, FmtWin32EC, FmtStdEC)

        // Note: The following 5 CurrentYYY methods are copies of the corresponding global static methods under the MosaicLib.Utils.Fcns "namespace" static class

        /// <summary>Creates and returns the callers current StackFrame</summary>
        public static StackFrame CurrentStackFrame 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1); }
        }

        /// <summary>Creates a StackFrame for the caller and returns the stack frame's current method.</summary>
        public static MethodBase CurrentMethod 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1).GetMethod(); } 
        }

        /// <summary>Creates a StackFrame for the caller and returns the Name of the stack frame's current method.</summary>
        public static string CurrentMethodName 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1).GetMethod().Name; } 
        }

        /// <summary>Creates a StackFrame for the caller and returns the Name of the current methods DeclaringType</summary>
        public static string CurrentClassName 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(); } 
        }

        /// <summary>Creates a StackFrame for the caller and returns the Leaf Name of the current methods DeclaringType (The token at the end of any sequence of dot seperated tokens)</summary>
        public static string CurrentClassLeafName 
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return (new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString()).Split('.').SafeLast(); } 
        }

        /// <summary>Protected utility method.  Returns the result of calling <code>Utils.EC.FmtWin32EC(PartID, win32EC);</code></summary>
		protected string FmtWin32EC(int win32EC) { return Utils.EC.FmtWin32EC(PartID, win32EC); }

        /// <summary>Protected utility method.  Returns the result of calling <code>Utils.EC.FmtStdEC(PartID, errorStr);</code></summary>
        protected string FmtStdEC(string errorStr) { return Utils.EC.FmtStdEC(PartID, errorStr); }

        /// <summary>Protected utility method.  Returns the result of calling <code>Utils.EC.FmtStdEC(PartID, errorCode, errorStr);</code></summary>
        protected string FmtStdEC(int errorCode, string errorStr) { return Utils.EC.FmtStdEC(PartID, errorCode, errorStr); }

        #endregion
	};

	#endregion

	//-----------------------------------------------------------------
	#region BaseState struct

	/// <summary>
	/// This struct is used by most types of Part as a storage container for the part's base state.  This struct also implements the IBaseState interface
	/// and may be used with some other storage container to service the part's BaseState property.
	/// </summary>
    [DataContract(Namespace=Constants.ModularNameSpace), Serializable]
	public struct BaseState : IBaseState
	{
		#region private fields

		private UseState useState;
        private ConnState connState;
        private string actionName;
        [NonSerialized]
        private QpcTimeStamp timeStamp;
        private string reason;

		#endregion

        #region Serialization support

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            age = timeStamp.Age.TotalSeconds;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            timeStamp = QpcTimeStamp.Now + age.FromSeconds();
        }

        #endregion

        #region IBaseState interface

        /// <summary>gives the PartID of the part that created this base state, or null if a part did not create this object</summary>
        [DataMember(Order = 90, EmitDefaultValue = false, IsRequired = false)]
        public string PartID { get; set; }

        /// <summary>return true if the part is simulated</summary>
        [DataMember(Order = 100, EmitDefaultValue = false, IsRequired = false)]
        public bool IsSimulated { get; private set; }

        /// <summary>return true if the part is a primary part.  secondary parts are parts that provide secondary functional facade on a common underlying part but where the secondary facade should not be used to manage the part's online/offline state or to start or stop it.</summary>
        [DataMember(Order = 200, EmitDefaultValue = false, IsRequired = false)]
        public bool IsPrimaryPart { get; private set; } // true if this part represents the master interface to a part which supports multiple interfaces

        /// <summary>reports the UseState of the part at the time the client obtained this state object</summary>
        [DataMember(Order = 300)]
        public UseState UseState { get { return useState; } set { useState = value; timeStamp.SetToNow(); } }

        /// <summary>reports the ConnState of the part at the time the client obtained this state object</summary>
        [DataMember(Order = 400)]
        public ConnState ConnState { get { return connState; } set { connState = value; timeStamp.SetToNow(); } }

        /// <summary>reports the, possibly empty, name of any action that part is currently performing</summary>
        [DataMember(Order = 500)]
        public string ActionName { get { return actionName.MapNullToEmpty(); } set { actionName = value; timeStamp.SetToNow(); } }

        /// <summary>reports the, possibly empty, reported reason for the last BaseState change.</summary>
        [DataMember(Order = 600)]
        public string Reason { get { return reason.MapNullToEmpty(); } set { reason = value; timeStamp.SetToNow(); } }

        /// <summary>reports the timestamp at which the new contents or state object was generated.</summary>
        public QpcTimeStamp TimeStamp { get { return timeStamp; } }

        private double age;

        /// <summary>summary property reports true if the UseState is in an Online state</summary>
        public bool IsOnline { get { return UseState.IsOnline(); } }

        /// <summary>summary property reports true if the UseState is in an Online or AttemptOnline states</summary>
        public bool IsOnlineOrAttemptOnline { get { return UseState.IsOnline(acceptAttemptOnline: true); } }

        /// <summary>summary property reports true if the UseState indicates that the device is busy.  Details of functionality depends on internal part behavior.</summary>
        public bool IsBusy { get { return UseState.IsBusy(); } }

        /// <summary>summary property reports true if the ConnState indicates that the part is in the process of making a connection.</summary>
        public bool IsConnecting { get { return ConnState.IsConnecting(); } }
        /// <summary>summary property reports true if the ConnState indicates that the part's connection (if any) is connected.</summary>
        public bool IsConnected { get { return ConnState.IsConnected(); } }
        /// <summary>summary property reports true if the ConnState indicates that the part's connection is connected or is making a connection</summary>
        public bool IsConnectedOrConnecting { get { return (ConnState.IsConnectedOrConnecting()); } }

        /// <summary>returns true if either the UseState or the ConnState are not at their Undefined value.  Typically this means that the part has not be started or that it has not generated its initial value.</summary>
        public bool IsDefined { get { return (UseState != UseState.Undefined || ConnState != ConnState.Undefined); } }

        /// <summary>Returns true if the given other IBaseState is non-null and if the contents of this and the given other IBaseState are equal to each other.</summary>
        public bool Equals(IBaseState other)
        {
            return (other != null
                && PartID == other.PartID
                && IsSimulated == other.IsSimulated
                && IsPrimaryPart == other.IsPrimaryPart
                && UseState == other.UseState
                && ConnState == other.ConnState
                && ActionName == other.ActionName
                && Reason == other.Reason
                && TimeStamp == other.TimeStamp
                );
        }

        [Obsolete("Please replace with the use of the corresponding IEquateable<>.Equals method (2017-03-10)")]
        public bool IsEqualTo(IBaseState other)
        {
            return Equals(other);
        }

		#endregion

		#region public methods

        /// <summary>static property that returns an empty IBaseState</summary>
        public static IBaseState None { get { return new BaseState(); } }

        /// <summary>Constructor: allows caller to specify if the source part IsPrimaryPart or not</summary>
        public BaseState(bool isPrimaryPart) 
            : this() 
        { 
            IsPrimaryPart = isPrimaryPart; 
        }
        /// <summary>Constructor: allows caller to specify if the source part IsSimulated and IsParimaryPart</summary>
        public BaseState(bool isSimulated, bool isPrimaryPart) 
            : this(isPrimaryPart) 
        { 
            IsSimulated = isSimulated; 
        }

        /// <summary>Copy constructor</summary>
        public BaseState(IBaseState other) 
            : this()
        {
            PartID = other.PartID;
            IsSimulated = other.IsSimulated;
            IsPrimaryPart = other.IsPrimaryPart;
            useState = other.UseState;
            connState = other.ConnState;
            actionName = other.ActionName;
            timeStamp = other.TimeStamp;
        }

        /// <summary>Sets the part as Simulator and allows it to be put in an Online state and/or to have its IsPrimaryPart property set.</summary>
        public BaseState SetSimulated(bool online, bool primary = true)
		{
			IsSimulated = true;
			IsPrimaryPart = primary;
			return SetState(online ? UseState.Online : UseState.Initial, ConnState.NotApplicable);
		}

        /// <summary>Sets the UseState and ConnState values and sets the contained TimeStamp to Now.</summary>
        public BaseState SetState(UseState useState, ConnState connState)
		{
			this.useState = useState;
			this.connState = connState;
			timeStamp.SetToNow();
            return this;
		}

        /// <summary>Sets the UseState and ActionName and sets the contained TimeStamp to Now.  If the current ConnState is default(Undefined) and <paramref name="autoInitializeConnStateIfNeeded"/> is true then the ConnState will be set to <paramref name="autoInitializeConnStateTo"/> which defaults to NotApplicable</summary>
        public BaseState SetState(UseState useState, string actionName, bool autoInitializeConnStateIfNeeded = true, ConnState autoInitializeConnStateTo = ConnState.NotApplicable)
        {
            this.useState = useState;
            if (autoInitializeConnStateIfNeeded && this.connState == default(ConnState))
                this.connState = autoInitializeConnStateTo;
            this.actionName = actionName;
            timeStamp.SetToNow();
            return this;
        }

        /// <summary>Sets the UseState, ActionName, and ConnState parameters and sets the TimeStamp to Now.</summary>
        public BaseState SetState(UseState useState, string actionName, ConnState connState)
        {
            this.useState = useState;
            this.actionName = actionName;
            this.connState = connState;
            timeStamp.SetToNow();
            return this;
        }

        /// <summary>Sets the UseState, ConnState, and the Reason parameters and sets the TimeStamp to Now.</summary>
        public BaseState SetState(UseState useState, ConnState connState, string reason)
        {
            this.useState = useState;
            this.connState = connState;
            this.reason = reason;
            timeStamp.SetToNow();
            return this;
        }

        /// <summary>Sets the UseState, ActionName, ConnState, and the Reason parameters and sets the TimeStamp to Now.</summary>
        public BaseState SetState(UseState useState, string actionName, ConnState connState, string reason)
        {
            this.useState = useState;
            this.actionName = actionName;
            this.connState = connState;
            this.reason = reason;
            timeStamp.SetToNow();
            return this;
        }

        /// <summary>
        /// Sets this structs contents to be a copy from the given IBaseState value.
        /// </summary>
        public BaseState SetState(IBaseState rhs)
        {
            this = new BaseState(rhs);
            return this;
        }

        /// <summary>
        /// Sets this structs contents to be a copy from the given IBaseState value and updates the reason to the given value
        /// </summary>
        public BaseState SetState(IBaseState rhs, string reason)
        {
            this = new BaseState(rhs) { reason = reason };
            return this;
        }

        /// <summary>Provides a print/log/debug suitable string representation of the contents of this state object.</summary>
        public override string ToString()
        {
            return ToString(ToStringSelect.All);
        }

        /// <summary>Provides a print/log/debug suitable string representation of the contents of this state object.</summary>
        public string ToString(ToStringSelect toStringSelect)
		{
            bool includePartID = toStringSelect.IsSet(ToStringSelect.PartID) && !PartID.IsNullOrEmpty();
            bool includeUseState = toStringSelect.IsSet(ToStringSelect.UseState);
            bool includeActionName = toStringSelect.IsSet(ToStringSelect.ActionName) && (IsBusy && (!actionName.IsNullOrEmpty()));
            bool includeReason = toStringSelect.IsSet(ToStringSelect.Reason) && (!reason.IsNullOrEmpty());
            bool includeConnState = toStringSelect.IsSet(ToStringSelect.ConnState) && (connState != ConnState.NotApplicable);

            StringBuilder sb = new StringBuilder();

            if (includePartID)
                sb.CheckedAppendFormat("partID:{0}", PartID);

            if (toStringSelect.IsSet(ToStringSelect.UseState))
                sb.CheckedAppendFormatWithDelimiter(", ", "use:{0}", useState);
            else if (toStringSelect.IsSet(ToStringSelect.UseStateNoPrefix))
                sb.CheckedAppendFormatWithDelimiter(", ", "{0}", useState);

            if (includeActionName)
                sb.CheckedAppendFormatWithDelimiter(", ", "action:{0}", actionName);

            if (includeConnState)
                sb.CheckedAppendFormatWithDelimiter(", ", "conn:{0}", connState);
            
            if (includeReason)
                sb.CheckedAppendFormatWithDelimiter(", ", "reason:[{0}]", reason);

            return sb.ToString();
		}

        /// <summary>
        /// Used to select which parts to include when using the customized version of the BaseState ToString method
        /// <para/>None (0x00), PartID (0x01), UseState (0x02), ActionName (0x04), ConnState (0x08), Reason (0x10), UseStateNoPrefix (0x20), All (0x1f), AllExceptPartID (0x3c)
        /// </summary>
        [Flags]
        public enum ToStringSelect : int
        {
            None = 0x00,
            PartID = 0x01,
            UseState = 0x02,
            ActionName = 0x04,
            ConnState = 0x08,
            Reason = 0x10,
            UseStateNoPrefix = 0x20,

            All = (PartID | UseState | ActionName | ConnState | Reason),
            AllForPart = (UseStateNoPrefix | ActionName | ConnState | Reason),
        }

		#endregion
    }

    public static partial class ExtensionMethods
    {
        public static bool Equals(this IBaseState thisIBS, IBaseState other, bool compareTimeStamps = true)
        {
            if (Object.ReferenceEquals(thisIBS, other))
                return true;

            if (thisIBS == null || other == null)
                return false;

            if (thisIBS.Equals(other))
                return true;

            if (compareTimeStamps)
                return false;

            return (thisIBS.IsSimulated == other.IsSimulated
                && thisIBS.IsPrimaryPart == other.IsPrimaryPart
                && thisIBS.UseState == other.UseState
                && thisIBS.ConnState == other.ConnState
                && thisIBS.ActionName == other.ActionName
                && thisIBS.Reason == other.Reason
                // && thisIBS.TimeStamp == rhs.TimeStamp
                );
        }
    }

	#endregion

    //-----------------------------------------------------------------
    #region SimplePartBaseBehavior, SimplePartBaseSettings, and SimplePartBase

    /// <summary>
    /// Defines the behavior options that can be select at the SimplePartBase level
    /// <para/>None (0x00), TreatPartAsBusyWhenQueueIsNotEmpty (0x01), TreatPartAsBusyWhenInternalPartBusyCountIsNonZero (0x02), All (0x03)
    /// </summary>
    [Flags]
    public enum SimplePartBaseBehavior : int
    {
        /// <summary>Default, placeholder value [0x00]</summary>
        None = 0x00,

        /// <summary>When present, this flag indicates that the part should be busy whenever any action queue is non-empty [0x01]</summary>
        TreatPartAsBusyWhenQueueIsNotEmpty = 0x01,

        /// <summary>When present, this flag indicates that the part should be busy whenever its internal part busy counter is non-zero [0x02]</summary>
        TreatPartAsBusyWhenInternalPartBusyCountIsNonZero = 0x02,

        /// <summary>(TreatPartAsBusyWhenQueueIsNotEmpty | TreatPartAsBusyWhenInternalPartBusyCountIsNonZero) [0x03]</summary>
        All = (TreatPartAsBusyWhenQueueIsNotEmpty | TreatPartAsBusyWhenInternalPartBusyCountIsNonZero),
    }

    /// <summary>
    /// This structure contains all of the "settings" values that are used by SimplePartBase's implementation.
    /// This structure is typically used by derived objects to comparmentalize and simplify how they configure their 
    /// base class.  This will also allow this object to provide a variety of static preconfigured settings values.
    /// It is also expected to simplify addition of new settings in the future
    /// </summary>
    public struct SimplePartBaseSettings
    {
        /// <summary>When non-null, this may be used to specify the initial instance log gate used by the part's default logger.  This will be ignored when using any SimplePartBase constructor where the caller provides the logger to use.</summary>
        public Logging.LogGate ? InitialInstanceLogGate { get; set; }

        /// <summary>When non-null, this may be used to specify the log group name to be used by the part's default logger.  This will be ignored when using any SimplePartBase constructor where the caller provides the logger to use.</summary>
        public string LogGroupName { get; set; }

        /// <summary>Defines the behavior characteristics that will be enabled for a part</summary>
        public SimplePartBaseBehavior SimplePartBaseBehavior { get; set; }

        /// <summary>
        /// Set this to be non-null to enable BaseState publication via IVA.  
        /// When this is set to the empty string, the IVA name will be derived from the PartID as [PartID].BaseState
        /// When this is neither null nor empty then the IVA name will be the given string value.
        /// <para/>Defaults to string.Empty to enable publication using the default name.
        /// </summary>
        public string BaseStatePublicationValueName { get { return baseStatePublicationValueName; } set { baseStatePublicationValueName = value; baseStatePublicationValueNameHasBeenSet = true; } }

        private string baseStatePublicationValueName;
        private bool baseStatePublicationValueNameHasBeenSet;

        /// <summary>
        /// Defines the IValuesInterconnection instance that will be used by this PartBase class when creating base class level IValueAccessor objects.
        /// When null (the default value) the part will use the default Modular.Interconnect.Values.Values.Instance.
        /// <para/>publically settable (for use in initializers), getter is protected.
        /// </summary>
        public IValuesInterconnection PartBaseIVI { get; set; }

        /// <summary>
        /// Used to specify settings in relation to logging options for this part.
        /// </summary>
        public LoggingOptionSelect LoggingOptionSelect { get; set; }

        /// <summary>
        /// This method is called during Settings assignment.  It applies any required settings changes of struct default values.
        /// <para/>if the BaseStatePublicationValueName property has not been explicitly set, it will be set to string.Empty.
        /// </summary>
        public SimplePartBaseSettings SetupForUse()
        {
            if (!baseStatePublicationValueNameHasBeenSet)
                baseStatePublicationValueName = string.Empty;

            return this;
        }

        /// <summary>
        /// returns a constructor default SimpleParstBaseSettings value.
        /// <para/>LoggingOptionSelect = LoggingOptionSelect.OldXmlishStyle
        /// </summary>
        public static SimplePartBaseSettings DefaultVersion0 
        { 
            get 
            {
                return new SimplePartBaseSettings() 
                { 
                    LoggingOptionSelect = LoggingOptionSelect.OldXmlishStyle, 
                }; 
            } 
        }

        /// <summary>
        /// returns te first non-constructor default SimpleParstBaseSettings value (established under MosaicLibCS 0.1.6.0):
        /// <para/>SimplePartBaseBehavior = SimplePartBaseBehavior.All (TreatPartAsBusyWhenQueueIsNotEmpty | TreatPartAsBusyWhenInternalPartBusyCountIsNonZero),
        /// </summary>
        public static SimplePartBaseSettings DefaultVersion1 
        { 
            get 
            { 
                return new SimplePartBaseSettings() 
                { 
                    SimplePartBaseBehavior = SimplePartBaseBehavior.All,
                }; 
            } 
        }
    }

    /// <summary>
    /// Flag enumeration used to select specific logging options.
    /// <para/>None (0x00), OldXmlishStyle (0x01)
    /// </summary>
    [Flags]
    public enum LoggingOptionSelect : int
    {
        /// <summary>Default, placeholder (0x00)</summary>
        None = 0x00,

        /// <summary>Selects use of older Xmlish style of these messages (0x01)</summary>
        OldXmlishStyle = 0x01,
    }


    /// <summary>Standard extension methods wrapper class/namespace</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given SimplePartBaseBehavior value has the indicated flag set (present)
        /// </summary>
        public static bool CheckFlag(this SimplePartBaseSettings settings, SimplePartBaseBehavior checkFlag)
        {
            return ((settings.SimplePartBaseBehavior & checkFlag) == checkFlag);
        }

        /// <summary>
        /// SimplePartBaseSettings content builder helper extension method.
        /// </summary>
        public static SimplePartBaseSettings Build(this SimplePartBaseSettings settings, 
                                                    SimplePartBaseBehavior? simplePartBaseBehavior = null, 
                                                    string baseStatePublicationValueName = null, 
                                                    bool setBaseStatePublicationValueNameToNull = false, 
                                                    IValuesInterconnection partBaseIVI = null,
                                                    LoggingOptionSelect ? loggingOptionSelect = null
                                                    )
        {
            if (simplePartBaseBehavior.HasValue)
                settings.SimplePartBaseBehavior = simplePartBaseBehavior.Value;

            if (baseStatePublicationValueName != null)
                settings.BaseStatePublicationValueName = baseStatePublicationValueName;

            if (setBaseStatePublicationValueNameToNull)
                settings.BaseStatePublicationValueName = null;

            if (partBaseIVI != null)
                settings.PartBaseIVI = partBaseIVI;

            if (loggingOptionSelect != null)
                settings.LoggingOptionSelect = loggingOptionSelect ?? default(LoggingOptionSelect);

            return settings;
        }
    }


    /// <summary>
    /// This class is a utility base class for objects that implement the IPartBase interface.  
    /// This class instantiates and gives derived classes use of a Logger via the Log property.  
    /// This class also implements all of the public properties and base protected utility methods that are used to implement and maintain the 
    /// Part's BaseState property including allowing clients to observe and/or subscribe to it.
    /// <para/>This class is derived from <see cref="PartBaseBase"/> which is derived from <see cref="DisposableBase"/> which requires that 
    /// derived class (eventually) implement abstract Dispose(DisposeType) method.
    /// </summary>
    public abstract class SimplePartBase : PartBaseBase
    {
        #region Construction and Destruction

        /// <summary>Protected constructor: derived class specifies only the PartID, PartType will be automatically derived from the class name of the derived type.</summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="basicLogger">Optionally gives the <see cref="Logging.IBasicLogger"/> instance that the part shall use for logging.  Part will use new Logging.Logger(partID) if this is null.</param>
        /// <param name="initialSettings">Optionally gives the SimplePartBaseSettings that will be used to configure this part's behavior.  Part will use default values if this is null.</param>
        protected SimplePartBase(string partID, Logging.IBasicLogger basicLogger = null, SimplePartBaseSettings? initialSettings = null) 
            : this(partID, new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(), basicLogger: basicLogger, initialSettings: initialSettings) 
        { }

        /// <summary>Protected constructor: derived class specifies the PartID, PartType, and IBasicLogger instance to be used</summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        /// <param name="basicLogger">Optionally gives the <see cref="Logging.IBasicLogger"/> instance that the part shall use for logging.  Part will use new Logging.Logger(partID) if this is null.</param>
        /// <param name="initialSettings">Optionally gives the SimplePartBaseSettings that will be used to configure this part's behavior.  Part will use default values if this is null.</param>
        protected SimplePartBase(string partID, string partType, Logging.IBasicLogger basicLogger = null, SimplePartBaseSettings? initialSettings = null)
            : base(partID, partType)
        {
            if (initialSettings != null)
                settings = initialSettings.GetValueOrDefault();

            Log = basicLogger ?? new Logging.Logger(partID, groupName: settings.LogGroupName ?? "", initialInstanceLogGate: settings.InitialInstanceLogGate ?? Logging.LogGate.All);

            BaseStatePublishedNotificationList = new EventHandlerNotificationList<IBaseState>(this);
            BaseStateChangeEmitter = Log.Emitter(Logging.MesgType.Trace);

            privateBaseState.PartID = PartID;
            publishedBaseState.Object = PrivateBaseState;

            settings = settings.SetupForUse();
        }

        #endregion

        #region PaseBaseBase methods and properties that are implemented by this base class

        /// <summary>public property that may be used to obtain a copy of the most recently generated IBaseState for this part.</summary>
        public override IBaseState BaseState { get { return publishedBaseState.VolatileObject; } }      // base class defines this as abstract - use VolatileObject since IBaseState will always be handling boxed copies.

        /// <summary>public property that may be used to gain access to the INotificationObject that is used to manage publication and notification of this part's BaseState.</summary>
        public override INotificationObject<IBaseState> BaseStateNotifier { get { return publishedBaseState; } }    // base class defines this as abstract

        /// <summary>Implementation class may call this method to notify all targets in the BaseStateNotifier's NotificationList without publishing a new base state value</summary>
        protected void NotifyBaseStateNotifier() { publishedBaseState.Notify(); }

        #endregion

        #region Logging related fields and protected properties

        /// <summary>Gives derived objects access to the Logger.ILogger created by this SimpleActivePartBase during construction.</summary>
        /// <remarks>Derived classes are allowed to replace the Log logger with some other instance.</remarks>
        protected Logging.IBasicLogger Log { get; set; }

        #endregion

        #region Operational Settings (and related properties)

        public SimplePartBaseSettings SimplePartBaseSettings { get; protected set; }

        /// <summary>
        /// Settings property gives derived objects access to all of the base class "settings" as a single set.
        /// The use of this property will apply the SetupForUse method to the given value and then replace the use of prior seperate settings values in the SimpleActivePartBase class with the updated given value.
        /// </summary>
        public SimplePartBaseSettings Settings { get { return settings; } protected set { settings = value.SetupForUse(); } }

        /// <summary>
        /// This protected Settings field gives derived objects direct read/write access to the SimplePartBaseSettings storage that is used by the part.  
        /// This may be useful in cases where the derived part wants to be able to make incremental changes to the current Settings without using the standard value object property pattern for incremental changes.
        /// <para/>WARNING: if a derived part replaces the settings using this method, then it must use the SetupForUse settings method on the new value before assigning it to this field.
        /// </summary>
        protected SimplePartBaseSettings settings = new SimplePartBaseSettings();

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-21)")]
        protected bool TreatPartAsBusyWhenQueueIsNotEmpty { get { return settings.CheckFlag(SimplePartBaseBehavior.TreatPartAsBusyWhenQueueIsNotEmpty); } set { settings.SimplePartBaseBehavior = settings.SimplePartBaseBehavior.Set(SimplePartBaseBehavior.TreatPartAsBusyWhenQueueIsNotEmpty, value); } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-21)")]
        protected bool TreatPartAsBusyWhenInternalPartBusyCountIsNonZero { get { return settings.CheckFlag(SimplePartBaseBehavior.TreatPartAsBusyWhenInternalPartBusyCountIsNonZero); } set { settings.SimplePartBaseBehavior = settings.SimplePartBaseBehavior.Set(SimplePartBaseBehavior.TreatPartAsBusyWhenInternalPartBusyCountIsNonZero, value); } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-21)")]
        protected string BaseStatePublicationValueName { get { return settings.BaseStatePublicationValueName; } set { settings.BaseStatePublicationValueName = value; } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-21)")]
        public IValuesInterconnection PartBaseIVI { protected get { return settings.PartBaseIVI; } set { settings.PartBaseIVI = value; } }

        #endregion

        #region BaseState support code - implements BaseState change logging, notification and common set methods

        /// <summary>Gives the customized storage field for the BaseStateChangeEmitter property</summary>
        private Logging.IMesgEmitter stateChangeEmitter = null;

        /// <summary>Defines the emitter that is used for state change event log messages.  Defaults to Log.Emitter(Logging.MesgType.Trace).  When set to null Logging.MesgEmitterImpl.Null is used to emit these messages (into the void).</summary>
        protected Logging.IMesgEmitter BaseStateChangeEmitter { get { return (stateChangeEmitter != null ? stateChangeEmitter : Logging.NullEmitter); } set { stateChangeEmitter = value; } }

        /// <summary>This field actually stores the Part's BaseState</summary>
        private BaseState privateBaseState;
        /// <summary>This protected get/set property gives derived objects access to get and set the privateBaseState field.</summary>
        protected BaseState PrivateBaseState { get { return privateBaseState; } set { privateBaseState = value; } }

        /// <summary>private field which is the interlocked notificatino ref object that is used to publish boxed BaseState values using the IBaseState interface.</summary>
        private InterlockedNotificationRefObject<IBaseState> publishedBaseState = new InterlockedNotificationRefObject<IBaseState>();

        /// <summary>Internal proprty that must be overridden by derived class to allow PublishBaseState to observe if any action queue is non-empty.</summary>
        protected virtual bool AreAllActionQueuesEmpty { get { return true; } }

        /// <summary>protected EventHandlerNotificationList{IBaseState} that may also be used to signal publication of new BaseState values</summary>
        protected Utils.EventHandlerNotificationList<IBaseState> BaseStatePublishedNotificationList = null;

        private IValueAccessor baseStatePublisherIVA = null;

        private void SetupBaseStatePublisherIVAIfNeeded()
        {
            if (baseStatePublisherIVA == null && settings.BaseStatePublicationValueName != null)
            {
                if (settings.BaseStatePublicationValueName.IsNullOrEmpty())
                    settings.BaseStatePublicationValueName = "{0}.BaseState".CheckedFormat(PartID);

                baseStatePublisherIVA = (settings.PartBaseIVI ?? Values.Instance).GetValueAccessor(settings.BaseStatePublicationValueName);
            }
        }

        /// <summary>Generates an updated copy of the base state, publishes it, logs specific transitions in use and/or connection state, and signals this parts base state published notification list.</summary>
        protected void PublishBaseState(string reason)
        {
            SetupBaseStatePublisherIVAIfNeeded();

            IBaseState entryState = publishedBaseState.VolatileObject;
            UseState entryUseState = (entryState != null ? entryState.UseState : UseState.Initial);
            ConnState entryConnState = (entryState != null ? entryState.ConnState : ConnState.Initial);
            string entryActionName = (entryState != null ? entryState.ActionName : String.Empty);

            BaseState baseStateCopy = PrivateBaseState;

            bool pushPartToBusyState = (settings.CheckFlag(SimplePartBaseBehavior.TreatPartAsBusyWhenQueueIsNotEmpty) && !AreAllActionQueuesEmpty)
                                     || (settings.CheckFlag(SimplePartBaseBehavior.TreatPartAsBusyWhenInternalPartBusyCountIsNonZero) && internalPartBusyCounter.VolatileValue != 0)
                                     ;

            if (pushPartToBusyState && baseStateCopy.UseState == UseState.Online)      // map idle state to busy state
                baseStateCopy.UseState = UseState.OnlineBusy;
            else if (entryUseState == UseState.OnlineBusy && baseStateCopy.UseState == UseState.Online)
                baseStateCopy.ActionName = String.Empty;

            IBaseState publishState = baseStateCopy;

            publishedBaseState.Object = publishState;
            if (baseStatePublisherIVA != null)
                baseStatePublisherIVA.Set(publishState);

            bool includeAction = (publishState.ActionName != String.Empty || entryActionName != publishState.ActionName);
            bool useNewStyle = ((settings.LoggingOptionSelect & LoggingOptionSelect.OldXmlishStyle) == 0);

            if (entryUseState != publishState.UseState)
            {
                if (!includeAction)
                {
                    if (useNewStyle)
                        BaseStateChangeEmitter.Emit("UseState changed [state:{0}<-{1} reason:({2})]", publishState.UseState, entryUseState, reason.GenerateEscapedVersion());
                    else
                        BaseStateChangeEmitter.Emit("<PartUseStateChange to='{0}' from='{1}' reason='{2}'/>", publishState.UseState, entryUseState, reason.GenerateQuotableVersion());
                }
                else
                {
                    if (useNewStyle)
                    {
                        if (publishState.ActionName == entryActionName)
                            BaseStateChangeEmitter.Emit("UseState changed [state:{0}<-{1} actionName:({2}) reason:({3})]", publishState.UseState, entryUseState, publishState.ActionName.GenerateEscapedVersion(), reason.GenerateEscapedVersion());
                        else
                            BaseStateChangeEmitter.Emit("UseState changed [state:{0}<-{1} actionName:({2})<-({3}) reason:({4})]", publishState.UseState, entryUseState, publishState.ActionName.GenerateEscapedVersion(), entryActionName.GenerateEscapedVersion(), reason.GenerateEscapedVersion());
                    }
                    else
                        BaseStateChangeEmitter.Emit("<PartUseStateChange to='{0}','{1}', from='{2}','{3}', reason='{4}'/>", publishState.UseState, publishState.ActionName.GenerateQuotableVersion(), entryUseState, entryActionName.GenerateQuotableVersion(), reason.GenerateQuotableVersion());
                }
            }

            if (entryConnState != publishState.ConnState)
            {
                if (useNewStyle)
                    BaseStateChangeEmitter.Emit("ConnState changed [state:{0}<-{1} reason:({2})]", publishState.ConnState, entryConnState, reason.GenerateEscapedVersion());
                else
                    BaseStateChangeEmitter.Emit("<PartConnStateChange to='{0}' from='{1}' reason='{2}'/>", publishState.ConnState, entryConnState, reason.GenerateQuotableVersion());
            }

            if (BaseStatePublishedNotificationList != null)
                BaseStatePublishedNotificationList.Notify(publishState);
        }

        /// <summary>
        /// Local internalPartBusyCounter atomic counter.  This counter may be used by the part to automatically push the use state from Online(Idle) to OnlineBusy whenver this counter is non-zero.
        /// Part specific code is responsible for making certain that this counter returns to zero whenever all busy signaling sources that incremented it have been completed.  
        /// Generally this is most safely safely done with the "using" construct using an object returned by a call to the CreateInternalBusyFlagHolderObject method.
        /// </summary>
        private Utils.AtomicUInt32 internalPartBusyCounter = new AtomicUInt32();

        /// <summary>Increments the internal part busy counter</summary>
        protected void IncrementInternalPartBusyCounter() { IncrementInternalPartBusyCounter(null, null); }

        /// <summary>Increments the internal part busy counter and publishes the base state if the publishReason is neither null nor empty</summary>
        protected void IncrementInternalPartBusyCounter(string publishReason, string startingActionName)
        {
            if (!string.IsNullOrEmpty(startingActionName))
                privateBaseState.ActionName = startingActionName;

            internalPartBusyCounter.Increment();

            if (!String.IsNullOrEmpty(publishReason))
                PublishBaseState(publishReason);
        }

        /// <summary>Decrements the internal part busy counter</summary>
        protected void DecrementInternalPartBusyCounter() { DecrementInternalPartBusyCounter(null); }

        /// <summary>Decrements the internal part busy counter and publishes the base state if the publishReason is neither null nor empty</summary>
        protected void DecrementInternalPartBusyCounter(string publishReason)
        {
            internalPartBusyCounter.Decrement();
            if (!String.IsNullOrEmpty(publishReason))
                PublishBaseState(publishReason);
        }

        /// <summary>
        /// Increments the internal part busy counter and Creates an internal busy flag holder object which is returned.  When the returned object is explicitly disposed it will decrement the internal part busy flag automatically.
        /// Caller is responsible for following an object dispose pattern that will cause the returned object to be disposed of at the correct time.
        /// This variant uses a reason of "StartBusy:" + flagName when first incrementing the busy flag and "EndBusy:" + flagName when the returned object is disposed and the counter is decremented.
        /// </summary>
        protected IDisposable CreateInternalBusyFlagHolderObject(string flagName = null, string startingActionName = null)
        {
            bool haveActionName = !startingActionName.IsNullOrEmpty();
            bool publish = !flagName.IsNullOrEmpty() || haveActionName;

            string startReason = (!publish ? null : (haveActionName ? "Starting:{0} FlagName:{1}" : "StartBusy:{1}").CheckedFormat(startingActionName, flagName));
            string endReason = (!publish ? null : (haveActionName ? "Ending:{0} FlagName:{1}" : "EndBusy:{1}").CheckedFormat(startingActionName, flagName));

            IncrementInternalPartBusyCounter(startReason, startingActionName);

            return new DecrementPartBusyCounterOnDispose(this, endReason);
        }

        /// <summary>
        /// This class provides the local implementation needed with the CreateInternalBusyFlagHolderObject method(s).  
        /// It is passed a SimplePartBase and a Reason on construction.  This class will call the part.DecrementInternalPartBusyCounter method when this object is explicitly disposed.
        /// </summary>
        private class DecrementPartBusyCounterOnDispose : DisposableBase
        {
            public DecrementPartBusyCounterOnDispose(SimplePartBase part, string reason) { Part = part; Reason = reason; }
            public SimplePartBase Part { get; internal set; }
            public string Reason { get; internal set; }

            protected override void Dispose(DisposableBase.DisposeType disposeType)
            {
                if (disposeType == DisposeType.CalledExplicitly && Part != null)
                {
                    Part.DecrementInternalPartBusyCounter(Reason);
                    Part = null;
                }
            }
        }

        /// <summary>Variant SetBaseState which allows caller to set the useState</summary>
        protected void SetBaseState(UseState useState, string reason, bool publish = true)
        {
            privateBaseState.SetState(useState, privateBaseState.ConnState, reason);
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the action name</summary>
        protected void SetBaseState(string actionName, string reason, bool publish = true)
        {
            privateBaseState.ActionName = actionName;
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the useState and identify a specific action name</summary>
        protected void SetBaseState(UseState useState, string actionName, string reason, bool publish = true)
        {
            privateBaseState.SetState(useState, actionName);
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the connState</summary>
        protected void SetBaseState(ConnState connState, string reason, bool publish = true)
        {
            privateBaseState.SetState(privateBaseState.UseState, connState, reason);
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the useState and the commState.  actionName will be set to null/empty.</summary>
        protected void SetBaseState(UseState useState, ConnState connState, string reason, bool publish = true)
        {
            SetBaseState(useState, null, connState, reason, publish);
        }

        /// <summary>Variant SetBaseState which allows caller to set the useState, the action name, and the commState</summary>
        protected void SetBaseState(UseState useState, string actionName, ConnState connState, string reason, bool publish = true)
        {
            privateBaseState.SetState(useState, actionName, connState, reason);
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows the caller to set the BaseState contents to match a value from some other source.</summary>
        protected void SetBaseState(IBaseState rhs, string reason, bool publish = true)
        {
            privateBaseState.SetState(rhs, reason);
            if (publish)
                PublishBaseState(reason);
        }

        #endregion

        #region ToString support

        /// <summary>Debugging and Logging helper</summary>
        public override string ToString()
        {
            return "PartID:[{0}] BaseState:[{1}]".CheckedFormat(PartID, BaseState.ToString(Part.BaseState.ToStringSelect.AllForPart));
        }

        #endregion
    }

    #endregion

    //-----------------------------------------------------------------
}
