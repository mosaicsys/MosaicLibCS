//-------------------------------------------------------------------
/*! @file PartBase.cs
 * @brief This file contains common definitions, interface and base classes that are used to define the common characteristics of Parts in this library.
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
using System;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular.Action;

namespace MosaicLib.Modular.Part
{
	//-----------------------------------------------------------------

	//------------------------------------
	#region enums UseState and ConnState

	/// <summary>Generic summary state for current usability of a part</summary>
	/// <remarks>
	/// This enum provides a set of state codes that are helpfull in determining if a Part is usable and if not, then why, particularly whether the part is online or not.
	/// All Parts support a UseState but they may only use the specific subset of the defined state codes that are relevant for that part and its capabilities.
	/// </remarks>
	public enum UseState
	{
		/// <summary>no valid value has been given or UseState is not supported by this part</summary>
		Undefined = 0,
		/// <summary>part has not been started</summary>
		Initial,
		/// <summary>part is attempting to go online (either automatically after start of by request)</summary>
		AttemptOnline,
		/// <summary>part is online but it requires initialization before it can be used normally.</summary>
		OnlineUnintialized,
		/// <summary>part is online</summary>
		Online,
		/// <summary>part is online and is performing some action</summary>
		OnlineBusy,
		/// <summary>part is online but is not functioning correctly</summary>
		OnlineFailure,
		/// <summary>part was explicitly told to go offline</summary>
		Offline,
		/// <summary>part failed its attempt to go online</summary>
		AttemptOnlineFailed,
		/// <summary>part had an urecoverable error (or exception recovery) that forced it to the offline state - may try to AttemptOnline depending on configuration</summary>
		FailedToOffline,
		/// <summary>part has been shutdown prior to application exit.</summary>
		Shutdown,
	}

	/// <summary>Generic summary state for current connection state of part for parts that support a single connection.</summary>
	/// <remarks>
	/// This enum defines a set of state codes that are helpfull in determining if a Part is connected to some related entity (or not), especially when the Part
	/// either implements the connection itself or depends on such a connection for it correct operation.  All parts support a ConnState but they may only use a 
	/// specific subset of the defined state codes that are relevant for that part and its specific operation.
	/// </remarks>
	public enum ConnState
	{
		/// <summary>no valid value has been given or ConnState is not supported by this part</summary>
		Undefined = 0,
		/// <summary>Either stays here until device has been put online explicitly or attempts autoOnline if appropriate</summary>
		Initial,
		/// <summary>this part does not (directly) support any known connection</summary>
		NotApplicable,
		/// <summary>the part is connecting (has initiated a connection but the connection is not complete and has not failed)</summary>
		Connecting,
		/// <summary>the part is waiting for another party to try to connect</summary>
		WaitingForConnect,
		/// <summary>Connection to remote device is believed to be available and/or operating correctly</summary>
		Connected,
        /// <summary>Connection to remote device is not available (rejected or failed while in use).  State supports automatic reconnection attempt</summary>
		ConnectionFailed,
		/// <summary>Attempt to connect failed.  State supports automatic reconnection attempt</summary>
		ConnectFailed,
		/// <summary>Connection has been explicitly stopped</summary>
		Disconnected,
        /// <summary>Connection has been explicitly stopped by the other end</summary>
        DisconnectedByOtherEnd,
    }

	#endregion

    //------------------------------------
    #region static class for static query methods on UseState and ConnState enum values (used by BaseState)

    /// <summary>
    /// Static utility method class used in relation to UseState and ConnState enums
    /// </summary>
    public static class BaseStateFcns
    {
        //------------------------------------

        /// <summary>Returns true if the given UseState value is any of the Online states.</summary>
        public static bool IsOnline(UseState useState)
        {
            switch (useState)
            {
                case UseState.OnlineUnintialized:
                case UseState.Online:
                case UseState.OnlineBusy:
                case UseState.OnlineFailure:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Returns true if the given UseState value is any of the Busy states (OnlineBusy and AttemptOnline)</summary>
        public static bool IsBusy(UseState useState)
        {
            return (useState == UseState.OnlineBusy || useState == UseState.AttemptOnline);
        }

        /// <summary>Returns true if the given ConnState value is any of the states that represent an active connection attempt being in progress (Conneting, WaitingForConnect)</summary>
        public static bool IsConnecting(ConnState connState)
        {
            return (connState == ConnState.Connecting || connState == ConnState.WaitingForConnect);
        }

        /// <summary>Returns true if the given ConnState value is any of the connected states (Connected)</summary>
        public static bool IsConnected(ConnState connState)
        {
            return (connState == ConnState.Connected);
        }

        /// <summary>Returns true if the given ConnState value is IsConnected or is IsConnecting</summary>
        public static bool IsConnectedOrConnecting(ConnState connState)
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
	public interface IBaseState
	{
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

		/// <summary>reports the timestamp at which the new contents or state object was generated.</summary>
		QpcTimeStamp TimeStamp { get; }

		/// <summary>summary property reports true if the UseState is in an Online state</summary>
		bool IsOnline { get; }
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
        bool IsEqualTo(IBaseState rhs);
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
		/// <summary>Method used to Start the part.  Some parts automatically start whenever a GoOnlineAction is created.</summary>
		void StartPart();

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

		/// <summary>Creates a Service Action and preinitializes its parameter to the given value.  All service action functionality is Part specific.</summary>
		IStringParamAction CreateServiceAction(string serviceName);
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

        /// <summary>
        /// Protected base class constructor: initializes the PartID and PartType fields
        /// </summary>
        /// <param name="partID">This gives the name that the part will carry</param>
        /// <param name="partType">This gives the type that the part will carry</param>
		protected PartBaseBase(string partID, string partType) 
		{
			PartID = partID;
            PartType = partType;

			Asserts.CheckIfConditionIsNotTrue(!String.IsNullOrEmpty(PartID), "PartID is valid");
			Asserts.CheckIfConditionIsNotTrue(!String.IsNullOrEmpty(PartType), "PartType is valid");
		}

        // NOTE: Dispose specific behavior related to the use of the Explicit Dispose Action and the use of the AddExplicitDipsoseAction method have been moved to
        //  DisposeableBase class to support more common use of this delegate list based approach to disposal.

        /// <summary>Protected utility method.  Returns the result of calling <code>Utils.EC.FmtWin32EC(PartID, win32EC);</code></summary>
		protected string FmtWin32EC(int win32EC) { return Utils.EC.FmtWin32EC(PartID, win32EC); }
        /// <summary>Protected utility method.  Returns the result of calling <code>Utils.EC.FmtStdEC(PartID, errorStr);</code></summary>
        protected string FmtStdEC(string errorStr) { return Utils.EC.FmtStdEC(PartID, errorStr); }
        /// <summary>Protected utility method.  Returns the result of calling <code>Utils.EC.FmtStdEC(PartID, errorCode, errorStr);</code></summary>
        protected string FmtStdEC(int errorCode, string errorStr) { return Utils.EC.FmtStdEC(PartID, errorCode, errorStr); }
	};

	#endregion

	//-----------------------------------------------------------------
	#region BaseState struct

	/// <summary>
	/// This struct is used by most types of Part as a storage container for the part's base state.  This struct also implements the IBaseState interface
	/// and may be used with some other storage container to service the part's BaseState property.
	/// </summary>
	public struct BaseState : IBaseState
	{
		#region private fields

		private UseState useState;
		private ConnState connState;
        private string actionName;
        private QpcTimeStamp timeStamp;

		#endregion

		#region IBaseState interface

        /// <summary>return true if the part is simulated</summary>
        public bool IsSimulated { get; private set; }
        /// <summary>return true if the part is a primary part.  secondary parts are parts that provide secondary functional facade on a common underlying part but where the secondary facade should not be used to manage the part's online/offline state or to start or stop it.</summary>
        public bool IsPrimaryPart { get; private set; } // true if this part represents the master interface to a part which supports multiple interfaces
        /// <summary>reports the UseState of the part at the time the client obtained this state object</summary>
        public UseState UseState { get { return useState; } set { useState = value; timeStamp.SetToNow(); } }
        /// <summary>reports the ConnState of the part at the time the client obtained this state object</summary>
        public ConnState ConnState { get { return connState; } set { connState = value; timeStamp.SetToNow(); } }
        /// <summary>reports the, possibly empty, name of any action that part is currently performing</summary>
        public string ActionName { get { return Utils.Fcns.MapNullToEmpty(actionName); } set { actionName = value; timeStamp.SetToNow(); } }
        /// <summary>reports the timestamp at which the new contents or state object was generated.</summary>
        public QpcTimeStamp TimeStamp { get { return timeStamp; } }

        /// <summary>summary property reports true if the UseState is in an Online state</summary>
        public bool IsOnline { get { return BaseStateFcns.IsOnline(UseState); } }
        /// <summary>summary property reports true if the UseState indicates that the device is busy.  Details of functionality depends on internal part behavior.</summary>
        public bool IsBusy { get { return BaseStateFcns.IsBusy(UseState); } }

        /// <summary>summary property reports true if the ConnState indicates that the part is in the process of making a connection.</summary>
        public bool IsConnecting { get { return BaseStateFcns.IsConnecting(ConnState); } }
        /// <summary>summary property reports true if the ConnState indicates that the part's connection (if any) is connected.</summary>
        public bool IsConnected { get { return BaseStateFcns.IsConnected(ConnState); } }
        /// <summary>summary property reports true if the ConnState indicates that the part's connection is connected or is making a connection</summary>
        public bool IsConnectedOrConnecting { get { return (BaseStateFcns.IsConnectedOrConnecting(ConnState)); } }

        /// <summary>returns true if either the UseState or the ConnState are not at their Undefined value.  Typically this means that the part has not be started or that it has not generated its initial value.</summary>
        public bool IsDefined { get { return (UseState != UseState.Undefined || ConnState != ConnState.Undefined); } }

        /// <summary>Returns true if the given rhs is non-null and if the contents of this and the given rhs IBaseState are equal to each other.</summary>
        public bool IsEqualTo(IBaseState rhs)
        {
            return (rhs != null
                && IsSimulated == rhs.IsSimulated
                && IsPrimaryPart == rhs.IsPrimaryPart
                && UseState == rhs.UseState
                && ConnState == rhs.ConnState
                && ActionName == rhs.ActionName
                && TimeStamp == rhs.TimeStamp
                );
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
        public BaseState(IBaseState rhs) 
            : this()
        {
            IsSimulated = rhs.IsSimulated;
            IsPrimaryPart = rhs.IsPrimaryPart;
            useState = rhs.UseState;
            connState = rhs.ConnState;
            actionName = rhs.ActionName;
            timeStamp = rhs.TimeStamp;
        }

        /// <summary>Sets the part as Simulated and possibly puts it in an Online state.</summary>
        public BaseState SetSimulated(bool online) 
        { 
            return SetSimulated(online, true); 
        }

        /// <summary>Sets the part as Simulator and allows it to be put in an Online state and/or to have its IsPrimaryPart property set.</summary>
        public BaseState SetSimulated(bool online, bool primary)
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

        /// <summary>Sets the UseState and ActionName and sets the contained TimeStamp to Now.</summary>
        public BaseState SetState(UseState useState, string actionName)
        {
            this.useState = useState;
            this.actionName = actionName;
            timeStamp.SetToNow();
            return this;
        }

        /// <summary>Sets the UseState, the ActionName and the ConnState parameters and sets the TimeStamp to Now.</summary>
        public BaseState SetState(UseState useState, string actionName, ConnState connState)
        {
            this.useState = useState;
            this.actionName = actionName;
            this.connState = connState;
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

        /// <summary>Provides a print/log/debug suitable string representation of the contents of this state object.</summary>
        public override string ToString()
		{
            bool includeActionName = (IsBusy && (ActionName != String.Empty));
            if (connState == ConnState.NotApplicable)
            {
                if (includeActionName)
                    return Utils.Fcns.CheckedFormat("use:{0}, action:{1}", UseState, ActionName);
                else
                    return Utils.Fcns.CheckedFormat("use:{0}", UseState);
            }
            else
            {
                if (includeActionName)
                    return Utils.Fcns.CheckedFormat("use:{0}, action:{1}, conn:{2}, ", UseState, ActionName, ConnState);
                else
                    return Utils.Fcns.CheckedFormat("use:{0}, conn:{1}", UseState, ConnState);
            }
		}

		#endregion
    }

	#endregion

    //-----------------------------------------------------------------
    #region SimplePartBase

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
        protected SimplePartBase(string partID) : this(partID, new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString()) { }

        /// <summary>Protected constructor: derived class specifies the PartID and PartType to be used</summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        protected SimplePartBase(string partID, string partType) : this(partID, partType, new Logging.Logger(partID)) { }

        /// <summary>Protected constructor: derived class specifies the PartID, PartType, and IBasicLogger instance to be used</summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        /// <param name="basicLogger">Gives the <see cref="Logging.IBasicLogger"/>instance that the part shall use for logging.</param>
        protected SimplePartBase(string partID, string partType, Logging.IBasicLogger basicLogger)
            : base(partID, partType)
        {
            Log = basicLogger;

            BaseStatePublishedNotificationList = new EventHandlerNotificationList<IBaseState>(this);
            BaseStateChangeEmitter = Log.Emitter(Logging.MesgType.Trace);

            publishedBaseState.Object = PrivateBaseState;
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
        
        /// <summary>protected EventHandlerNotificationList{IBaseState} that may also be used to signal publication of new BaseState values</summary>
        protected Utils.EventHandlerNotificationList<IBaseState> BaseStatePublishedNotificationList = null;

        /// <summary>If this property is set to true, the part should indicate that it is busy whenever any action queue is non-empty.</summary>
        protected bool TreatPartAsBusyWhenQueueIsNotEmpty { get; set; }
        /// <summary>Internal proprty that must be overridden by derived class to allow PublishBaseState to observe if any action queue is non-empty.</summary>
        protected virtual bool AreAllActionQueuesEmpty { get { return true; } }

        /// <summary>If this property is set to true, the part should indicate that it is busy whenever its internal part busy counter is non-zero</summary>
        protected bool TreatPartAsBusyWhenInternalPartBusyCountIsNonZero { get; set; }

        /// <summary>Generates an updated copy of the base state, publishes it, logs specific transitions in use and/or connection state, and signals this parts base state published notification list.</summary>
        protected void PublishBaseState(string reason)
        {
            IBaseState entryState = publishedBaseState.VolatileObject;
            UseState entryUseState = (entryState != null ? entryState.UseState : UseState.Initial);
            ConnState entryConnState = (entryState != null ? entryState.ConnState : ConnState.Initial);
            string entryActionName = (entryState != null ? entryState.ActionName : String.Empty);

            BaseState baseStateCopy = PrivateBaseState;

            bool pushPartToBusyState = (TreatPartAsBusyWhenQueueIsNotEmpty && !AreAllActionQueuesEmpty)
                                     || (TreatPartAsBusyWhenInternalPartBusyCountIsNonZero && internalPartBusyCounter.VolatileValue != 0)
                                     ;

            if (pushPartToBusyState && baseStateCopy.UseState == UseState.Online)      // map idle state to busy state
                baseStateCopy.UseState = UseState.OnlineBusy;
            else if (entryUseState == UseState.OnlineBusy && baseStateCopy.UseState == UseState.Online)
                baseStateCopy.ActionName = String.Empty;

            IBaseState publishState = baseStateCopy;

            publishedBaseState.Object = publishState;

            bool includeAction = (publishState.ActionName != String.Empty || entryActionName != publishState.ActionName);

            if (entryUseState != publishState.UseState)
            {
                if (!includeAction)
                    BaseStateChangeEmitter.Emit("<PartUseStateChange to=\"{0}\" from=\"{1}\" reason=\"{2}\"/>", publishState.UseState, entryUseState, reason);
                else
                    BaseStateChangeEmitter.Emit("<PartUseStateChange to=\"{0}\",\"{1}\", from=\"{2}\",\"{3}\", reason=\"{2}\"/>", publishState.UseState, publishState.ActionName, entryUseState, entryActionName, reason);
            }
            if (entryConnState != publishState.ConnState)
                BaseStateChangeEmitter.Emit("<PartConnStateChange to=\"{0}\" from=\"{1}\" reason=\"{2}\"/>", publishState.ConnState, entryConnState, reason);

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
        /// </summary>
        protected IDisposable CreateInternalBusyFlagHolderObject() { return CreateInternalBusyFlagHolderObject(null, null); }

        /// <summary>
        /// Increments the internal part busy counter and Creates an internal busy flag holder object which is returned.  When the returned object is explicitly disposed it will decrement the internal part busy flag automatically.
        /// Caller is responsible for following an object dispose pattern that will cause the returned object to be disposed of at the correct time.
        /// This variant uses a reason of "StartBusy:" + flagName when first incrementing the busy flag and "EndBusy:" + flagName when the returned object is disposed and the counter is decremented.
        /// </summary>
        protected IDisposable CreateInternalBusyFlagHolderObject(string flagName, string startingActionName)
        {
            bool haveActionName = !string.IsNullOrEmpty(startingActionName);
            bool publish = !string.IsNullOrEmpty(flagName) || haveActionName;

            string startReason = (!publish ? null : (!haveActionName ?  ("StartBusy:" + flagName) : Utils.Fcns.CheckedFormat("Starting:{0} FlagName:{1}", startingActionName, flagName)));
            string endReason = (!publish ? null : (!haveActionName ?  ("EndBusy:" + flagName) : Utils.Fcns.CheckedFormat("Ending:{0} FlagName:{1}", startingActionName, flagName)));

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
        protected void SetBaseState(UseState useState, string reason, bool publish)
        {
            privateBaseState.UseState = useState;
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the action name</summary>
        protected void SetBaseState(string actionName, string reason, bool publish)
        {
            privateBaseState.ActionName = actionName;
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the useState and identify a specific action name</summary>
        protected void SetBaseState(UseState useState, string actionName, string reason, bool publish)
        {
            privateBaseState.SetState(useState, actionName);
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the commState</summary>
        protected void SetBaseState(ConnState commState, string reason, bool publish)
        {
            privateBaseState.ConnState = commState;
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows caller to set the useState and the commState</summary>
        protected void SetBaseState(UseState useState, ConnState connState, string reason, bool publish)
        {
            SetBaseState(useState, null, connState, reason, publish);
        }

        /// <summary>Variant SetBaseState which allows caller to set the useState, the action name, and the commState</summary>
        protected void SetBaseState(UseState useState, string actionName, ConnState connState, string reason, bool publish)
        {
            privateBaseState.SetState(useState, actionName, connState);
            if (publish)
                PublishBaseState(reason);
        }

        /// <summary>Variant SetBaseState which allows the caller to set the BaseState contents to match a value from some other source.</summary>
        protected void SetBaseState(IBaseState rhs, string reason, bool publish)
        {
            privateBaseState.SetState(rhs);
            if (publish)
                PublishBaseState(reason);
        }

        #endregion
    }

    #endregion

    //-----------------------------------------------------------------
}
