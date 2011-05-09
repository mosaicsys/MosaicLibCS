//-------------------------------------------------------------------
/*! @file ClientFacet.cs
 * @brief This file contains the definitions and classes that are used to define the Client side, or facet, of the Modular Action concept which is implemented in this library.
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

//-------------------------------------------------
/// @page MosaicLib_ModularActionsPage Modular Actions
/// 
/// This page presents the preliminary documentation for the Modular Action portion of the MosaicLib.  This functionality is found under the
/// MosaicLib.Modular namespace which contains sub-namespaces: Action and Part.
/// 
/// @section Terms Terms
/// <list type="bullet">
///		<item>Active Object<br>
///		An Active object is an instance of a class which includes one or more internal threads of execution and which restricts publically accessible method
///		so that they interact with the internal thread or threads in a well defined and thread safe manner.  In most cases public methods in Active Objects are
///		thread safe state accessor properties or are methods that request the object to perform some action by recording the action and rethreading it so that
///		it can be performed by the object's internal thread or threads.  
///		</item>
///		
///		<item>Part<br>
///		A Part is a Passive or Active Object which acts as a component in some system.
///		Parts generally inherit from PartBase (directly or indirectly) and all Parts implment the MosaicLib.Modular.Part.IPartBase inteface.
///		Many Parts are Active and are derived from SimpleActivePart.  Active Parts generally provide public methods that create action objects for the
///		various actions that the Part can perform.
///		</item>
///		
///		<item>Action<br>
///		In its general form, an Action is an operation that a Part can perform.  
///		It can be generated with Parameter values and it may produce a Result as a form of Future Value.  
///		
///		Under this library an Action implies the following major functions:  
///		<list type="table">
///			<item>Actions are generic objects that are created by a Part for use by a client and which are jointly owned by the Part and the Client.</item>
///			<item>Actions have a state, a result code, and may define a non-null parameter value and/or result value.  Access by the client to the parameter and/or result values are only permitted when the action is in a ready or idle state.</item>
///			<item>Actions are started by a client which generally enqueues the action for execution by the part that created it.  Parts dequeue and perform such started actions in whatever order the part desires (fifo or otherwise).  Once the part has completed the action, the client may access its result code and its result value (if any).</item>
///			<item>As such Actions act as a form of FutureValue in that the result code and optional result value are made available to the client after the Action has been completed by the Part.</item>
///		</list>
///		
///		Internally Actions are represented by a set of interfaces including the IClientFacet and IProviderFacet interfaces.  These
///		interfaces define the methods that are exposed by an Action to the client and to the provider (typically the base class of the part itself).  
/// 
///		Under this library the Action's Client Facet provide a number helper methods that may be used to perform common Action related patterns with a minimum of glue code.
///		Finally under this library, an Action object may be reused multiple times by a client, provided that the client honors the state transition diagram for each such created action and starts an action when it is ready and accesses the actions results code and value when it is complete.
///		In addition, the client may discard its reference to an action at any time without risk to the part, even if the action is in progress at the time the client discards its reference.
///		
///		Clients may also request that a started and incomplete action be canceled.  In general parts will honor such a cancel request at their whim and will only check for cancel requests at points in the operation where it is easy to abandon or unwind the operation.  An Action may be successfully completed even if the client has requested that it be canceled.
///		</item>
///		
///		<item>ResultCode<br>
///		All Actions produce a ResultCode which is a string.  In general the empty string indicates success and any other value indicates failure.  If the part wishes to produce a string on some successfull completion of some Action, it should generally use a specific variant of the Action to return the string in the Action's result value rather than its result code.
///		</item>
///		
///		<item>Result FutureValue<br>
///		The term FutureValue refers to a design pattern that is in general use where provider of some asynchronous function gives the client a placeholder for the results of some asynchronous operation that may be used by the client to obtain the results of the opertion once it is complete.
///		In our case, the ResultCode and optional Result Value are considered the FutureValue of each Action from when it is started to when it is complete.
///		</item>
///		
///		<item>Action Implementation<br>
///		Internally actions are implemented using the ActionImpl generic class.  This class provides a number of configurable characteristics and provides implementations for all of the necessary interfaces.
///		Parts generally sub-class this class for specific cases or make use of one of the preexisting sub-classes such as BasicActionImpl or StringActinImpl.
///		</item>
///		
///		<item>Client Facet<br>
///		This term relates to the IClientFacet interface.  It defines the set of properties and methods that an Action object makes available to a client.  Actions are given to clients using this,
///		or a derived, interface so that the client does not have public access to the methods in the Action Implementation object that are used by the Part (and vica versa).
///		</item>
///		
///		<item>Provider Facet<br>
///		This term relates to the IProviderFacet interface.  It defines the set of properties and methods that an Action object makes available to the part.  Actions are given to the parts using this,
///		or a derived, interface so that the part does not have public access to the methods in the Action Implementation object that are used by the client.
///		</item>
///		
///		<item>Queue<br>
///		Most common active parts make use of a single, size limited, ActionQueue.  Normally, when started, an Action enequeue itself in a queue that belongs to the Part that originally created the Action.  Then that part services the queue or queues and performs the Actions that are dequeued from it, initiating them in fifo order.
///		Some parts may wish to make use of more than one ActionQueue.  This allows the client to service each of these queues at different points in the client and to enqueue differnt Actions in to different queues.  By permitting ActionQueues to be sericed in a nested manner,
///		the Part may be able to perform actions on one queue while it is still in the process of performing a single action on an outer queue.  Examples where this may be useful include more generic forms of cancelation or abort requests than can be provided by the existing
///		IClientFacet cancel request and in cases where a Part may make itself available to two differnt types of clients where it may need to be able to perform quick (or Instant) actions for one while performing longer running actions for another, while still supporting a
///		a relatively simple threaded flow of control based execution pattern (rather than state based) for the longer running actions.
///		</item>
/// </list>
/// 
/// @section comments Additional Comments
/// 
/// The usage pattern defined above is loosely derived from various sources including:
/// <list type="table">
///		<item>The ACE Framework and related publications by Douglas C. Schmidt (et. al.)</item>
///		<item>The concept of a placeholder object for a Future Value (The ACE Framework, parasoft.com Threads++ product, DotNet CLR IAsyncResult, etc.)</item>
///		<item>Various web articles on the Active Object Pattern (google "Active Object Pattern")</item>
///		<item>http://www.orocos.org/</item>
/// </list>
//-------------------------------------------------

namespace MosaicLib.Modular.Action
{
	//-------------------------------------------------
	using System;
	using MosaicLib.Utils;
	using MosaicLib.Time;
    using MosaicLib.Modular.Common;

	//-------------------------------------------------
	#region ActionStateCode

	/// <summary>
	///	This enum is used to define the set of states that represent the user's high level view of the progress of an Action.
	///	The normal progression of such states is as follows:
	///	[Complete->]Ready->Started[->Issued]->Complete
	/// Successfully constructed Action objects always start in the Ready state.  When the Action accepts the Start method it
	///	transitions to the Started state.  This state applies while the operation is placed into and later pulled form an Action Queue
	/// and until the Action transitions to the Issued or directly to the Completed state.  Finally Issued Actions transition to the
	///	Completed state once they have been performed.  Transition to the Completed state may be peformed by the Queue or by the
	///	service provider.  The contents of the ResultCode are usually used to determine if the action was successfull or not.
	/// </summary>

	public enum ActionStateCode
	{
		/// <summary>default ctor value for structs</summary>
		Initial = 0,
		/// <summary>state after valid creation by a provider</summary>
		Ready,
		/// <summary>this state covers the operation from once the Start method has committed to enqueueing the operation until it is marked as having been issued.</summary>
		Started,
		/// <summary>provider has accepted this operation and is performing it</summary>
		Issued,
		/// <summary>operation has been completed (successfully or not).</summary>
		Complete,
		/// <summary>should never be in this state - cannot be Started or used</summary>
		Invalid,
	};

	#endregion

	//-------------------------------------------------
	#region IActionState

	///<summary>
	///Define the result from generic Action State accessor propery interface that is provided to the client.  
	///This state combines a state code, a time stamp, a result code string and provides a set of helper properties that can be used to
	///make summary inquires about the state code and result code string.
	///</summary>
	
	public interface IActionState
	{
		/// <summary>The last reported state code for from the Action</summary>
		ActionStateCode StateCode { get; }
		/// <summary>The timestamp taken when the state code was last published.</summary>
		QpcTimeStamp TimeStamp { get; }

		/// <summary>returns error message if !IsComplete, returns actual resultCode string if state IsComplete.</summary>
		string ResultCode { get; }

        /// <summary>true if a client has requested this action to be canceled</summary>
        bool IsCancelRequested { get; }

		// simple state test methods (each matches one state)
		/// <summary>StateCode != Invalid</summary>
		bool IsValid { get; }
		/// <summary>StateCode == Ready</summary>
		bool IsReady { get; }
		/// <summary>StateCode == Started</summary>
		bool IsStarted { get; }
		/// <summary>StateCode == Issued</summary>
		bool IsIssued { get; }
		/// <summary>StateCode == Complete</summary>
		bool IsComplete { get; }

		/// <summary>true if IsReady || IsComplete</summary>
		bool CanStart { get; }
		/// <summary>true if IsStarted or IsIssued</summary>
		bool IsPendingCompletion { get; }

		/// <summary>true if IsComplete and resultCode is not null or empty</summary>
		bool Failed { get; }
		/// <summary>true if IsComplete and resultCode is null or empty</summary>
		bool Succeeded { get; }

        /// <summary>Carries a set of name/value pair objects that have been published along with this state.</summary>
        Common.NamedValueList NamedValues { get; }
	}

	#endregion

	//-------------------------------------------------
	#region IClientFacet, related parameter and result value interfaces, combination interfaces.

	/// <summary>
    /// Basic Client Facet interface.  
    /// Allows client to Start, Wait For Completion, Run, Request Cancel and access the Action's ActionState.  
    /// Also allows client to register a notification item with the notification list.
    /// </summary>
	public interface IClientFacet
	{
		/// <summary>Property gives caller access to the IBasicNotificationList that is signaled each time the action completes.</summary>
		IBasicNotificationList NotifyOnComplete { get; }

        /// <summary>Property gives caller access to the IBasicNotificationList that is signaled each time the action's IActionState is updated.</summary>
        IBasicNotificationList NotifyOnUpdate { get; }

		/// <summary>Starts the action if it is in an Idle state and return string.Empty or returns an error message if the action is not in a state from which it can be started.</summary>
		string Start();

		/// <summary>Waits until the action is complete or the given time limit is reached.  Returns ActionState.ResultCode or suitable string if the action was not complete within the stated time limit.</summary>
		bool WaitUntilComplete(TimeSpan timeLimit);

		/// <summary>Waits until the action is complete.  Returns ActionState.ResultCode</summary>
		string WaitUntilComplete();

		/// <summary>Run the action to completion or until the given time limit is reached.  Returns ActionState.ResultCode or suitable string if the action was not complete within the stated time limit.</summary>
		string Run(TimeSpan timeLimit);

		/// <summary>Run the action to completion and return the ActionState.ResultCode.</summary>
		string Run();

		/// <summary>Invoke this to request that the current action be canceled.  Only meaningfull when action has been successfully started and is not complete.</summary>
		void RequestCancel();

        /// <summary>True if the action cancel request flag has been set by the client or by the target provider.</summary>
        bool IsCancelRequestActive { get; }

		/// <summary>Property gives access to the dynamically updating IActionState for this action</summary>
		IActionState ActionState { get; }
	}

	/// <summary>Properties about storage and use of a parameter value provided by an Action in parallel with the IClientFacet for some Action types.</summary>
	public interface IParamValue<ParamType>
	{
		/// <summary>returns false if ParamValue cannot be assigned</summary>
		bool IsParamValueSettable { get; }
		/// <summary>returns false if ParamValue cannot be assigned</summary>
		bool SetParamValue(ParamType value);
		/// <summary>Get or Set the Param value.  set throws if !IsParamValueSettable</summary>
		ParamType ParamValue { get; set; }
	}

	/// <summary>A non-typed version of a Future Result Value.</summary>
	public interface IFutureResult
	{
		/// <summary>synonym for IsComplete</summary>
		bool IsResultAvailable { get; }
		/// <summary>returns null if !IsResultAvailable, if value was not generated, or if value was explicitly set to null</summary>
		object ResultObject { get; }
	}

	/// <summary>A templatized (generic) Future Result Value which gives typed access to the value.</summary>
	public interface IFutureResult<ResultType> : IFutureResult where ResultType : new()
	{
		/// <summary>return final method produced ResultType if IsComplete or new ResultType() if not</summary>
		ResultType ResultValue { get; }
	}

	/// <summary>Combined interface provides IClientFacet and IParamValue</summary>
	public interface IClientFacetWithParam<ParamType> : IClientFacet, IParamValue<ParamType>
	{
		/// <summary>Variation on IClientFacet.Start which allows caller to set ParamValue and then Start the action</summary>
		string Start(ParamType paramValue);
	}

	/// <summary>Combined interface provides IClientFacet and IFutureResult</summary>
	public interface IClientFacetWithResult<ResultType> : IClientFacet, IFutureResult<ResultType> where ResultType : new()
	{}

	/// <summary>Combined interface provides IClientFacet, IParamValue and IFutureResult</summary>
	public interface IClientFacetWithParamAndResult<ParamType, ResultType> : IClientFacetWithParam<ParamType>, IClientFacetWithResult<ResultType> where ResultType : new()
	{ }

    #endregion

	//-------------------------------------------------
	#region public static partial Helper Fcns class

#if (false)     // for now these helper methods do not appear to be used and were causing ambiguity in resolving the prior MosaicLib.Utils.Fcns class under which ChkFormat is located.

	/// <summary>
	/// This class exists as a pseudo namespace for Action related Helper functions that may be used by clients
	/// </summary>

	public static partial class ActionFcns
	{
		//-------------------------------------------------
		// the following is a set of templatized helper methods

		public static IClientFacetType Run<IClientFacetType>(IClientFacetType p, TimeSpan timeout) where IClientFacetType : IClientFacet
		{
			if (p != null)
				p.Run(timeout);
			return p;
		}

		public static IClientFacetType Run<IClientFacetType>(IClientFacetType p) where IClientFacetType : IClientFacet
		{
			if (p != null)
				p.Run();
			return p;
		}

		public static bool IsComplete(IActionState p)
		{
			return ((p != null) ? p.IsComplete : false);
		}

		public static bool Failed(IActionState p)
		{
			return ((p != null) ? p.Failed : false);
		}

		public static bool Succeeded(IActionState p)
		{
			return ((p != null) ? p.Succeeded : false);
		}

		public static bool GetResult<ResultType>(IClientFacetWithResult<ResultType> p, out ResultType o) where ResultType : new()
		{
			if (p != null && p.IsResultAvailable)
			{
				o = p.ResultValue;
				return true;
			}
			else
			{
				o = new ResultType();
				return false;
			}
		}
	}

#endif

	#endregion

	//-------------------------------------------------
}

//-------------------------------------------------------------------
