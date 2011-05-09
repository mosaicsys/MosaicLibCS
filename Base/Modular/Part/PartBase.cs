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

namespace MosaicLib.Modular.Part
{
	//-----------------------------------------------------------------

	using System;
	using MosaicLib.Utils;
	using MosaicLib.Time;
	using MosaicLib.Modular.Action;

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
		/// <summary>Connection to remote device is not available (rejected or failed while in use)</summary>
		ConnectionFailed,
		/// <summary>Attempt to connect failed.  May automatically transition back to AttemptConnect</summary>
		ConnectFailed,
		/// <summary>Connection has been explicitly stopped</summary>
		Disconnected,
	}

	#endregion

    //------------------------------------
    #region static class for static query methods on UseState and ConnState enum values (used by BaseState)

    public static class BaseStateFcns
    {
        //------------------------------------

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

        public static bool IsBusy(UseState useState)
        {
            return (useState == UseState.OnlineBusy || useState == UseState.AttemptOnline);
        }

        public static bool IsConnecting(ConnState connState)
        {
            return (connState == ConnState.Connecting || connState == ConnState.WaitingForConnect);
        }

        public static bool IsConnected(ConnState connState)
        {
            return (connState == ConnState.Connected);
        }

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
	}

	#endregion

	//-----------------------------------------------------------------
	#region Active part interface

	public interface IBasicAction : IClientFacet { }
	public interface IBoolParamAction : IClientFacetWithParam<bool>, IBasicAction { }
    public interface IStringParamAction : IClientFacetWithParam<string>, IBasicAction { }

	/// <summary>
	/// This interface is implmeneted by all ActiveParts.  
	/// It provides the ability for a client to be notified when the part publishes a new BaseState value.
	/// It provides the ability for the client to create actions that can request tha the part 
	/// Go Online, Go Online and Initialize, or to Go Offline.  It also provides a common interface for a client
	/// to create a service action for the part.  Details of useful/legal Service actions and their functions are 
	/// entirely part dependant
	/// </summary>
	public interface IActivePartBase : IPartBase
	{
		/// <summary>Property gives client access to the part's Guarded Notificdation Object for the part's BaseState property.</summary>
		INotificationObject<IBaseState> BaseStateNotifier { get; }

		/// <summary>Method used to Start the part.  Some parts automatically start whenever a GoOnlineAction is created.</summary>
		void StartPart();

		/// <summary>Method used to explicitly stop the part.  Parts also self stop when they are explicilty Disposed.</summary>
		void StopPart();

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
	#region PartBase class

	/// <summary>
	/// This abstract class is generally used as a base class for all Part classes (active or otherwise).  See SimpleActivePart.
	/// </summary>
	public abstract class PartBase : DisposableBase, IPartBase
	{
		private readonly string partID = string.Empty;
		private readonly string partType = string.Empty;

		#region IPartBase Members

		public string PartID { get { return partID; } }
		public string PartType { get { return partType; } }
		public abstract IBaseState BaseState { get; }

		#endregion

		protected PartBase(string partID, string partType) 
		{
			this.partID = partID;
            this.partType = partType;

			Assert.Condition(!String.IsNullOrEmpty(partID), "PartID is valid");
			Assert.Condition(!String.IsNullOrEmpty(partType), "PartType is valid");
		}

		protected string FmtWin32EC(int win32EC) { return Utils.EC.FmtWin32EC(PartID, win32EC); }
		protected string FmtStdEC(string errorStr) { return Utils.EC.FmtStdEC(PartID, errorStr); }
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
		private QpcTimeStamp timeStamp;
        private string actionName;

		#endregion

		#region IBaseState interface

        public bool IsSimulated { get; private set; }   // true if this part is simulated
		public bool IsPrimaryPart { get; private set; } // true if this part represents the master interface to a part which supports multiple interfaces
		public UseState UseState { get { return useState; } set { useState = value; timeStamp.SetToNow(); } }
		public ConnState ConnState { get { return connState; } set { connState = value; timeStamp.SetToNow(); } }
		public QpcTimeStamp TimeStamp { get { return timeStamp; } }
        public string ActionName { get { return Utils.Fcns.MapNullToEmpty(actionName); } set { actionName = value; timeStamp.SetToNow(); } }

        public bool IsOnline { get { return BaseStateFcns.IsOnline(UseState); } }
        public bool IsBusy { get { return BaseStateFcns.IsBusy(UseState); } }

        public bool IsConnecting { get { return BaseStateFcns.IsConnecting(ConnState); } }
        public bool IsConnected { get { return BaseStateFcns.IsConnected(ConnState); } }
        public bool IsConnectedOrConnecting { get { return (BaseStateFcns.IsConnectedOrConnecting(ConnState)); } }

		#endregion

		#region public methods

		static private IBaseState noneState = new BaseState();

		public static IBaseState None { get { return noneState; } }

		public BaseState(bool isPrimaryPart) : this() { IsPrimaryPart = isPrimaryPart; }
		public BaseState(bool isSimulated, bool isPrimaryPart) : this(isPrimaryPart) { IsSimulated = isSimulated; }
        public BaseState(BaseState rhs) : this() { IsSimulated = rhs.IsSimulated; IsPrimaryPart = rhs.IsPrimaryPart; useState = rhs.useState; connState = rhs.connState; timeStamp = rhs.timeStamp; actionName = rhs.actionName; }

		public void SetSimulated(bool online) { SetSimulated(online, true); }
		public void SetSimulated(bool online, bool primary)
		{
			IsSimulated = true;
			IsPrimaryPart = primary;
			SetState(online ? UseState.Online : UseState.Initial, ConnState.NotApplicable);
		}

		public void SetState(UseState useState, ConnState connState)
		{
			this.useState = useState;
			this.connState = connState;
			timeStamp.SetToNow();
		}

        public void SetState(UseState useState, string actionName)
        {
            this.useState = useState;
            this.actionName = actionName;
            timeStamp.SetToNow();
        }

        public void SetState(UseState useState, string actionName, ConnState connState)
        {
            this.useState = useState;
            this.actionName = actionName;
            this.connState = connState;
            timeStamp.SetToNow();
        }

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

        //------------------------------------
    }

	#endregion

	//-----------------------------------------------------------------
}
