//-------------------------------------------------------------------
/*! @file Implementation.cs
 * @brief This file contains the definitions and classes that are used to define the internal Action Implementation objects for the Modular Action portions of this library.
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

namespace MosaicLib.Modular.Action
{
	//-------------------------------------------------
	using System;
	using MosaicLib.Utils;
	using MosaicLib.Time;

	//-------------------------------------------------
	#region ActionLogging

    /// <summary>
    /// This class defines the Logging.MesgType's for each of the standard classes of log messaages that Action objects emit during normal use.
    /// These include Done, Error, State and Update events.
    /// </summary>
    public class ActionLoggingConfig
    {
        public ActionLoggingConfig()
        {
            DoneMesgType = Logging.MesgType.None;
            ErrorMesgType = Logging.MesgType.None;
            StateMesgType = Logging.MesgType.None;
            UpdateMesgType = Logging.MesgType.None;
        }

        public ActionLoggingConfig(Logging.MesgType doneMesgType, Logging.MesgType errorMesgType, Logging.MesgType stateMesgType, Logging.MesgType updateMesgType) 
        {
            DoneMesgType = doneMesgType;
            ErrorMesgType = errorMesgType;
            StateMesgType = stateMesgType;
            UpdateMesgType = updateMesgType;
        }

        public ActionLoggingConfig(ActionLoggingConfig rhs) 
        {
            DoneMesgType = rhs.DoneMesgType;
            ErrorMesgType = rhs.ErrorMesgType;
            StateMesgType = rhs.StateMesgType;
            UpdateMesgType = rhs.UpdateMesgType;
        }

        public Logging.MesgType DoneMesgType { get; protected set; }
        public Logging.MesgType ErrorMesgType { get; protected set; }
        public Logging.MesgType StateMesgType { get; protected set; }
        public Logging.MesgType UpdateMesgType { get; protected set; }

        private static readonly ActionLoggingConfig signif_error_debug_debug = new ActionLoggingConfig(Logging.MesgType.Signif, Logging.MesgType.Error, Logging.MesgType.Debug, Logging.MesgType.Debug);
        private static readonly ActionLoggingConfig info_error_debug_debug = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Error, Logging.MesgType.Debug, Logging.MesgType.Debug);
        private static readonly ActionLoggingConfig info_error_trace_trace = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Error, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig info_info_trace_trace = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Info, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig debug_debug_trace_trace = new ActionLoggingConfig(Logging.MesgType.Debug, Logging.MesgType.Debug, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig trace_trace_trace_trace = new ActionLoggingConfig(Logging.MesgType.Trace, Logging.MesgType.Trace, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig none_none_none_none = new ActionLoggingConfig(Logging.MesgType.None, Logging.MesgType.None, Logging.MesgType.None, Logging.MesgType.None);

        public static ActionLoggingConfig Signif_Error_Debug_Debug { get { return signif_error_debug_debug; } }
        public static ActionLoggingConfig Info_Error_Debug_Debug { get { return info_error_debug_debug; } }
        public static ActionLoggingConfig Info_Error_Trace_Trace { get { return info_error_trace_trace; } }
        public static ActionLoggingConfig Info_Info_Trace_Trace { get { return info_info_trace_trace; } }
        public static ActionLoggingConfig Debug_Debug_Trace_Trace { get { return debug_debug_trace_trace; } }
        public static ActionLoggingConfig Trace_Trace_Trace_Trace { get { return trace_trace_trace_trace; } }
        public static ActionLoggingConfig None_None_Trace_Trace { get { return none_none_none_none; } }
    }

	/// <summary>
	/// This class assists in generating log messages for actions.  
	/// It is typically created by an active part and given to the action for the action to use in emiting log messages.
    /// It also supports copy constructor behavior so that it can be replicated and used for individual actions.
	/// </summary>

	public class ActionLogging
	{
        /// <summary>Copy constructor for use in creating ActionLogging objects for new Action objects: Mesg specified, MesgDetails empty.</summary>
        public ActionLogging(string mesg, ActionLogging copyFrom) : this(mesg, string.Empty, copyFrom.Logger, copyFrom.Config, copyFrom.Done, copyFrom.Error, copyFrom.State, copyFrom.Update) { }
        /// <summary>Copy constructor for use in creating ActionLogging objects for new Action objects: Mesg ane MesgDetails specified.</summary>
        public ActionLogging(string mesg, string mesgDetails, ActionLogging copyFrom) : this(mesg, mesgDetails, copyFrom.Logger, copyFrom.Config, copyFrom.Done, copyFrom.Error, copyFrom.State, copyFrom.Update) { }

        /// <summary>Standard constructor for creating a reference ActionLogging object (from which others are created)</summary>
        public ActionLogging(Logging.IBasicLogger logger, ActionLoggingConfig config)
		{
            this.mesg = String.Empty;
            this.mesgDetail = String.Empty;
            this.logger = logger;
            this.config = config;
            UpdateEmitters();
		}

        protected ActionLogging(string mesg, string mesgDetails, Logging.IBasicLogger logger, ActionLoggingConfig config, Logging.IMesgEmitter doneEmitter, Logging.IMesgEmitter errorEmitter, Logging.IMesgEmitter stateEmitter, Logging.IMesgEmitter updateEmitter)
        {
            this.mesg = mesg;
            this.mesgDetail = mesgDetails;
            this.logger = logger;
            this.config = config;
            this.doneEmitter = doneEmitter;
            this.errorEmitter = errorEmitter;
            this.stateEmitter = stateEmitter;
            this.updateEmitter = updateEmitter;
        }

		string mesg;
		string mesgDetail;
        Logging.IBasicLogger logger;
        volatile ActionLoggingConfig config;
        volatile Logging.IMesgEmitter doneEmitter, errorEmitter, stateEmitter, updateEmitter;

		public string Mesg { get { return mesg; } set { mesg = value; } }
		public string MesgDetail { get { return mesgDetail; } set { mesgDetail = value; } }
        public Logging.IBasicLogger Logger { get { return logger; } set { logger = value; UpdateEmitters(); } }
        public ActionLoggingConfig Config { get { return config; } set { config = value; UpdateEmitters(); } }

		public Logging.IMesgEmitter Done { get { return doneEmitter; } set { doneEmitter = value; } }
        public Logging.IMesgEmitter Error { get { return errorEmitter; } set { errorEmitter = value; } }
        public Logging.IMesgEmitter State { get { return stateEmitter; } set { stateEmitter = value; } }
        public Logging.IMesgEmitter Update { get { return updateEmitter; } set { updateEmitter = value; } }

		public override string ToString()
		{
			if (string.IsNullOrEmpty(mesgDetail))
				return Mesg;
			else
				return Utils.Fcns.CheckedFormat("{0}[{1}]", Mesg, MesgDetail);
		}

        private void UpdateEmitters()
        {
            ActionLoggingConfig capturedConfig = config;

            if (logger != null && capturedConfig != null)
            {
                doneEmitter = logger.Emitter(capturedConfig.DoneMesgType);
                errorEmitter = logger.Emitter(capturedConfig.ErrorMesgType);
                stateEmitter = logger.Emitter(capturedConfig.StateMesgType);
                updateEmitter = logger.Emitter(capturedConfig.UpdateMesgType);
            }
            else
            {
                doneEmitter = Logging.NullEmitter;
                errorEmitter = Logging.NullEmitter;
                stateEmitter = Logging.NullEmitter;
                updateEmitter = Logging.NullEmitter;
            }
        }
	}

	#endregion

	//-------------------------------------------------
	#region IActionState Implementation

    public class ActionStateImplBase : IActionState
    {
        #region Protected constructor(s)

        protected ActionStateImplBase() { }
        protected ActionStateImplBase(IActionState rhs)
        {
            stateCode = rhs.StateCode;
            timeStamp = rhs.TimeStamp;
            resultCode = rhs.ResultCode;
            isCancelRequested = rhs.IsCancelRequested;
            if (rhs.NamedValues != null)
                namedValues = rhs.NamedValues;
        }

        #endregion

        #region Protected fields

        protected ActionStateCode stateCode = ActionStateCode.Initial;
        protected Time.QpcTimeStamp timeStamp = Time.QpcTimeStamp.Zero;
        protected string resultCode = null;
        protected volatile bool isCancelRequested = false;
        protected Common.NamedValueList namedValues = null;

        #endregion

        #region IActionState and corresponding public property set methods

        // principle properties of the state
        public ActionStateCode StateCode
        {
            get { return stateCode; }
            protected set
            {
                stateCode = value;
                timeStamp.SetToNow();
            }
        }

        public Time.QpcTimeStamp TimeStamp { get { return timeStamp; } }

        /// <summary>Reports error message if !IsComplete, reports actual resultCode string if state IsComplete.</summary>
        public string ResultCode
        {
            get
            {
                if (IsComplete)
                    return resultCode;
                else if (!IsValid)
                    return "Error.Action.StateIsNotValid";
                else if (IsReady)
                    return "Error.Action.HasNotBeenStarted";
                else	// treat all of the other states as 
                    return "Error.Action.HasNotCompleted";
            }
        }

        public bool IsCancelRequested { get { return isCancelRequested; } }

        // simple state test methods (each matches one state)
        public bool IsValid { get { return stateCode != ActionStateCode.Invalid; } }
        public bool IsReady { get { return stateCode == ActionStateCode.Ready; } }
        public bool IsStarted { get { return stateCode == ActionStateCode.Started; } }
        public bool IsIssued { get { return stateCode == ActionStateCode.Issued; } }
        public bool IsComplete { get { return stateCode == ActionStateCode.Complete; } }

        // true if StateCode is Ready or Complete
        public bool CanStart { get { return (IsReady || IsComplete); } }

        // true if StateCode is Started or Issued
        public bool IsPendingCompletion { get { return (IsStarted || IsIssued); } }

        public bool Failed { get { return (IsComplete && !string.IsNullOrEmpty(resultCode)); } }
        public bool Succeeded { get { return (IsComplete && string.IsNullOrEmpty(resultCode)); } }

        /// <summary>Carries a set of name/value pair objects that have been published along with this state.</summary>
        public Common.NamedValueList NamedValues { get { return namedValues; } }

        #endregion
    }

    public class ActionStateCopy : ActionStateImplBase
    {
        public ActionStateCopy() : base() { }
        public ActionStateCopy(IActionState rhs) : base(rhs) { }
    }

	///<summary>
	/// Define a class that contains the information necessary to determine the progress and success of a specfic Action.
	/// The object is a struct 
	///</summary>
	
	public class ActionStateImpl : ActionStateImplBase
	{
		#region local methods

		private static void EmitStateChangeMesg(ActionLogging logging, ActionStateCode toState, ActionStateCode fromState, string resultCode)
		{
            Logging.IMesgEmitter emitter = logging.State;
			bool isComplete = (toState == ActionStateCode.Complete);
			bool includeRC = (isComplete || !string.IsNullOrEmpty(resultCode));
			if (isComplete)
				emitter = logging.Done;

			if (includeRC)
				emitter.Emit("<ActionStateChange id=\"{0}\" to=\"{1}\" from=\"{2}\" rc=\"{3}\"/>", logging, toState, fromState, resultCode);
			else
				emitter.Emit("<ActionStateChange id=\"{0}\" to=\"{1}\" from=\"{2}\"/>", logging, toState, fromState);
		}

        private static void EmitNamedValueListUpdateMesg(ActionLogging logging, ActionStateCode state, Common.NamedValueList nvl)
        {
            Logging.IMesgEmitter emitter = logging.Update;

            string nvls = (nvl != null ? nvl.ToString() : "");

            emitter.Emit("<ActionNamedValueListUpdate id=\"{0}\" state=\"{1}\">{2}</ActionNamedValueListUpdate>", logging, state, nvls);
        }

        public void SetCancelRequested()
        {
            isCancelRequested = true;
        }

		public void SetStateStarted(ActionLogging logging)
		{
			ActionStateCode entryASC = stateCode;
			if (CanStart)
			{
				resultCode = null;
				StateCode = ActionStateCode.Started;
				EmitStateChangeMesg(logging, stateCode, entryASC, string.Empty);
			}
			else
				HandleInvalidStateChange("SetStateStarted", logging);
		}

		public void SetStateIssued(ActionLogging logging)
		{
			ActionStateCode entryASC = stateCode;
			if (IsStarted)
			{
				StateCode = ActionStateCode.Issued;
				EmitStateChangeMesg(logging, stateCode, entryASC, string.Empty);
			}
			else
				HandleInvalidStateChange("SetStateIssued", logging);
		}

        public void UpdateNamedValues(Common.NamedValueList namedValueList, ActionLogging logging)
        {
            if (IsStarted)
            {
                namedValues = namedValueList;
                EmitNamedValueListUpdateMesg(logging, StateCode, namedValues);
            }
            else
            {
                HandleInvalidStateChange("UpdateNamedValues", logging);
            }
        }

        public void SetStateComplete(string rc, ActionLogging logging) { SetStateComplete(rc, logging, false, null); } 

		public void SetStateComplete(string rc, ActionLogging logging, bool updateNVL, Common.NamedValueList namedValueList)
		{
			ActionStateCode entryASC = stateCode;
			bool isError = !String.IsNullOrEmpty(rc);

			// normal transition is from Issued to Complete.
			//	also accept transition from Ready or Started to Complete if we are given a non-null resultCode

			if (IsIssued || (isError && (IsReady || IsStarted)))
			{
				resultCode = rc;
				StateCode = ActionStateCode.Complete;
                if (updateNVL)
                {
                    namedValues = namedValueList;
                    EmitNamedValueListUpdateMesg(logging, StateCode, namedValues);
                }
				EmitStateChangeMesg(logging, stateCode, entryASC, rc);
			}
			else
				HandleInvalidStateChange(Utils.Fcns.CheckedFormat("SetStateComplete('{0}')", (rc != null ? rc : "<NullString>")), logging);
		}

		public void SetStateReady(ActionLogging logging)
		{
			ActionStateCode entryASC = stateCode;
			if (IsComplete || StateCode == ActionStateCode.Initial)
			{
				StateCode = ActionStateCode.Ready;
				EmitStateChangeMesg(logging, stateCode, entryASC, string.Empty);
			}
			else
				HandleInvalidStateChange("SetStateReady", logging);
		}

		private void HandleInvalidStateChange(string methodName, ActionLogging logging)
		{
			string ec = Utils.Fcns.CheckedFormat("ActionState.{0}: is not legal while action is in state '{1}'", methodName, StateCode);
			Utils.Assert.BreakpointFault(ec);
			SetStateInvalid(ec, logging);
		}

		public void SetStateInvalid(string rc, ActionLogging logging)
		{
			ActionStateCode entryASC = stateCode;

			resultCode = rc;
			StateCode = ActionStateCode.Invalid;

			EmitStateChangeMesg(logging, stateCode, entryASC, rc);
		}

		#endregion
	}

	#endregion

	//-------------------------------------------------
	#region ActionImpl related interfaces and delegates

	/// <summary>Non-typed version of templatized IProviderActionBase.  This is simply an IProviderFacet</summary>
	public interface IProviderActionBase : IProviderFacet { }

	/// <summary>Version of IProviderActionBase that gives get access to the ParamValue, set access to the ResultValue and a variation on CompleteRequest that completes the Action with a result code and value.</summary>
	public interface IProviderActionBase<ParamType, ResultType> : IProviderActionBase
	{
		ParamType ParamValue { get; }
		ResultType ResultValue { set; }
		void CompleteRequest(string resultCode, ResultType resultValue);
	}

	/// <summary>Action Method Delegate for a simple synchronous method that returns the string result code which is then used to complete the action.</summary>
	/// <returns>string result code.  null or string.Empty indicates that the caller must complete the action successfully, any other value is a fault code and indicates that the caller must mark the action has failed.</returns>
	public delegate string ActionMethodDelegateStrResult();

	/// <summary>Action Method Delegate for a moderately simple synchronous method that has access to the typed IProviderActionBase interface on the action and which returns a string result code which is then used to complete the action.</summary>
	/// <returns>string result code.  null indicates invoked method is responsible for completing the action, string.Empty indicates that the caller must complete the action successfully, any other value is a fault code and indicates that the caller must mark the action as failed.</returns>
	public delegate string ActionMethodDelegateActionArgStrResult<ParamType, ResultType>(IProviderActionBase<ParamType, ResultType> action);

	/// <summary>
	/// Action Method Delegate for fully capable action startup method.  
	/// This verions takes a typed IProviderActionBase interface for the action and produces a resultCode.  
	/// However with this version, the resultCode may be given as null, in which case the action will not be completed by the caller.
	/// </summary>
	/// <param name="action">gives the invoked method access to the IProviderActionBase interface on the action implementation object</param>
	/// <param name="resultCode">output resultCode.  Called method must assign this field.  null indicates invoked method is responsible for completing the action, string.Empty indicates that the caller must complete the action successfully, any other value is a fault code and indicates that the caller must mark the action as failed.</param>
	public delegate void FullActionMethodDelegate<ParamType, ResultType>(IProviderActionBase<ParamType, ResultType> action, out string resultCode);

	#endregion

	//-------------------------------------------------
	#region ActionImpl class

	/// <summary>
	/// Each of the public Action Factory methods provided by a Part generally constructs an instance of an ActionImpl object that is 
	/// templatized on the parameter and result type of the action and then returns the object to the client using the appropriate version of its IClientFacet interface.
	/// This class provides the basic implementation for all Actions.  It retains the Part's queue that it will enqueue itself into on start, the specific version of the
	/// delegate that will be invoked when it is issued, and an ActionLogging instance that it uses to emit action related messages and errors.
	/// </summary>
	/// <typeparam name="ParamType">Defines the type of the Parameter value that may be provided with this Action.</typeparam>
	/// <typeparam name="ResultType">Defines the type of the Result value that may be provided by the Part on successfull completion of this Action.</typeparam>

	public class ActionImplBase<ParamType, ResultType> 
		: IClientFacetWithParamAndResult<ParamType, ResultType>
		, IProviderActionBase<ParamType, ResultType>
		, IEnqueableProviderFacet
		where ResultType : new()
	{
		#region ctors

		public ActionImplBase(ActionQueue actionQ, ActionMethodDelegateStrResult method, ActionLogging logging) 
			: this(actionQ, null, false
					, delegate(IProviderActionBase<ParamType, ResultType> action, out string resultCode) 
							{ 
								resultCode = method(); 
								if (resultCode == null) 
									resultCode = string.Empty; 
							}
					, logging) 
		{}

		public ActionImplBase(ActionQueue actionQ, object paramValueObj, bool paramValueIsFixed, ActionMethodDelegateActionArgStrResult<ParamType, ResultType> method, ActionLogging logging)
			: this(actionQ, paramValueObj, paramValueIsFixed
					, delegate(IProviderActionBase<ParamType, ResultType> action, out string resultCode) 
							{ 
								resultCode = method(action); 
							}
					, logging) 
		{}

		public ActionImplBase(ActionQueue actionQ, object paramValueObj, bool paramValueIsFixed, FullActionMethodDelegate<ParamType, ResultType> method, ActionLogging logging)
		{
			this.actionQ = actionQ;
			this.method = method;
			this.logging = logging;

			string ec = null;
			if (paramValueObj != null)
			{
				if (paramValueObj is ParamType)
					paramValue = (ParamType) paramValueObj;
				else
					ec = "ActionImpl construction error: given parameter object is not of the expected type";
			}

			this.paramValueIsFixed = paramValueIsFixed;

			actionState.SetStateReady(logging);
			if (ec != null)
				actionState.SetStateComplete(ec, logging);

            NoteActionStateUpdated();
        }

		#endregion

		#region private and protected fields and properties

		private volatile bool isCancelRequestActive = false;
		private ActionQueue actionQ = null;
		private FullActionMethodDelegate<ParamType, ResultType> method = null;
		private ActionLogging logging;
		private object actionStateMutex = new object();
		private ActionStateImpl actionState = new ActionStateImpl();
        private IActionState iActionState = null;
        private BasicNotificationList notifyOnComplete = new BasicNotificationList();
        private BasicNotificationList notifyOnUpdate = new BasicNotificationList();
        private ParamType paramValue;
		private bool paramValueIsFixed;
		private ResultType resultValue = new ResultType();

		protected ActionLogging Logging { get { return logging; } }

        protected static SharedWaitEventNotifierSet sharedWaitEventNotifierSet { get { return sharedWaitEventNotifierSetSingleton.Instance; } }
        protected static Utils.SingletonHelper<SharedWaitEventNotifierSet> sharedWaitEventNotifierSetSingleton = new SingletonHelper<SharedWaitEventNotifierSet>();

		#endregion

		#region IClientFacet (et. al.)

		IBasicNotificationList IClientFacet.NotifyOnComplete { get { return notifyOnComplete; } }
		protected IBasicNotificationList NotifyOnComplete { get { return notifyOnComplete; } }

        IBasicNotificationList IClientFacet.NotifyOnUpdate { get { return notifyOnUpdate; } }
        protected IBasicNotificationList NotifyOnUpdate { get { return notifyOnUpdate; } }

		public string Start() { return Start(null, false); }

		public bool WaitUntilComplete(TimeSpan timeout)		//!< Obtains an IEventNotifier from the shared set and uses it.
		{
			bool useInfiniteTimeout = (timeout == TimeSpan.MaxValue);

			IActionState actionState = ActionState;
			if (!actionState.IsPendingCompletion)
				return actionState.IsComplete;

			IEventNotifier ien = sharedWaitEventNotifierSet.GetNextEventNotifier();

			NotifyOnComplete.AddItem(ien);

			QpcTimeStamp now = QpcTimeStamp.Now;
			QpcTimeStamp endTime = now + (useInfiniteTimeout ? TimeSpan.Zero : timeout);

			while (!ActionState.IsComplete)
			{
				TimeSpan remainingTime = (useInfiniteTimeout ? TimeSpan.FromMilliseconds(100) : (endTime - now));

				if (remainingTime < TimeSpan.Zero)
					break;

				ien.Wait(remainingTime);
				now = QpcTimeStamp.Now;
			}

			NotifyOnComplete.RemoveItem(ien);

			return ActionState.IsComplete;
		}

		public string WaitUntilComplete()
		{
			WaitUntilComplete(TimeSpan.MaxValue);
			return ActionState.ResultCode;
		}

		public string Run(TimeSpan timeout)
		{
			string ec = Start();
			if (!string.IsNullOrEmpty(ec))
				return ec;

			if (!WaitUntilComplete(timeout))
				return "Action.Run failed: time limit reached before action was complete";

			lock (actionStateMutex) 
			{ 
				ec = actionState.ResultCode; 
			}

			return ec;
		}

		public string Run() { return Run(TimeSpan.MaxValue); }

		public void RequestCancel()
		{
			if (!isCancelRequestActive)
			{
				isCancelRequestActive = true;
                actionState.SetCancelRequested();

				ActionQueue aq = actionQ;
				IActionState ias = ActionState;

				if (aq != null && ias.IsStarted)
				{
					aq.NoteCancelHasBeenRequestedOnRelatedAction();
					aq.ServiceCancelRequests();
				}

				EmitActionEvent("Cancel has been requested", ActionState.StateCode);
			}
		}

		public IActionState ActionState 
        { 
            get 
            { 
                lock (actionStateMutex) 
                {
                    if (iActionState == null)
                        iActionState = new ActionStateCopy(actionState);
                        
                    return iActionState;
                } 
            } 
        }

		public virtual bool IsParamValueSettable				// returns false if ParamValue cannot be assigned
		{
			get 
			{
				IActionState ias = ActionState;
				return ias.CanStart && !paramValueIsFixed;
			}
		}

		public virtual bool SetParamValue(ParamType value)			// returns false if ParamValue cannot be assigned
		{
			lock (actionStateMutex)
			{
				if (!actionState.CanStart || paramValueIsFixed)
					return false;

				paramValue = value;
				return true;
			}
		}

		public virtual ParamType ParamValue			// set throws if !IsParamValueSettable
		{
			get { lock (actionStateMutex) { return paramValue; } }
			set
			{
				lock (actionStateMutex)
				{
					if (paramValueIsFixed)
					{
						EmitActionError("Preventing attempt to update fixed paramValue", actionState.StateCode);

						throw new System.FieldAccessException("Action.ParamValue set failed: value is fixed");
					}

					if (!actionState.CanStart)
					{
						EmitActionError("Preventing attempt to update paramValue", actionState.StateCode);

						throw new System.FieldAccessException("Action.ParamValue can only be set while action is Idle");
					}

					paramValue = value;

					EmitActionEvent(Utils.Fcns.CheckedFormat("paramValue has been set to '{0}'", value.ToString()), actionState.StateCode);
				}
			}
		}

		string IClientFacetWithParam<ParamType>.Start(ParamType paramValue) { return Start(paramValue, true); }

		bool IFutureResult.IsResultAvailable { get { IActionState ias = ActionState; return ias.IsComplete; } }
		object IFutureResult.ResultObject 
		{
			get 
			{ 
				lock (actionStateMutex)
				{
					if (actionState.IsComplete)
						return resultValue;
					else
						return null;
				}
			}
		}

		ResultType IFutureResult<ResultType>.ResultValue	
		{
			get 
			{ 
				lock (actionStateMutex)
				{
					if (actionState.IsComplete)
						return resultValue;
					else
						return new ResultType();
				}
			}
		}

		#endregion

		#region IProviderActionBase<ParamType, ResultType> (et. al.)

		ParamType IProviderActionBase<ParamType, ResultType>.ParamValue { get { return paramValue; } }
		ResultType IProviderActionBase<ParamType, ResultType>.ResultValue { set { resultValue = value; } }

		public void CompleteRequest(string resultCode, ResultType resultValue)
		{
			this.resultValue = resultValue;
			CompleteRequest(resultCode);
		}

		public void IssueAndInvokeAction()
		{
			lock (actionStateMutex) 
			{ 
				actionState.SetStateIssued(logging);
                NoteActionStateUpdated();
			}

			if (IsCancelRequestActive)
			{
				CompleteRequest("Cancel Requested");
				return;
			}

			string resultCode = null;
			IActionState ias = null;

			if (method != null)
			{
                try
                {
                    method(this, out resultCode);
                }
                catch (System.Exception e)
                {
                    resultCode = Utils.Fcns.CheckedFormat("Internal: Method invoke threw unexpected exception: [{0}]", e.ToString());
                }

				ias = ActionState;
			}
			else
			{
				// else the action is handled through some other means
			}

			if (ias != null && !ias.IsComplete)
			{
				if (resultCode != null)
					CompleteRequest(resultCode);
				else if (method != null)
					EmitActionEvent("method return, action still pending", ActionState.StateCode);
			}
		}

		public bool IsCancelRequestActive { get { return isCancelRequestActive; } }

        public void UpdateNamedValues(Common.NamedValueList namedValueList)
        {
            lock (actionStateMutex)
            {
                actionState.UpdateNamedValues((namedValueList != null) ? new Common.NamedValueList(namedValueList) : null, logging);
                NoteActionStateUpdated();
            }
        }

        public void CompleteRequest(string resultCode) { CompleteRequest(resultCode, false, null); }
        public void CompleteRequest(string resultCode, Common.NamedValueList namedValueList) { CompleteRequest(resultCode, true, namedValueList); }

        private void CompleteRequest(string resultCode, bool updateNVL, Common.NamedValueList namedValueList)
        {
			if (resultCode == null)
				resultCode = "Error.Action.NullResultCodeIsNotValid";

			lock (actionStateMutex) 
            { 
                actionState.SetStateComplete(resultCode, logging, updateNVL, namedValueList);
                NoteActionStateUpdated();
            }

			notifyOnComplete.Notify();
        }


		#endregion

		#region IEnqueableProviderFacet

		public bool IsStarted { get { lock (actionStateMutex) { return actionState.IsStarted; } } }

		#endregion 

		#region Other

        protected void NoteActionStateUpdated()
        {
            iActionState = null;
            notifyOnUpdate.Notify();
        }

		protected void EmitActionEvent(string eventStr, ActionStateCode actionStateCode) 
		{
			logging.State.Emit("<ActionEvent id=\"{0}\" state=\"{1}\">{2}</ActionEvent>", logging.Mesg, actionStateCode.ToString(), eventStr); 
		}

		protected void EmitActionError(string eventStr, ActionStateCode actionStateCode) 
		{ 
			logging.Error.Emit("<ActionError id=\"{0}\" state=\"{1}\">{2}</ActionError>", logging.Mesg, actionStateCode.ToString(), eventStr); 
		}

		public string Start(object paramValueObj, bool paramProvided)
		{
			lock (actionStateMutex)
			{
				if (!actionState.CanStart)
				{
					string ec = "Action.Start failed: action is not Idle";

					EmitActionError(ec, actionState.StateCode);
					return ec;
				}

				if (paramProvided)
				{
					paramValue = (ParamType) paramValueObj;	// will throw on error
					EmitActionEvent(Utils.Fcns.CheckedFormat("paramValue has been set to '{0}' by Start", paramValueObj.ToString()), actionState.StateCode);
				}

                isCancelRequestActive = false;

                actionState.SetStateStarted(logging);
                NoteActionStateUpdated();
			}

			return actionQ.Enqueue(this);
		}


		#endregion
	}

	#endregion

	//-------------------------------------------------
}
