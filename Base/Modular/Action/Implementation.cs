//-------------------------------------------------------------------
/*! @file Implementation.cs
 *  @brief This file contains the definitions and classes that are used to define the internal Action Implementation objects for the Modular Action portions of this library.
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
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular.Common;
using System.Runtime.Serialization;

namespace MosaicLib.Modular.Action
{
	//-------------------------------------------------
	#region ActionLoggingConfig and ActionLogging

    /// <summary>
    /// This class defines the Logging.MesgType's for each of the standard classes of log messaages that Action objects emit during normal use.
    /// It also includes other logging related configuration parameters.
    /// These include Done, Error, State and Update events.
    /// <para/>Commonly used static values: Signif_Error_Debug_Debug, Info_Error_Debug_Debug, Info_Error_Trace_Trace, Info_Info_Trace_Trace, Debug_Debug_Trace_Trace, Trace_Trace_Trace_Trace, None_None_None_None
    /// <para/>Instances of this class (but possibly not derived ones) are immutable.
    /// </summary>
    public class ActionLoggingConfig
    {
        /// <summary>Explicit constructor with all available properties as optional ones.</summary>
        public ActionLoggingConfig(Logging.MesgType doneMesgType = Logging.MesgType.None, Logging.MesgType errorMesgType = Logging.MesgType.None, Logging.MesgType stateMesgType = Logging.MesgType.None, Logging.MesgType updateMesgType = Logging.MesgType.None, ActionLoggingStyleSelect actionLoggingStyleSelect = Action.ActionLoggingStyleSelect.None) 
        {
            DoneMesgType = doneMesgType;
            ErrorMesgType = errorMesgType;
            StateMesgType = stateMesgType;
            UpdateMesgType = updateMesgType;
            ActionLoggingStyleSelect = actionLoggingStyleSelect;
        }

        /// <summary>Copy Constructor:  Creates a new instance from the given <paramref name="other"/> that contains copies of the contained MesgType values.</summary>
        public ActionLoggingConfig(ActionLoggingConfig other, Logging.MesgType ? doneMesgType = null, Logging.MesgType ? errorMesgType = null, Logging.MesgType ? stateMesgType = null, Logging.MesgType ? updateMesgType = null, ActionLoggingStyleSelect ? actionLoggingStyleSelect = null) 
        {
            DoneMesgType = doneMesgType ?? other.DoneMesgType;
            ErrorMesgType = errorMesgType ?? other.ErrorMesgType;
            StateMesgType = stateMesgType ?? other.StateMesgType;
            UpdateMesgType = updateMesgType ?? other.UpdateMesgType;
            ActionLoggingStyleSelect = actionLoggingStyleSelect ?? other.ActionLoggingStyleSelect;
        }

        /// <summary>
        /// Debugging and logging helper method.
        /// </summary>
        public override string ToString()
        {
            return "Done:{0} Error:{1} State:{2} Update:{3}{4}".CheckedFormat(DoneMesgType, ErrorMesgType, StateMesgType, UpdateMesgType, (ActionLoggingStyleSelect == Action.ActionLoggingStyleSelect.None) ? "" : " {0}".CheckedFormat(ActionLoggingStyleSelect));
        }

        /// <summary>Gives the Logging.MesgType that is to be used for successfull Action Completion messages.</summary>
        public Logging.MesgType DoneMesgType { get; protected set; }

        /// <summary>Gives the Logging.MesgType that is to be used for Action Failed messages (including failed Action Completion messages).</summary>
        public Logging.MesgType ErrorMesgType { get; protected set; }

        /// <summary>Gives the Logging.MesgType that is to be used for other Action state change messages.</summary>
        public Logging.MesgType StateMesgType { get; protected set; }

        /// <summary>Gives the Logging.MesgTuype that is to be used for intermediate Action Update related messages.</summary>
        public Logging.MesgType UpdateMesgType { get; protected set; }

        /// <summary>Gives the currently selected action logging style</summary>
        public ActionLoggingStyleSelect ActionLoggingStyleSelect { get; protected set; }
        
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

        /// <summary>Canned configuration: Done=Debug, Error=Error, State=Trace, Update=Trace</summary>
        public static ActionLoggingConfig Debug_Error_Trace_Trace { get { return debug_error_trace_trace; } }

        /// <summary>Canned configuration: Done=Trace, Error=Trace, State=Trace, Update=Trace</summary>
        public static ActionLoggingConfig Trace_Trace_Trace_Trace { get { return trace_trace_trace_trace; } }

        /// <summary>Canned configuration: Done=None, Error=None, State=None, Update=None</summary>
        public static ActionLoggingConfig None_None_None_None { get { return none_none_none_none; } }

        private static readonly ActionLoggingConfig signif_error_debug_debug = new ActionLoggingConfig(Logging.MesgType.Signif, Logging.MesgType.Error, Logging.MesgType.Debug, Logging.MesgType.Debug);
        private static readonly ActionLoggingConfig info_error_debug_debug = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Error, Logging.MesgType.Debug, Logging.MesgType.Debug);
        private static readonly ActionLoggingConfig info_error_trace_trace = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Error, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig info_info_trace_trace = new ActionLoggingConfig(Logging.MesgType.Info, Logging.MesgType.Info, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig debug_debug_trace_trace = new ActionLoggingConfig(Logging.MesgType.Debug, Logging.MesgType.Debug, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig debug_error_trace_trace = new ActionLoggingConfig(Logging.MesgType.Debug, Logging.MesgType.Error, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig trace_trace_trace_trace = new ActionLoggingConfig(Logging.MesgType.Trace, Logging.MesgType.Trace, Logging.MesgType.Trace, Logging.MesgType.Trace);
        private static readonly ActionLoggingConfig none_none_none_none = new ActionLoggingConfig(Logging.MesgType.None, Logging.MesgType.None, Logging.MesgType.None, Logging.MesgType.None);
    }

    /// <summary>
    /// Flag enumeration used to select specific logging options.
    /// <para/>None (0x00), OldXmlishStyle (0x01), IncludeRunTimeOnCompletion (0x02)
    /// </summary>
    [Flags]
    public enum ActionLoggingStyleSelect : int
    {
        /// <summary>Default, placeholder (0x00)</summary>
        None = 0x00,

        /// <summary>Selects use of older Xmlish style of these messages (0x01)</summary>
        OldXmlishStyle = 0x01,

        /// <summary>When enabled, this option selects that the run time (time from start to complete) will be included in the completion log message (if any).  This option is not supported when OldXmlishStyle logging has been selected.</summary>
        IncludeRunTimeOnCompletion = 0x02,
    }

    /// <summary>
    /// This interface defines the features that an ActionLogging class instance provides that are used by other elements of the action implementation system.
    /// </summary>
    public interface IActionLogging
    {
        /// <summary>Typically used to give the name of the Action</summary>
        string Mesg { get; }

        /// <summary>Typically updated to contain the string version of the ParamValue or other per instance details for a specific Action instance.</summary>
        string MesgDetail { get; }

        /// <summary>Gives the, optional, Logging.IBasicLogger that shall be used as the base for the Emitters according to the corresponding Config values.  When set to null the individual Emitters are explicitly set to the Logging.NullEmitter.</summary>
        Logging.IBasicLogger Logger { get; }

        /// <summary>Gives the, optional, ActionLoggingConfig instance that will be used to determine which Logger emitters will be used by this instance.  When set to null the individual Emitters are explicitly set to the Logging.NullEmitter.</summary>
        ActionLoggingConfig Config { get; }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for Done related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        Logging.IMesgEmitter Done { get; }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for Error related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        Logging.IMesgEmitter Error { get; }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for State change related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        Logging.IMesgEmitter State { get; }

        /// <summary>get/set property defines the Logging.IMesgEmitter that is used for Action Update related events generated using this object.  Assigning the Logger or Config properties has the side effect of settings this property as well.</summary>
        Logging.IMesgEmitter Update { get; }
    }

	/// <summary>
	/// This class assists in generating log messages for actions.  
	/// It is typically created by an active part and given to the action for the action to use in emiting log messages.
    /// It also supports copy constructor behavior so that it can be replicated and used for individual actions.
	/// </summary>
    public class ActionLogging : IActionLogging
	{
        /// <summary>Copy constructor for use in creating ActionLogging objects for new Action objects: Mesg empty, MesgDetails empty. For use with Property Initializers</summary>
        public ActionLogging(ActionLogging copyFrom) : this(string.Empty, string.Empty, copyFrom.Logger, copyFrom.Config, copyFrom.Done, copyFrom.Error, copyFrom.State, copyFrom.Update) { }

        /// <summary>Copy constructor for use in creating ActionLogging objects for new Action objects: Mesg specified, MesgDetails empty.</summary>
        public ActionLogging(string mesg, ActionLogging copyFrom) : this(mesg, string.Empty, copyFrom.Logger, copyFrom.Config, copyFrom.Done, copyFrom.Error, copyFrom.State, copyFrom.Update) { }

        /// <summary>Copy constructor for use in creating ActionLogging objects for new Action objects: Mesg ane MesgDetails specified.</summary>
        public ActionLogging(string mesg, string mesgDetails, ActionLogging copyFrom) : this(mesg, mesgDetails, copyFrom.Logger, copyFrom.Config, copyFrom.Done, copyFrom.Error, copyFrom.State, copyFrom.Update) { }

        /// <summary>Standard constructor for creating a reference ActionLogging object (from which others are created)</summary>
        public ActionLogging(Logging.IBasicLogger logger, ActionLoggingConfig config)
		{
            Mesg = String.Empty;
            MesgDetail = String.Empty;
            this.logger = logger;
            this.config = config;
            UpdateEmitters();
		}

        /// <summary>Common constructor used by other public ones.</summary>
        protected ActionLogging(string mesg, string mesgDetails, Logging.IBasicLogger logger, ActionLoggingConfig config, Logging.IMesgEmitter doneEmitter, Logging.IMesgEmitter errorEmitter, Logging.IMesgEmitter stateEmitter, Logging.IMesgEmitter updateEmitter)
        {
            Mesg = mesg;
            MesgDetail = mesgDetails;
            this.logger = logger;
            this.config = config;
            this.doneEmitter = doneEmitter;
            this.errorEmitter = errorEmitter;
            this.stateEmitter = stateEmitter;
            this.updateEmitter = updateEmitter;
        }

        string mesg;
		volatile string mesgDetail;
        internal volatile string eMesgDetail;
        volatile Logging.IBasicLogger logger;
        volatile ActionLoggingConfig config;
        volatile Logging.IMesgEmitter doneEmitter, errorEmitter, stateEmitter, updateEmitter;

        /// <summary>Typically used to give the name of the Action</summary>
        public string Mesg { get { return mesg; } set { mesg = value.MapNullToEmpty(); } }

        /// <summary>Typically updated to contain the string version of the ParamValue or other per instance details for a specific Action instance.</summary>
        public string MesgDetail { get { return mesgDetail; } set { mesgDetail = value.MapNullToEmpty(); eMesgDetail = mesgDetail.GenerateEscapedVersion(); } }

        /// <summary>Gives the, optional, Logging.IBasicLogger that shall be used as the base for the Emitters according to the corresponding Config values.  When set to null the individual Emitters are explicitly set to the Logging.NullEmitter.</summary>
        public Logging.IBasicLogger Logger { get { return logger; } set { logger = value; UpdateEmitters(); } }

        /// <summary>Gives the, optional, ActionLoggingConfig instance that will be used to determine which Logger emitters will be used by this instance.  When set to null the individual Emitters are explicitly set to the Logging.NullEmitter.</summary>
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
        public System.Collections.Generic.IDictionary<string, Logging.IMesgEmitter> Emitters
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
        public string ActionDescription
        {
            get
            {
                if (string.IsNullOrEmpty(mesgDetail))
                    return Mesg;
                else
                    return "{0}[{1}]".CheckedFormat(Mesg, eMesgDetail);
            }
        }

        /// <summary>Returns a string version of the Mesg and MesgDetail.</summary>
        public override string ToString()
		{
            return ActionDescription;
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
    /// <remarks>
    /// This class does not directly support DataContract serialiation (it has no DataMembers itself) but it needs to be marked with the DataContract attribute
    /// so that its derived types can be DataContract serialized.
    /// </remarks>
    [DataContract(Namespace = Constants.ModularActionNameSpace), Serializable]
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
        protected ActionStateImplBase(IActionState other)
        {
            if (other != null)
            {
                ActionStateImplBase otherAsASIB = other as ActionStateImplBase;

                stateCode = other.StateCode;
                timeStamp = other.TimeStamp;
                resultCode = ((otherAsASIB != null) ? otherAsASIB.resultCode : other.ResultCode);     // try to make a naked copy of the result code (rather than using the public ResultCode property that has additional logic).
                isCancelRequested = other.IsCancelRequested;
                namedValues = other.NamedValues.ConvertToReadOnly();
            }
        }

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

        #region Protected fields

        /// <summary>stores the last ActionStateCode value</summary>
        protected ActionStateCode stateCode = ActionStateCode.Initial;

        /// <summary>stores the last QpcTimeStamp for the stateCode value.</summary>
        [NonSerialized]
        protected Time.QpcTimeStamp timeStamp = Time.QpcTimeStamp.Zero;

        /// <summary>stores the resultCode which is valid while the Action is Complete.</summary>
        protected string resultCode = null;

        /// <summary>Indicates if the client has requested that the current Action execution be canceled by the provider.</summary>
        protected volatile bool isCancelRequested = false;

        /// <summary>Contains the last set of NameValues that have been given by the provider.  This will always be a readonly copy of a set that was passed in from elsewhere.</summary>
        protected Common.NamedValueSet namedValues = null;

        private double age;

        #endregion

        #region IActionState and corresponding public property set methods

        ///<summary>Custom ToString method gives assistance for debuging and logging</summary>
        public override string ToString()
        {
            if (namedValues.IsNullOrEmpty())
                return Fcns.CheckedFormat("{0} rc:'{1}'{2}", stateCode, resultCode, (IsCancelRequested ? " CancelRequested" : ""));
            else
                return Fcns.CheckedFormat("{0} rc:'{1}'{2} {3}", stateCode, resultCode, (IsCancelRequested ? " CancelRequested" : ""), namedValues.ToStringSML());
        }

        /// <summary>Public Getter returns the published ActionStateCode.  Protected Setter updates the StateCode to the given value and sets the timeStamp to QpcTimeStamp.Now.</summary>
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
        public Time.QpcTimeStamp TimeStamp 
        { 
            get { return timeStamp; } 
        }

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
        //[DataMember(Order = 20, EmitDefaultValue = false, IsRequired = false)]
        public bool IsCancelRequested 
        { 
            get { return isCancelRequested; } 
        }

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
        //[DataMember(Order = 30, EmitDefaultValue = false, IsRequired = false)]
        public Common.INamedValueSet NamedValues
        {
            get { return namedValues ?? Common.NamedValueSet.Empty; }
        }

        /// <summary>
        /// IEquatable{IActionState} Equals implementation.  
        /// Returns true of the contents of this IActionState are "equal" to the contents of the other IActionState.
        /// Checks StateCode, ResultCode, TimeStamp and NamedValues for equality
        /// </summary>
        public bool Equals(IActionState other)
        {
            return this.Equals(other, compareTimestamps: true);
        }

        /// <summary>
        /// IEquatable{IActionState} Equals implementation.  
        /// Returns true of the contents of this IActionState are "equal" to the contents of the other IActionState.
        /// Checks StateCode, ResultCode, TimeStamp (optionally) and NamedValues for equality
        /// </summary>
        public bool Equals(IActionState other, bool compareTimestamps)
        {
            if (Object.ReferenceEquals(this, other))
                return true;

            if (other == null)
                return false;

            return (StateCode == other.StateCode
                    && ResultCode == other.ResultCode
                    && (TimeStamp == other.TimeStamp || !compareTimestamps)
                    && NamedValues.IsEqualTo(other.NamedValues)
                    );
        }

        #endregion

        #region object.Equals overrides

        /// <summary>
        /// Returns true if the given other object is an IActionState and this action state is IEquatable.Equals to the given other IActionState
        /// </summary>
        public override bool Equals(object other)
        {
            if (other is IActionState)
                return Equals(other as IActionState);
            else
                return false;
        }

        /// <summary>
        /// Passthough override method include to prevent warnings due to custom Equals implementation
        /// </summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion

        public static IActionState Empty { get { return empty; } }
        private static readonly IActionState empty = new ActionStateImplBase();
    }

    public static partial class ExtensionMethods
    {
        public static bool Equals(this IActionState thisIAS, IActionState other, bool compareTimeStamps = true)
        {
            if (Object.ReferenceEquals(thisIAS, other))
                return true;

            if (thisIAS == null || other == null)
                return false;

            if (thisIAS.Equals(other))
                return true;

            if (compareTimeStamps)
                return false;

            return (thisIAS.StateCode == other.StateCode
                    && thisIAS.ResultCode == other.ResultCode
                    // && thisIAS.TimeStamp == other.TimeStamp
                    && thisIAS.NamedValues.IsEqualTo(other.NamedValues)
                    );
        }
    }

    /// <summary>
    /// This is a basic storage wrapper object for IActionState.  Any code that wishes may use this class to create a clone of a given IActionState.
    /// This class is also used as the source/destination class for all DataContract serialization/deserialization of an IActionState's contents.
    /// </summary>
    [DataContract(Name = "ActionState", Namespace = Constants.ModularActionNameSpace), Serializable]
    public class ActionStateCopy : ActionStateImplBase
    {
        /// <summary>Default constructor</summary>
        public ActionStateCopy() 
            : base() 
        { }

        /// <summary>
        /// Special constructor to allow external code to build IActionState objects with special desired contents (esp Interconnect WCF)
        /// </summary>
        public ActionStateCopy(ActionStateCode stateCode, string resultCode, INamedValueSet namedValues)
        {
            this.StateCode = stateCode;
            this.resultCode = resultCode;
            this.namedValues = namedValues.ConvertToReadOnly(mapNullToEmpty: false);
        }

        /// <summary>Copy constructor</summary>
        /// <remarks>
        /// Please note that the namedValues are not deep-cloned by this operation.  
        /// NVL cloning is done on entry to the ActionImpl class and from there it requred that the IActionState users do not modify the contents of these shared objects.
        /// </remarks>
        public ActionStateCopy(IActionState other, bool autoRemoveResultCode = true) 
            : base(other) 
        {
            if (autoRemoveResultCode && StateCode != ActionStateCode.Complete)
                resultCode = null;
        }

        /// <summary>
        /// This property is only used for DataContract serialization/deserialization.
        /// </summary>
        [DataMember(Name = "StateCode", Order = 10)]
        private ActionStateCode DC_StateCode { get { return base.StateCode; } set { StateCode = value; } }

        /// <summary>
        /// This property is only used for DataContract serialization/deserialization.
        /// </summary>
        [DataMember(Name = "ResultCode", Order = 20, EmitDefaultValue = false, IsRequired = false)]
        private string DC_ResultCode { get { return base.ResultCode; } set { resultCode = value; } }

        /// <summary>
        /// This property is only used for DataContract serialization/deserialization.
        /// </summary>
        [DataMember(Name = "IsCancelRequested", Order = 30, EmitDefaultValue = false, IsRequired = false)]
        private bool DC_IsCancelRequested { get { return base.isCancelRequested; } set { base.isCancelRequested = value; } }

        /// <summary>
        /// This property is used for DataContract serialization/deserialization.  The getter is also used for remoting as it gives the remoting code getter access to the internally deserialized and stored namedValues field, which may be null.
        /// The property setter sets the IsReadOnly flag on the NamedValueSet instance that it is given.
        /// It can (and should) do this because this setter is only called by DataContract deserialization and as such the NamedValueSet instance it is being given
        /// has always been freshly constructed and has no other users or clients that are permitted to further change it.
        /// </summary>
        [DataMember(Name = "NamedValues", Order = 40, EmitDefaultValue = false, IsRequired = false)]
        internal NamedValueSet DC_NamedValues 
        {
            get { return base.namedValues; }
            private set { base.namedValues = ((value != null) ? value.MakeReadOnly() : null); }     // this path is only used for deserialization.  As such we can safely convert the given NVS instance to be readonly without needing to copy it first.
        }

        /// <summary>Returns the approximate size of the contents in bytes.</summary>
        [Obsolete("The use of this property has been deprecated.  (2018-03-09)")]
        public int EstimatedContentSizeInBytes
        {
            get { return (30 + 10 + DC_ResultCode.EstimatedContentSizeInBytes() + (DC_IsCancelRequested ? 40 : 0) + DC_NamedValues.MapNullToEmpty().EstimatedContentSizeInBytes); }
        }
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Allows the caller to pass an IActionState instance and get back an ActionStateCopy from it.  
        /// This is done by casting if the underlying object is already an ActionStateCopy, otherwise this done using a copy constructor.
        /// </summary>
        public static ActionStateCopy ConvertToActionStateCopy(this IActionState actionStateIn, bool autoRemoveResultCode = true)
        {
            ActionStateCopy actionState = actionStateIn as ActionStateCopy;

            if (actionState == null && actionStateIn != null)
                actionState = new ActionStateCopy(actionStateIn, autoRemoveResultCode: autoRemoveResultCode);

            return actionState;
        }
    }

    ///<summary>
	/// Define a class that contains the information necessary to determine the progress and success of a specfic Action.
	///</summary>
    [Serializable]
    public class ActionStateImpl : ActionStateImplBase
	{
		#region local methods

        /// <summary>Used internally to generate and emit consistantly formatted ActionStateChange records, either with or without a resultCode as deteremined by the toState value.</summary>
		private static void EmitStateChangeMesg(ActionLogging logging, ActionStateCode toState, ActionStateCode fromState, string resultCode, TimeSpan runTime = default(TimeSpan))
		{
            Logging.IMesgEmitter emitter = logging.State;
            string description = logging.ActionDescription;
            bool useNewStyle = ((logging.Config.ActionLoggingStyleSelect & ActionLoggingStyleSelect.OldXmlishStyle) == 0);

            bool isComplete = (toState == ActionStateCode.Complete);
            bool rcIsNonEmpty = !resultCode.IsNullOrEmpty();
            bool includeRC = (isComplete || rcIsNonEmpty);
			if (isComplete)
                emitter = rcIsNonEmpty ? logging.Error : logging.Done;

            if (useNewStyle)
            {
                string eDescription = description.GenerateEscapedVersion();

                bool includeRunTime = isComplete && ((logging.Config.ActionLoggingStyleSelect & ActionLoggingStyleSelect.IncludeRunTimeOnCompletion) != 0);
                string runTimeStr = (includeRunTime ? " runTime:{0:f6}".CheckedFormat(runTime.TotalSeconds) : "");

                if (isComplete && !rcIsNonEmpty)
                    emitter.Emit("Action succeeded [name:({0}) state:{1}<-{2}{3}]", eDescription, toState, fromState, runTimeStr);
                else if (isComplete)
                    emitter.Emit("Action failed [name:({0}) state:{1}<-{2} resultCode:({3}){4}]", eDescription, toState, fromState, resultCode.GenerateEscapedVersion(), runTimeStr);
                else if (!includeRC)
                    emitter.Emit("Action state changed [name:({0}) state:{1}<-{2}]", eDescription, toState, fromState);
                else
                    emitter.Emit("Action state changed [name:({0}) state:{1}<-{2} resultCode:({3})]", eDescription, toState, fromState, resultCode.GenerateEscapedVersion());
            }
            else
            {
                if (!includeRC)
                    emitter.Emit("<ActionStateChange id='{0}' to='{1}' from='{2}'/>", description, toState, fromState);
                else
                    emitter.Emit("<ActionStateChange id='{0}' to='{1}' from='{2}' rc='{3}'/>", description, toState, fromState, resultCode.GenerateQuotableVersion());
            }
		}

        /// <summary>Used internally to generate and emit consistantly formatted ActionNamedValueListUpdate records.</summary>
        private static void EmitUpdateNamedValuesMesg(ActionLogging logging, ActionStateCode state, Common.INamedValueSet nvs)
        {
            Logging.IMesgEmitter emitter = logging.Update;
            string description = logging.ActionDescription;
            bool useNewStyle = ((logging.Config.ActionLoggingStyleSelect & ActionLoggingStyleSelect.OldXmlishStyle) == 0);

            if (emitter.IsEnabled)
            {
                if (useNewStyle)
                {
                    // NOTE: the nvs is passed directly to the message level so it will show up when the message is formatted for output.
                    emitter.EmitWith("Action state NamedValues updated [name:({0}) state:{1}]".CheckedFormat(logging.ActionDescription.GenerateEscapedVersion(), state), nvs);
                }
                else
                {
                    string nvls = ((nvs != null) ? nvs.ToString() : "");

                    emitter.Emit("<ActionNamedValueListUpdate id='{0}' state='{1}'>{2}</ActionNamedValueListUpdate>", logging, state, nvls);
                }
            }
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
                StartedTimeStamp = TimeStamp;
                EmitStateChangeMesg(logging, stateCode, entryASC, string.Empty);
            }
            else
            {
                HandleInvalidStateChange("SetStateStarted", logging);
            }
		}

        public QpcTimeStamp StartedTimeStamp { get; private set; }

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

        /// <summary>
        /// Allows the caller to update the contained namedValues by replacing it with a readonly copy of the given namedValueSet.  Emits a corresponding mesg if the ActionState IsStarted or IsIssued
        /// </summary>
        public void UpdateNamedValues(Common.INamedValueSet namedValueSet, ActionLogging logging)
        {
            if (IsStarted || IsIssued)
            {
                namedValues = namedValueSet.ConvertToReadOnly();
                EmitUpdateNamedValuesMesg(logging, StateCode, namedValues);
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
        /// Optionally updates the namedValues, by setting it to a readonly copy of the given namedValueSet, when the updateNVL flag is true.
        /// </summary>
        public void SetStateComplete(string rc, ActionLogging logging, bool updateNVL, Common.INamedValueSet namedValueSet)
		{
			ActionStateCode entryASC = stateCode;
			bool isError = !rc.IsNullOrEmpty();

			// normal transition is from Issued to Complete.
			//	also accept transition from Ready or Started to Complete if we are given a non-null resultCode

            if (IsIssued || (isError && (IsReady || IsStarted)))
            {
                resultCode = rc;
                StateCode = ActionStateCode.Complete;
                if (updateNVL)
                {
                    namedValues = namedValueSet.ConvertToReadOnly();
                    EmitUpdateNamedValuesMesg(logging, StateCode, namedValues);
                }
                EmitStateChangeMesg(logging, stateCode, entryASC, rc, runTime: TimeStamp - StartedTimeStamp);
            }
            else
            {
                HandleInvalidStateChange("SetStateComplete[{0}]".CheckedFormat(rc.MapDefaultTo("<NullString>")), logging);
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
			string ec = Utils.Fcns.CheckedFormat("ActionState.{0}: is not legal while action is in state {1}", methodName, StateCode);
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
	public interface IProviderActionBase : IProviderFacet 
    { }

	/// <summary>Version of IProviderActionBase that gives get access to the ParamValue, set access to the ResultValue and a variation on CompleteRequest that completes the Action with a result code and value.</summary>
    /// <typeparam name="ParamType">Gives the type used for the ParamValue property that may be used to pass customized data from the client to the provider.</typeparam>
    /// <typeparam name="ResultType">Gives the type used for the ResultValue property that may be used to pass customized data from the provider to the client at the completion of the Action.</typeparam>
	public interface IProviderActionBase<ParamType, ResultType> : IProviderActionBase
	{
        /// <summary>
        /// Gives the provider get/set access to the ParamValue as last set by the client.
        /// <para/>Access to setter gives specific powers to the action provider that should be used with caution.  Generally the provider should only change the parameter contents while the command is active.
        /// </summary>
        ParamType ParamValue { get; set; }

        /// <summary>
        /// Gives the provider get/set access to the ResultValue that will be given to the client once the Action is complete.
        /// <para/>Generally the provider should only change the parameter contents while the command is active.
        /// </summary>
        ResultType ResultValue { get; set; }

        /// <summary>Used by the provider to mark an Action as complete and provide the corresponding resultCode and resultValue in a single call.</summary>
        /// <param name="resultCode">Gives the error code, or non if the string is empty to indicate the success or failure of the action.</param>
        /// <param name="resultValue">Gives the parameterized ResultType that will be stored in the Action implementation and which will then be available to the client.</param>
		void CompleteRequest(string resultCode, ResultType resultValue);
	}

	/// <summary>
    /// Action Method Delegate for a simple synchronous method that returns the string result code which is then used to complete the action.
    /// <para/>Note: unlike the ActionMethodDelegateActionArgStrResult and the FullActionMethodDelegate, if this delegate returns null it indicates that the action is complete.
    /// </summary>
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
	#region ActionImplBase class

	/// <summary>
	/// Each of the public Action Factory methods provided by a Part generally constructs an action instance derived from the ActionImplBase class that is 
	/// templatized on the parameter and result type of the action and then returns the object to the client using the appropriate version of its IClientFacet interface.
	/// This class provides the basic implementation for all Actions.  It retains the Part's queue in that it will enqueue itself into on start.  Then the specific version of the
	/// delegate that will be invoked when it is issued, and an ActionLogging instance that it uses to emit action related messages and errors.
    /// <para/>Use MosaicLib.Modular.Common.NullObj in place of the ParamType or ResultType if that capability is not needed for this action implementation type.
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
        /// <param name="mesg">When this is non-null it will be used to replace the given logging instance's Mesg with a new instance constructed using the given message</param>
        /// <param name="mesgDetails">When this is non-null and if the mesg is non-null it will be used to replace the given logging instance's MesgDetail with a new instance constructed using the given message details</param>
        /// <param name="doNotCloneLogging">When this parameter is false, the given logging object will be treated as a reference copy and a clone will be made of it, otherwise the given instance will be used verbatim</param>
        public ActionImplBase(ActionQueue actionQ, ActionMethodDelegateStrResult method, ActionLogging logging, string mesg = null, string mesgDetails = null, bool doNotCloneLogging = false) 
			: this(actionQ, null, false
					, delegate(IProviderActionBase<ParamType, ResultType> action, out string resultCode) 
							{ 
								resultCode = method(); 
								if (resultCode == null) 
									resultCode = string.Empty; 
							}
                    , logging, mesg, mesgDetails, doNotCloneLogging) 
		{}

        /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{ParamType, ResultType}.  Uses adapter delegate to become a FullActionMethodDelegate{ParamType, ResultType}</summary>
        /// <param name="actionQ">Defines the ActionQueue that this action will be enqueued to each time it is started.</param>
        /// <param name="paramValueObj">Gives the initial value that is to be assigned to the Action's ParamValue, or null if no value is to be so assigned.</param>
        /// <param name="paramValueIsFixed">Set to true if the Action's ParamValue cannot be changed after the Action has been created.</param>
        /// <param name="method">Defines the client provided delegate that will be invoked when the Action transitions to the ActionStateCode.Issued state.</param>
        /// <param name="logging">Provides the ActionLogging information that is used to define and customize the logging for this Action.</param>
        /// <param name="mesg">When this is non-null it will be used to replace the given logging instance's Mesg with a new instance constructed using the given message</param>
        /// <param name="mesgDetails">When this is non-null and if the mesg is non-null it will be used to replace the given logging instance's MesgDetail with a new instance constructed using the given message details</param>
        /// <param name="doNotCloneLogging">When this parameter is false, the given logging object will be treated as a reference copy and a clone will be made of it, otherwise the given instance will be used verbatim</param>
        public ActionImplBase(ActionQueue actionQ, object paramValueObj, bool paramValueIsFixed, ActionMethodDelegateActionArgStrResult<ParamType, ResultType> method, ActionLogging logging, string mesg = null, string mesgDetails = null, bool doNotCloneLogging = false)
			: this(actionQ, paramValueObj, paramValueIsFixed
					, delegate(IProviderActionBase<ParamType, ResultType> action, out string resultCode) 
							{ 
								resultCode = method(action); 
							}
                    , logging, mesg, mesgDetails, doNotCloneLogging) 
		{}

        /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{ParamType, ResultType}.</summary>
        /// <param name="actionQ">Defines the ActionQueue that this action will be enqueued to each time it is started.</param>
        /// <param name="paramValueObj">Gives the initial value that is to be assigned to the Action's ParamValue, or null if no value is to be so assigned.</param>
        /// <param name="paramValueIsFixed">Set to true if the Action's ParamValue cannot be changed after the Action has been created.</param>
        /// <param name="method">Defines the client provided delegate that will be invoked when the Action transitions to the ActionStateCode.Issued state.</param>
        /// <param name="loggingIn">Provides the ActionLogging information that is used to define and customize the logging for this Action.</param>
        /// <param name="mesg">When this is non-null it will be used to replace the given logging instance's Mesg with a new instance constructed using the given message</param>
        /// <param name="mesgDetails">When this is non-null and if the mesg is non-null it will be used to replace the given logging instance's MesgDetail with a new instance constructed using the given message details</param>
        /// <param name="doNotCloneLogging">When this parameter is false, the given logging object will be treated as a reference copy and a clone will be made of it, otherwise the given instance will be used verbatim</param>
        public ActionImplBase(ActionQueue actionQ, object paramValueObj, bool paramValueIsFixed, FullActionMethodDelegate<ParamType, ResultType> method, ActionLogging loggingIn, string mesg = null, string mesgDetails = null, bool doNotCloneLogging = false)
		{
			this.actionQ = actionQ;
			this.method = method;

            var cloneLogging = !doNotCloneLogging;

            var logging = cloneLogging ? new ActionLogging(loggingIn) : loggingIn;

            this.logging = logging;

            if (mesg != null || mesgDetails != null)
            {
                logging.Mesg = mesg;
                logging.MesgDetail = mesgDetails;
            }

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
		private readonly object actionStateMutex = new object();
		private ActionStateImpl actionState = new ActionStateImpl();
        private IActionState iActionState = null;
        private BasicNotificationList notifyOnComplete = new BasicNotificationList();
        private BasicNotificationList notifyOnUpdate = new BasicNotificationList();
        private ParamType paramValue;
		private bool paramValueIsFixed;
		private ResultType resultValue = default(ResultType);

        /// <summary>Protected property that reports the ActionLogging instance that this Action is using for its logging.</summary>
		protected ActionLogging Logging { get { return logging; } }

        IActionLogging IProviderFacet.Logging { get { return logging; } }

        /// <summary>Protected Static property that Action's use as a source for WaitEventNotifiers to be used when waiting without any other useable event notifier.</summary>
        protected static EventNotifierPool eventNotifierPool { get { return eventNotifierPoolSingleton.Instance; } }

        /// <summary>Private static SingletonHelper used to create and manage the standard Action's SharedWaitEventNotifierSet</summary>
        private static Utils.ISingleton<EventNotifierPool> eventNotifierPoolSingleton = new SingletonHelperBase<EventNotifierPool>(() => new EventNotifierPool());

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

        /// <summary>
        /// Carries a set of name/value pair objects that can be passed by the client to the target Part as named parameter values based on the Common.NamedValueList
        /// facility.  These may be used to carry an arbitrary set of parameters, by name, to the Action's implementing part.
        /// A readonly copy of this property is made when the action is Started and this clone is then made available to the provider.
        /// </summary>
        /// <remarks>
        /// Client is free to replace this property at any time or to change the underlying set contents at any time.  
        /// Start method will create a readonly copy and save it in the provider's version of the NamedParamValues property.
        /// </remarks>
        public Common.INamedValueSet NamedParamValues { get { return clientNamedParamValues; } set { clientNamedParamValues = value; } }
        Common.INamedValueSet clientNamedParamValues = null;

        /// <summary>
        /// Gives provider access to a readonly NamedValueSet provided by the client and cloned by the action implementation when the action is started.
        /// If the client did not provide any NamedParamValues then this property will return a readonly empty set so that it may safely be used by the provider without additional null checking.
        /// </summary>
        Common.INamedValueSet IProviderFacet.NamedParamValues
        {
            get { return providerNamedParamValues ?? Common.NamedValueSet.Empty; }
        }
        Common.INamedValueSet providerNamedParamValues = null;

        /// <summary>
        /// Updates the IProviderFacet.NamedParamValues property to be a readonly copy of the IClientFacet.NamedParamValues.
        /// null is passed and handled at the IProviderFacet.NamedParamValues property getter level.
        /// </summary>
        protected void CaptureClientNamedParamValues()
        {
            INamedValueSet entryProviderNPV = providerNamedParamValues;

            providerNamedParamValues = clientNamedParamValues.ConvertToReadOnly(mapNullToEmpty: false);
 
            if (providerNamedParamValues != null)
                EmitActionEvent("NamedParamValues have been captured", actionState.StateCode, nvs: providerNamedParamValues);
            else if (entryProviderNPV != null)
                EmitActionEvent("NamedParamValues have been cleared", actionState.StateCode);
        }

        /// <summary>Starts the action if it is in an Idle state and return string.Empty or returns an error message if the action is not in a state from which it can be started.</summary>
        public string Start() 
        { 
            return Start(null, false); 
        }

        /// <summary>Waits until the action is complete or the given time limit is reached.  Returns true if the action was already complete or if it completed within the stated time limit.  Returns false otherwise</summary>
        /// <remarks>Obtains an IEventNotifier from the shared set and uses it.</remarks>
        public bool WaitUntilComplete(TimeSpan timeout)
		{
			bool useInfiniteTimeout = (timeout == TimeSpan.MaxValue);

			IActionState actionState = ActionState;
			if (!actionState.IsPendingCompletion)
				return actionState.IsComplete;

			IEventNotifier ien = eventNotifierPool.GetInstanceFromPool();

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
                {
                    try
                    {
                        NotifyOnComplete.RemoveItem(ien);
                        eventNotifierPool.ReturnInstanceToPool(ref ien);
                    }
                    catch
                    { 
                    }
                }
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
                        iActionState = new ActionStateCopy(actionState, autoRemoveResultCode: false);
                        
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

        /// <summary>
        /// Gives the provider get/set access to set the ParamValue - getter simple returns the stored value as last set by the client or the provider.  Setter locks the actionState, sets the value and Emits an action event
        /// </summary>
        ParamType IProviderActionBase<ParamType, ResultType>.ParamValue
        {
            get { return paramValue; }
            set
            {
                lock (actionStateMutex)
                {
                    paramValue = value;

                    EmitActionEvent(Utils.Fcns.CheckedFormat("Provider set paramValue to '{0}'", value.ToString()), actionState.StateCode);
                }
            }
        }

        /// <summary>
        /// Gives the provider get/set access to the ResultValue - directly assigns and reads the internal storage without locks as the provider should only access this when the action is in progress.
        /// Final ResultValue will be available to the client once the Action is complete.
        /// </summary>
        ResultType IProviderActionBase<ParamType, ResultType>.ResultValue
        {
            get { return resultValue; }
            set { resultValue = value; }
        }

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
                    resultCode = Utils.Fcns.CheckedFormat("Internal: Method invoke threw unexpected {0}", ex.ToString(ExceptionFormat.Full));
                }

				ias = ActionState;
			}
			else
			{
				// else the action is handled through some other means
			}

            // this method can only complete an action due to a generated resultCode if the action state is still Issued.
            // if the action has been completed by the method and was re-started in the mean time then the state will be Started and not Issued and we must not 
            //  complete any such restarted action.

            if (ias != null && ias.IsIssued)
			{
				if (resultCode != null)
					CompleteRequest(resultCode);
				else if (method != null)
					EmitActionEvent("Invoked delegate return value indicates action is still pending", ias.StateCode);
			}
            else if (!resultCode.IsNullOrEmpty())
            {
                EmitActionError("Invoked delegate gave rc:[{0}] after action already complete".CheckedFormat(resultCode), ActionState.StateCode);
            }
		}

        /// <summary>
        /// True if the action cancel request flag has been set by the client or by the target provider.
        /// Property allows provider to determine if the action cancel request has been set.
        /// </summary>
        public bool IsCancelRequestActive { get { return isCancelRequestActive; } }

        /// <summary>
        /// Provider invokes this to replace the ActionState's NamedValueSet with a readonly copy of this given value and inform action's clients of the new values.  
        /// </summary>
        public void UpdateNamedValues(Common.INamedValueSet namedValueSet)
        {
            lock (actionStateMutex)
            {
                actionState.UpdateNamedValues(namedValueSet, logging);
                NoteActionStateUpdated();
            }
        }

        /// <summary>Provider invokes this to indicate that the action is complete and to provide the final <paramref name="resultCode"/></summary>
        public void CompleteRequest(string resultCode) 
        { 
            CompleteRequest(resultCode, false, null); 
        }

        /// <summary>
        /// Provider invokes this to indicate that the action is complete and to provide the final <paramref name="resultCode"/>.
        /// If a non-null <paramref name="namedValueSet"/> is provided then it will be used to secify the completed IActionState's NamedValues.
        /// </summary>
        public void CompleteRequest(string resultCode, Common.INamedValueSet namedValueSet) 
        { 
            CompleteRequest(resultCode, (namedValueSet != null), namedValueSet); 
        }

        /// <summary>Internal private common implementation for the CompleteRequest method.</summary>
        private void CompleteRequest(string resultCode, bool updateNVL, Common.INamedValueSet namedValueSet)
        {
			if (resultCode == null)
				resultCode = "Error.Action.NullResultCodeIsNotValid";

			lock (actionStateMutex) 
            {
                actionState.SetStateComplete(resultCode, logging, updateNVL, namedValueSet);
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

        /// <summary>
        /// Local implementation for assistance in logging and debugging.
        /// </summary>
        public override string ToString()
        {
            return ToString(default(ToStringSelect));
        }

        /// <summary>
        /// Custom variant of normal ToString method that gives caller access to which parts of the action they want included in the string.
        /// </summary>
        public string ToString(ToStringSelect select)
        {
            string mesg = logging.Mesg;
            string eMesgDetail = logging.MesgDetail;

            bool includeMesgDetail = (!eMesgDetail.IsNullOrEmpty() && (select != ToStringSelect.JustMesg));
            bool includeState = (select == ToStringSelect.MesgDetailAndState);

            if (!includeMesgDetail && !includeState)
                return mesg;
            else if (includeMesgDetail && !includeState)
                return "{0}[{1}]".CheckedFormat(mesg, eMesgDetail);
            else if (!includeMesgDetail && includeState)
                return "{0} state:{1}".CheckedFormat(mesg, ActionState);
            else
                return "{0}[{1}] state:{2}".CheckedFormat(mesg, eMesgDetail, ActionState);
        }

        /// <summary>Protected method is used internally to record that the ActionState may have been changed so as to force the generation of a new clone when one of the clients next asks for it.</summary>
        protected void NoteActionStateUpdated()
        {
            iActionState = null;
            notifyOnUpdate.Notify();
        }

        /// <summary>
        /// Protected common method used to generate and emit consistently formatted ActionEvent records
        /// <para/>{ActionEvent id="{0}" state="{1}"}{2}{/ActionEvent}
        /// </summary>
		protected void EmitActionEvent(string eventStr, ActionStateCode actionStateCode, INamedValueSet nvs = null) 
		{
            bool useNewStyle = ((logging.Config.ActionLoggingStyleSelect & ActionLoggingStyleSelect.OldXmlishStyle) == 0);
            string description = logging.ActionDescription;

            if (useNewStyle)
            {
                logging.State.EmitWith("Action event reported [name:({0}) state:{1} event:({2})]".CheckedFormat(description.GenerateEscapedVersion(), actionStateCode, eventStr.GenerateEscapedVersion()), nvs: nvs);
            }
            else
            {
                string nvsStr = (nvs.IsNullOrEmpty() ? "" : " {0}".CheckedFormat(nvs.ToStringSML()));

                logging.State.Emit("<ActionEvent id='{0}' state='{1}'>{2}{3}</ActionEvent>", description, actionStateCode, eventStr, nvsStr);
            }
		}

        /// <summary>
        /// Protected common method used to generate and emit consistently formatted ActionError records
        /// <para/>{ActionError id="{0}" state="{1}"}{2}{/ActionError}
        /// </summary>
        protected void EmitActionError(string errorStr, ActionStateCode actionStateCode) 
		{
            bool useNewStyle = ((logging.Config.ActionLoggingStyleSelect & ActionLoggingStyleSelect.OldXmlishStyle) == 0);
            string description = logging.ActionDescription;

            if (useNewStyle)
                logging.Error.Emit("Action issue detected [name:({0}) state:{1} error:({2})]", description.GenerateEscapedVersion(), actionStateCode, errorStr.GenerateEscapedVersion());
            else
                logging.Error.Emit("<ActionError id='{0}' state='{1}'>{2}</ActionError>", description, actionStateCode, errorStr); 
		}

        /// <summary>
        /// Common method used to attempt to Start an Action.  
        /// This method may attempt to update the ParamValue if paramProvided is true and then attempts to mark the Action as Started and then enqueue the Action into
        /// the ActionQueue object that the Part will use for this Action.
        /// </summary>
        /// <exception cref="System.InvalidCastException">Thown if paramProvided is true and given paramValueObj cannot be casted to this implementation's ParamType.</exception>
        public string Start(object paramValueObj, bool paramProvided)
		{
			lock (actionStateMutex)
			{
                // reset the last resultValue each time the action is started (so that prior results cannot be accidentally reused if the action fails to actually get started).
                resultValue = default(ResultType);

				if (!actionState.CanStart)
				{
					string ec = "Action.Start failed: action is not Idle";

					EmitActionError(ec, actionState.StateCode);
					return ec;
				}

                if (paramProvided)
				{
					paramValue = (ParamType) paramValueObj;	// will throw on error
					EmitActionEvent(Utils.Fcns.CheckedFormat("paramValue has been set to '{0}' by Start", paramValueObj), actionState.StateCode);
				}

                CaptureClientNamedParamValues();

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
