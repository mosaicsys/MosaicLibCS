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
	#region ActionLoggingConfig and ActionLogging

    /// <summary>
    /// This class defines the Logging.MesgType's for each of the standard classes of log messaages that Action objects emit during normal use.
    /// These include Done, Error, State and Update events.
    /// </summary>
    public class ActionLoggingConfig
    {
        /// <summary>Default constructor:  Sets all Mesg Types to Logging.MesgTypes.None</summary>
        public ActionLoggingConfig()
        {
            DoneMesgType = Logging.MesgType.None;
            ErrorMesgType = Logging.MesgType.None;
            StateMesgType = Logging.MesgType.None;
            UpdateMesgType = Logging.MesgType.None;
        }

        /// <summary>Explicit constructor:  Caller passes the 4 Logging.MesgType values explicitly.</summary>
        public ActionLoggingConfig(Logging.MesgType doneMesgType, Logging.MesgType errorMesgType, Logging.MesgType stateMesgType, Logging.MesgType updateMesgType) 
        {
            DoneMesgType = doneMesgType;
            ErrorMesgType = errorMesgType;
            StateMesgType = stateMesgType;
            UpdateMesgType = updateMesgType;
        }

        /// <summary>Copy Constructor:  Creates a new instance from the given rhs that contains copies of the contained MesgType values.</summary>
        public ActionLoggingConfig(ActionLoggingConfig rhs) 
        {
            DoneMesgType = rhs.DoneMesgType;
            ErrorMesgType = rhs.ErrorMesgType;
            StateMesgType = rhs.StateMesgType;
            UpdateMesgType = rhs.UpdateMesgType;
        }

        /// <summary>Gives the Logging.MesgType that is to be used for Action Completion messages.</summary>
        public Logging.MesgType DoneMesgType { get; protected set; }
        /// <summary>Gives the Logging.MesgType that is to be used for Action Failed messages.</summary>
        public Logging.MesgType ErrorMesgType { get; protected set; }
        /// <summary>Gives the Logging.MesgType that is to be used for other Action state change messages.</summary>
        public Logging.MesgType StateMesgType { get; protected set; }
        /// <summary>Gives the Logging.MesgTuype that is to be used for intermediate Action Update related messages.</summary>
        public Logging.MesgType UpdateMesgType { get; protected set; }

        private static readonly ActionLoggingConfig signif_error_debug_debug = new ActionLoggingConfig(Logging.MesgType.Signif, Logging.MesgType.Error, Logging.MesgType.Debug, Logging.MesgType.Debug);
        private static readonly ActionLoggingConfig info_error_debug_debug = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Error, Logging.MesgType.Debug, Logging.MesgType.Debug);
        private static readonly ActionLoggingConfig info_error_trace_trace = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Error, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig info_info_trace_trace = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Info, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig debug_debug_trace_trace = new ActionLoggingConfig(Logging.MesgType.Debug, Logging.MesgType.Debug, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig trace_trace_trace_trace = new ActionLoggingConfig(Logging.MesgType.Trace, Logging.MesgType.Trace, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig none_none_none_none = new ActionLoggingConfig(Logging.MesgType.None, Logging.MesgType.None, Logging.MesgType.None, Logging.MesgType.None);

        /// <summary>Canned configuration: Done=Signif, Error=Error, State=Debug, Update=Debug</summary>
        public static ActionLoggingConfig Signif_Error_Debug_Debug { get { return signif_error_debug_debug; } }
        /// <summary>Canned configuration: Done=Info, Error=Error, State=Debug, Update=Debug</summary>
        public static ActionLoggingConfig Info_Error_Debug_Debug { get { return info_error_debug_debug; } }
        /// <summary>Canned configuration: Done=Info, Error=Error, State=Trace, Update=Trace</summary>
        public static ActionLoggingConfig Info_Error_Trace_Trace { get { return info_error_trace_trace; } }
        /// <summary>Canned configuration: Done=Info, Error=Info, State=Trace, Update=Trace</summary>
        public static ActionLoggingConfig Info_Info_Trace_Trace { get { return info_info_trace_trace; } }
        /// <summary>Canned configuration: Done=Debug, Error=Debug, State=Trace, Update=Trace</summary>
        public static ActionLoggingConfig Debug_Debug_Trace_Trace { get { return debug_debug_trace_trace; } }
        /// <summary>Canned configuration: Done=Trace, Error=Trace, State=Trace, Update=Trace</summary>
        public static ActionLoggingConfig Trace_Trace_Trace_Trace { get { return trace_trace_trace_trace; } }
        /// <summary>Canned configuration: Done=None, Error=None, State=None, Update=None</summary>
        public static ActionLoggingConfig None_None_None_None { get { return none_none_none_none; } }
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

        /// <summary>Common constructor used by other public ones.</summary>
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
        volatile Logging.IBasicLogger logger;
        volatile ActionLoggingConfig config;
        volatile Logging.IMesgEmitter doneEmitter, errorEmitter, stateEmitter, updateEmitter;

        /// <summary>Typically used to give the name of the Action</summary>
        public string Mesg { get { return mesg; } set { mesg = value; } }

        /// <summary>Typically updated to contain the string version of the ParamValue or other per instance details for a specific Action instance.</summary>
        public string MesgDetail { get { return mesgDetail; } set { mesgDetail = value; } }

        /// <summary>Gives the, optinal, Logging.IBasicLogger that shall be used as the base for the Emitters according to the corresponding Config values.  When set to null the Emitters are explicitly set to the Logging.NullEmitter.</summary>
        public Logging.IBasicLogger Logger { get { return logger; } set { logger = value; UpdateEmitters(); } }

        /// <summary>Gives the, optional, ActionLoggingConfig instance that will be used to determine which Logger emitters will be used by this instance.  When set to null the Emitters are explicitly set to the Logging.NullEmitter.</summary>
        public ActionLoggingConfig Config { get { return config; } set { config = value; UpdateEmitters(); } }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for Done related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        public Logging.IMesgEmitter Done { get { return doneEmitter; } set { doneEmitter = value; } }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for Error related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        public Logging.IMesgEmitter Error { get { return errorEmitter; } set { errorEmitter = value; } }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for State change related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        public Logging.IMesgEmitter State { get { return stateEmitter; } set { stateEmitter = value; } }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for Action Update related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        public Logging.IMesgEmitter Update { get { return updateEmitter; } set { updateEmitter = value; } }

        /// <summary>
        /// Set only property may be used to assign the Done, Error, State and/or Update emitters from provided key/emitter dictionary.  Logging.NullEmitter will be automatically substituted for each null Logging.IMesgEmitter value.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, Logging.IMesgEmitter> Emitters
        {
            set
            {
                if (value == null)
                    UpdateEmitters();
                else
                {
                    Logging.IMesgEmitter emitter = null;
                    if (value.TryGetValue("Done", out emitter)) 
                        Done = emitter ?? Logging.NullEmitter;
                    if (value.TryGetValue("Error", out emitter)) 
                        Error = emitter ?? Logging.NullEmitter;
                    if (value.TryGetValue("State", out emitter)) 
                        State = emitter ?? Logging.NullEmitter;
                    if (value.TryGetValue("Update", out emitter)) 
                        Update = emitter ?? Logging.NullEmitter;
                }
            }
        }

        /// <summary>Returns a string version of the Mesg and MesgDetail.</summary>
        public override string ToString()
		{
			if (string.IsNullOrEmpty(mesgDetail))
				return Mesg;
			else
				return Utils.Fcns.CheckedFormat("{0}[{1}]", Mesg, MesgDetail);
		}

        /// <summary>Uses the given Logger and Config to update/replace the contents of the 4 emitters.</summary>
        private void UpdateEmitters()
        {
            Logging.IBasicLogger capturedLogger = logger;
            ActionLoggingConfig capturedConfig = config;

            if (capturedLogger != null && capturedConfig != null)
            {
                doneEmitter = capturedLogger.Emitter(capturedConfig.DoneMesgType);
                errorEmitter = capturedLogger.Emitter(capturedConfig.ErrorMesgType);
                stateEmitter = capturedLogger.Emitter(capturedConfig.StateMesgType);
                updateEmitter = capturedLogger.Emitter(capturedConfig.UpdateMesgType);
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

    /// <summary>
    /// This class is the basic class that implements an IActionState
    /// </summary>
    public class ActionStateImplBase : IActionState
    {
        #region Protected constructor(s)

        /// <summary>Default constructor.</summary>
        protected ActionStateImplBase() { }

        /// <summary>Copy constructor</summary>
        /// <remarks>
        /// Please note that the namedValues are not deep-cloned by this operation.  
        /// NVL cloning is done on entry to the ActionImpl class and from there it requred that the IActionState users do not modify the contents of these shared objects.
        /// </remarks>
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

        /// <summary>stores the last ActionStateCode value</summary>
        protected ActionStateCode stateCode = ActionStateCode.Initial;
        /// <summary>stores the last QpcTimeStamp for the stateCode value.</summary>
        protected Time.QpcTimeStamp timeStamp = Time.QpcTimeStamp.Zero;
        /// <summary>stores the resultCode which is valid while the Action is Complete.</summary>
        protected string resultCode = null;
        /// <summary>Indicates if the client has requested that the current Action execution be canceled by the provider.</summary>
        protected volatile bool isCancelRequested = false;
        /// <summary>Contains the last set of NameValues that have been given by the provider.</summary>
        protected Common.NamedValueList namedValues = null;

        #endregion

        #region IActionState and corresponding public property set methods

        ///<summary>Custom ToString method gives assistance for debuging and logging</summary>
        public override string ToString()
        {
            if (namedValues == null)
                return Fcns.CheckedFormat("{0} rc:'{1}'{2}", stateCode, resultCode, (IsCancelRequested ? " CancelRequested" : ""));
            else
                return Fcns.CheckedFormat("{0} rc:'{1}'{2} {3}", stateCode, resultCode, (IsCancelRequested ? " CancelRequested" : ""), namedValues);
        }

        /// <summary>Public Getter returns the published ActionStateCode.  Protected Setter updates the StateCode to the given value and the timeStamp.</summary>
        public ActionStateCode StateCode
        {
            get { return stateCode; }
            protected set
            {
                stateCode = value;
                timeStamp.SetToNow();
            }
        }

        /// <summary>Gives the TimeStamp for the last assigned value of the StateCode.</summary>
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

        /// <summary>True if the client has requested the current Action execution to be canceled.</summary>
        public bool IsCancelRequested { get { return isCancelRequested; } }

        // simple state test methods (each matches one state)
        /// <summary>True if the StateCode is not ActionStateCode.Invalid</summary>
        public bool IsValid { get { return stateCode != ActionStateCode.Invalid; } }
        /// <summary>True if the StateCode is ActionStateCode.Ready</summary>
        public bool IsReady { get { return stateCode == ActionStateCode.Ready; } }
        /// <summary>True if the StateCode is ActionStateCode.Started</summary>
        public bool IsStarted { get { return stateCode == ActionStateCode.Started; } }
        /// <summary>True if the StateCode is ActionStateCode.Issued</summary>
        public bool IsIssued { get { return stateCode == ActionStateCode.Issued; } }
        /// <summary>True if the StateCode is ActionStateCode.Complete</summary>
        public bool IsComplete { get { return stateCode == ActionStateCode.Complete; } }

        /// <summary>True if the StateCode IsReady or IsComplete (ActionStateCode.Ready or ActionStateCode.Complete)</summary>
        public bool CanStart { get { return (IsReady || IsComplete); } }

        /// <summary>True if the StateCode IsStarted or IsIssued (ActionStateCode.Started or ActionStateCode.Issued)</summary>
        public bool IsPendingCompletion { get { return (IsStarted || IsIssued); } }

        /// <summary>True if the StateCode IsComplete and the resultCode is neither null nor empty</summary>
        public bool Failed { get { return (IsComplete && !string.IsNullOrEmpty(resultCode)); } }
        /// <summary>True if the StateCode IsComplete and the resultCode is either null or empty</summary>
        public bool Succeeded { get { return (IsComplete && string.IsNullOrEmpty(resultCode)); } }

        /// <summary>Carries a set of name/value pair objects that have been published along with this state.</summary>
        public Common.NamedValueList NamedValues { get { return namedValues; } }

        #endregion
    }

    /// <summary>
    /// This is a basic storage wrapper object for IActionState.  Any code that wishes may use this class to create a clone of a given IActionState
    /// </summary>
    public class ActionStateCopy : ActionStateImplBase
    {
        /// <summary>Default constructor</summary>
        public ActionStateCopy() : base() { }

        /// <summary>Copy constructor</summary>
        /// <remarks>
        /// Please note that the namedValues are not deep-cloned by this operation.  
        /// NVL cloning is done on entry to the ActionImpl class and from there it requred that the IActionState users do not modify the contents of these shared objects.
        /// </remarks>
        public ActionStateCopy(IActionState rhs) : base(rhs) { }
    }

	///<summary>
	/// Define a class that contains the information necessary to determine the progress and success of a specfic Action.
	/// The object is a struct 
	///</summary>
	public class ActionStateImpl : ActionStateImplBase
	{
		#region local methods

        /// <summary>Used internally to generate and emit consistantly formatted ActionStateChange records, either with or without a resultCode as deteremined by the toState value.</summary>
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

        /// <summary>Used internally to gennerate and emit consistantly formatted ActinoNamedValueListUpdate records.</summary>
        private static void EmitNamedValueListUpdateMesg(ActionLogging logging, ActionStateCode state, Common.NamedValueList nvl)
        {
            Logging.IMesgEmitter emitter = logging.Update;

            string nvls = (nvl != null ? nvl.ToString() : "");

            emitter.Emit("<ActionNamedValueListUpdate id=\"{0}\" state=\"{1}\">{2}</ActionNamedValueListUpdate>", logging, state, nvls);
        }

        /// <summary>Method sets the volatile isCancelRequested value</summary>
        public void SetCancelRequested()
        {
            isCancelRequested = true;
        }

        /// <summary>Attempts to change the contained StateCode to ActionStateCode.Started.</summary>
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
            {
                HandleInvalidStateChange("SetStateStarted", logging);
            }
		}

        /// <summary>Attempts to change the contained StateCode to ActionStateCode.Issued</summary>
		public void SetStateIssued(ActionLogging logging)
		{
			ActionStateCode entryASC = stateCode;
            if (IsStarted)
            {
                StateCode = ActionStateCode.Issued;
                EmitStateChangeMesg(logging, stateCode, entryASC, string.Empty);
            }
            else
            {
                HandleInvalidStateChange("SetStateIssued", logging);
            }
		}

        /// <summary>Allows the caller to update the contained namedValues list and emit a corresponding mesg if the ActionState IsStarted or IsIssued</summary>
        public void UpdateNamedValues(Common.NamedValueList namedValueList, ActionLogging logging)
        {
            if (IsStarted || IsIssued)
            {
                namedValues = namedValueList;
                EmitNamedValueListUpdateMesg(logging, StateCode, namedValues);
            }
            else
            {
                HandleInvalidStateChange("UpdateNamedValues", logging);
            }
        }

        /// <summary>Attempts to set the StateCode to ActionStateCode.Complete and to set the resultCode to the given value.</summary>
        public void SetStateComplete(string rc, ActionLogging logging) 
        { 
            SetStateComplete(rc, logging, false, null); 
        }

        /// <summary>
        /// Attempts to set the StateCode to ActionStateCode.Complete and to set the resultCode to the given value.  
        /// Optionally updates the namedValues as well from the given list when the updateNVL flag is true.
        /// </summary>
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
            {
                HandleInvalidStateChange(Utils.Fcns.CheckedFormat("SetStateComplete('{0}')", (rc != null ? rc : "<NullString>")), logging);
            }
		}

        /// <summary>Attempts to change the contained StateCode to ActionStateCode.Ready so that the Action may be used again.</summary>
        public void SetStateReady(ActionLogging logging)
		{
			ActionStateCode entryASC = stateCode;
            if (IsComplete || StateCode == ActionStateCode.Initial)
            {
                StateCode = ActionStateCode.Ready;
                EmitStateChangeMesg(logging, stateCode, entryASC, string.Empty);
            }
            else
            {
                HandleInvalidStateChange("SetStateReady", logging);
            }
		}

        /// <summary>
        /// Fields trigger case for Valid state change assertion logic.  Generally attempts to take a breakpoint.  Sets state to Invalid from which point the Action object will no longer be usable.
        /// </summary>
		private void HandleInvalidStateChange(string methodName, ActionLogging logging)
		{
			string ec = Utils.Fcns.CheckedFormat("ActionState.{0}: is not legal while action is in state '{1}'", methodName, StateCode);
			Utils.Asserts.TakeBreakpointAfterFault(ec);
			SetStateInvalid(ec, logging);
		}

        /// <summary>Sets the contained StateCode to ActinStateCode.Invalid and emits a corresponding state change message.</summary>
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
	public interface IProviderActionBase 
        : IProviderFacet 
    { }

	/// <summary>Version of IProviderActionBase that gives get access to the ParamValue, set access to the ResultValue and a variation on CompleteRequest that completes the Action with a result code and value.</summary>
    /// <typeparam name="ParamType">Gives the type used for the ParamValue property that may be used to pass customized data from the client to the provider.</typeparam>
    /// <typeparam name="ResultType">Gives the type used for the ResultValue property that may be used to pass customized data from the provider to the client at the completion of the Action.</typeparam>
	public interface IProviderActionBase<ParamType, ResultType> 
        : IProviderActionBase
	{
        /// <summary>Gives the provider get access to the ParamValue as last set by the client.</summary>
		ParamType ParamValue { get; }

        /// <summary>Gives the provider set access to the ResultValue that will be given to the client once the Action is complete.</summary>
		ResultType ResultValue { set; }

        /// <summary>Used by the provider to mark an Action as complete and provide the corresponding resultCode and resultValue in a single call.</summary>
        /// <param name="resultCode">Gives the error code, or non if the string is empty to indicate the success or failure of the action.</param>
        /// <param name="resultValue">Gives the parameterized ResultType that will be stored in the Action implementation and which will then be available to the client.</param>
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
	{
		#region ctors

        /// <summary>Constructor for use with simple action method delegate: ActionMethodDelegateStrResult.  Uses adapter delegate to become a FullActionMethodDelegate{ParamType, ResultType}</summary>
        /// <param name="actionQ">Defines the ActionQueue that this action will be enqueued to each time it is started.</param>
        /// <param name="method">Defines the client provided delegate that will be invoked when the Action transitions to the ActionStateCode.Issued state.</param>
        /// <param name="logging">Provides the ActionLogging information that is used to define and customize the logging for this Action.</param>
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

        /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{ParamType, ResultType}.  Uses adapter delegate to become a FullActionMethodDelegate{ParamType, ResultType}</summary>
        /// <param name="actionQ">Defines the ActionQueue that this action will be enqueued to each time it is started.</param>
        /// <param name="paramValueObj">Gives the initial value that is to be assigned to the Action's ParamValue, or null if no value is to be so assigned.</param>
        /// <param name="paramValueIsFixed">Set to true if the Action's ParamValue cannot be changed after the Action has been created.</param>
        /// <param name="method">Defines the client provided delegate that will be invoked when the Action transitions to the ActionStateCode.Issued state.</param>
        /// <param name="logging">Provides the ActionLogging information that is used to define and customize the logging for this Action.</param>
        public ActionImplBase(ActionQueue actionQ, object paramValueObj, bool paramValueIsFixed, ActionMethodDelegateActionArgStrResult<ParamType, ResultType> method, ActionLogging logging)
			: this(actionQ, paramValueObj, paramValueIsFixed
					, delegate(IProviderActionBase<ParamType, ResultType> action, out string resultCode) 
							{ 
								resultCode = method(action); 
							}
					, logging) 
		{}

        /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{ParamType, ResultType}.</summary>
        /// <param name="actionQ">Defines the ActionQueue that this action will be enqueued to each time it is started.</param>
        /// <param name="paramValueObj">Gives the initial value that is to be assigned to the Action's ParamValue, or null if no value is to be so assigned.</param>
        /// <param name="paramValueIsFixed">Set to true if the Action's ParamValue cannot be changed after the Action has been created.</param>
        /// <param name="method">Defines the client provided delegate that will be invoked when the Action transitions to the ActionStateCode.Issued state.</param>
        /// <param name="logging">Provides the ActionLogging information that is used to define and customize the logging for this Action.</param>
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
		private ResultType resultValue = default(ResultType);

        /// <summary>Protected property that reports the ActionLogging instance that this Action is using for its logging.</summary>
		protected ActionLogging Logging { get { return logging; } }

        /// <summary>Protected Static property that Action's use as a source for WaitEventNotifiers to be used when waiting without any other useable event notifier.</summary>
        protected static SharedWaitEventNotifierSet sharedWaitEventNotifierSet { get { return sharedWaitEventNotifierSetSingleton.Instance; } }

        /// <summary>Private static SingletonHelper used to create and manage the standard Action's SharedWaitEventNotifierSet</summary>
        private static Utils.ISingleton<SharedWaitEventNotifierSet> sharedWaitEventNotifierSetSingleton = new SingletonHelperBase<SharedWaitEventNotifierSet>(() => new SharedWaitEventNotifierSet());

		#endregion

		#region IClientFacet (et. al.)

        /// <summary>Property gives caller access to the IBasicNotificationList that is signaled each time the action completes.</summary>
        IBasicNotificationList IClientFacet.NotifyOnComplete { get { return notifyOnComplete; } }
        /// <summary>Property gives caller access to the IBasicNotificationList that is signaled each time the action completes.</summary>
        protected IBasicNotificationList NotifyOnComplete { get { return notifyOnComplete; } }

        /// <summary>Property gives caller access to the IBasicNotificationList that is signaled each time the action's IActionState is updated.</summary>
        IBasicNotificationList IClientFacet.NotifyOnUpdate { get { return notifyOnUpdate; } }
        /// <summary>Property gives caller access to the IBasicNotificationList that is signaled each time the action's IActionState is updated.</summary>
        protected IBasicNotificationList NotifyOnUpdate { get { return notifyOnUpdate; } }

        /// <summary>Starts the action if it is in an Idle state and return string.Empty or returns an error message if the action is not in a state from which it can be started.</summary>
        public string Start() 
        { 
            return Start(null, false); 
        }

        /// <summary>Waits until the action is complete or the given time limit is reached.  Returns ActionState.ResultCode or suitable string if the action was not complete within the stated time limit.</summary>
        /// <remarks>Obtains an IEventNotifier from the shared set and uses it.</remarks>
        public bool WaitUntilComplete(TimeSpan timeout)
		{
			bool useInfiniteTimeout = (timeout == TimeSpan.MaxValue);

			IActionState actionState = ActionState;
			if (!actionState.IsPendingCompletion)
				return actionState.IsComplete;

			IEventNotifier ien = sharedWaitEventNotifierSet.GetNextEventNotifier();

            try
            {
                if (ien != null)
                    NotifyOnComplete.AddItem(ien);

                QpcTimeStamp now = QpcTimeStamp.Now;
                QpcTimeStamp endTime = now + (useInfiniteTimeout ? TimeSpan.Zero : timeout);

                while (!ActionState.IsComplete)
                {
                    TimeSpan remainingTime = (useInfiniteTimeout ? TimeSpan.FromMilliseconds(100) : (endTime - now));

                    if (remainingTime < TimeSpan.Zero)
                        break;

                    if (ien != null)
                        ien.Wait(remainingTime);
                    else
                        System.Threading.Thread.Sleep(10);

                    now = QpcTimeStamp.Now;
                }
            }
            finally
            {
                if (ien != null)
                    NotifyOnComplete.RemoveItem(ien);
            }

			return ActionState.IsComplete;
		}

        /// <summary>Waits until the action is complete.  Returns ActionState.ResultCode</summary>
        public string WaitUntilComplete()
		{
			WaitUntilComplete(TimeSpan.MaxValue);
			return ActionState.ResultCode;
		}

        /// <summary>Run the action to completion or until the given time limit is reached.  Returns ActionState.ResultCode or suitable string if the action was not complete within the stated time limit.</summary>
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

        /// <summary>Run the action to completion and return the ActionState.ResultCode.</summary>
        public string Run() 
        { 
            return Run(TimeSpan.MaxValue); 
        }

        /// <summary>
        /// Client invokes this to request that the current action be canceled.  This is only meaningfull when action has been successfully started and is not complete.
        /// Provider may invoke this to internally indicate that the action should be canceled.
        /// </summary>
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

        /// <summary>Property gives access to the dynamically updating IActionState for this action</summary>
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

        /// <summary>returns true if the client can modify ParamValue by assignement right now, returns false if ParamValue cannot be assigned, either because it is not changable or because the Action was not Ready to be started.</summary>
        public virtual bool IsParamValueSettable
		{
			get 
			{
				IActionState ias = ActionState;
				return ias.CanStart && !paramValueIsFixed;
			}
		}

        /// <summary>returns false if ParamValue could not be assigned either because it is not changable or because the Action was not Ready to be started.</summary>
        public virtual bool SetParamValue(ParamType value)
		{
			lock (actionStateMutex)
			{
				if (!actionState.CanStart || paramValueIsFixed)
					return false;

				paramValue = value;
				return true;
			}
		}

        /// <summary>Get or Set the Param value.</summary>
        /// <exception cref="System.FieldAccessException">thrown if setter is used when IsParamValueSettable is false.</exception>
        public virtual ParamType ParamValue
		{
			get 
            { 
                // note that locking is required on getter as the ParamType may be a value type and cannot use interlocked semantics for atomic update.
                lock (actionStateMutex)
                { 
                    return paramValue; 
                } 
            }
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

        /// <summary>Variation on IClientFacet.Start which allows caller to set ParamValue and then Start the action</summary>
        string IClientFacetWithParam<ParamType>.Start(ParamType paramValue) 
        { 
            return Start(paramValue, true); 
        }

        /// <summary>synonym for IsComplete</summary>
        bool IFutureResult.IsResultAvailable 
        { 
            get 
            { 
                IActionState ias = ActionState; 
                return ias.IsComplete; 
            } 
        }

        /// <summary>returns null if !IsResultAvailable, if value was not generated, or if value was explicitly set to null</summary>
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

        /// <summary>return final method produced ResultType if IsComplete or default(ResultType) if not</summary>
        ResultType IFutureResult<ResultType>.ResultValue	
		{
			get 
			{ 
				lock (actionStateMutex)
				{
					if (actionState.IsComplete)
						return resultValue;
					else
						return default(ResultType);
				}
			}
		}

		#endregion

		#region IProviderActionBase<ParamType, ResultType> (et. al.)

        /// <summary>Gives the provider get access to the ParamValue as last set by the client.</summary>
        ParamType IProviderActionBase<ParamType, ResultType>.ParamValue { get { return paramValue; } }

        /// <summary>Gives the provider set access to the ResultValue that will be given to the client once the Action is complete.</summary>
        ResultType IProviderActionBase<ParamType, ResultType>.ResultValue { set { resultValue = value; } }

        /// <summary>Used by the provider to mark an Action as complete and provide the corresponding resultCode and resultValue in a single call.</summary>
        /// <param name="resultCode">Gives the error code, or non if the string is empty to indicate the success or failure of the action.</param>
        /// <param name="resultValue">Gives the parameterized ResultType that will be stored in the Action implementation and which will then be available to the client.</param>
		public void CompleteRequest(string resultCode, ResultType resultValue)
		{
			this.resultValue = resultValue;
			CompleteRequest(resultCode);
		}

        /// <summary>Provider invokes this to dispatch the mark the action as issued and invoke its delegate method.</summary>
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
                catch (System.Exception ex)
                {
                    resultCode = Utils.Fcns.CheckedFormat("Internal: Method invoke threw unexpected exception: [{0}]", ex.ToString());
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

        /// <summary>
        /// True if the action cancel request flag has been set by the client or by the target provider.
        /// Property allows provider to determine if the action cancel request has been set.
        /// </summary>
        public bool IsCancelRequestActive { get { return isCancelRequestActive; } }

        /// <summary>Provider invokes this to replace the ActionState's NamedValues and inform action's clients of the new values</summary>
        public void UpdateNamedValues(Common.NamedValueList namedValueList)
        {
            lock (actionStateMutex)
            {
                actionState.UpdateNamedValues((namedValueList != null) ? new Common.NamedValueList(namedValueList) : null, logging);
                NoteActionStateUpdated();
            }
        }

        /// <summary>Provider invokes this to indicate that the action is complete and to provide the final resultCode</summary>
        public void CompleteRequest(string resultCode) 
        { 
            CompleteRequest(resultCode, false, null); 
        }

        /// <summary>Provider invokes this to indicate that the action is complete and to provide the final resultCode and set of NamedValues</summary>
        public void CompleteRequest(string resultCode, Common.NamedValueList namedValueList) 
        { 
            CompleteRequest(resultCode, true, namedValueList); 
        }

        /// <summary>Internal private common implementation for the CompleteRequest method.</summary>
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

        /// <summary>Returns true if the contained ActionState IsStarted</summary>
		public bool IsStarted 
        { 
            get 
            { 
                lock (actionStateMutex) 
                { 
                    return actionState.IsStarted; 
                } 
            } 
        }

		#endregion 

		#region Other

        /// <summary>Protected method is used internally to record that the ActionState may have been changed so as to force the generation of a new clone when one of the clients next asks for it.</summary>
        protected void NoteActionStateUpdated()
        {
            iActionState = null;
            notifyOnUpdate.Notify();
        }

        /// <summary>Protected common method used to generate and emit consistently formatted ActionEvent records</summary>
		protected void EmitActionEvent(string eventStr, ActionStateCode actionStateCode) 
		{
			logging.State.Emit("<ActionEvent id=\"{0}\" state=\"{1}\">{2}</ActionEvent>", logging.Mesg, actionStateCode.ToString(), eventStr); 
		}

        /// <summary>Protected common method used to generate and emit consistently formatted ActionError records</summary>
        protected void EmitActionError(string eventStr, ActionStateCode actionStateCode) 
		{ 
			logging.Error.Emit("<ActionError id=\"{0}\" state=\"{1}\">{2}</ActionError>", logging.Mesg, actionStateCode.ToString(), eventStr); 
		}

        /// <summary>
        /// Common method used to attempt to Start an Action.  
        /// This method may attempt to update the ParamValue if paramProvided is true and then attempts to mark the Action as Started and then enqueue the Action into
        /// the ActionQueue object that the Part will use for this Action.
        /// </summary>
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
