//-------------------------------------------------------------------
/*! @file SimpleActivePart.cs
 *  @brief This file contains the templatized definition of a base class that can be used to build many types of active parts.  This functionality is part of the Modular.Part namespace in this library.
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Parts;
using MosaicLib.Modular.Interconnect.Values;

namespace MosaicLib.Modular.Part
{
	//-----------------------------------------------------------------
	#region SimpleActivePartBase

    /// <summary>
    /// Defines the logic that a SimpleActivePart uses for clearing the threadWakeupNotifier object that is used in WaitForSomethingToDo calls.
    /// <para/>ExplicitlyResetNotifierAtStartOfEachLoop (0, default value), AutoResetIsUsedToResetNotifier, AutoResetIsUsedToResetNotifierWhenNoActionsHaveBeenPerformed
    /// </summary>
    public enum ThreadWakeupNotifierResetHandling : int
    {
        /// <summary>Old version (current default): threadWakeupNotifier is explicitly reset in loop just prior to calling PerformMainLoopService and is implicilty AutoReset when WaitForSomethingToDo returns true.</summary>
        ExplicitlyResetNotifierAtStartOfEachLoop = 0,

        /// <summary>recommended: threadWakeupNotifier is not explicitly Reset in the main loop.  WaitForSomethingToDo is called with the WaitTimeLimit if IssueNextQueuedAction returned false or with TimeSpan.Zero if IssueNextQueuedAction returned true.</summary>
        AutoResetIsUsedToResetNotifier = 1,

        /// <summary>threadWakeupNotifier is not explicitly Reset in the main loop.  WaitForSomethingToDo is called with the WaitTimeLimit if IssueNextQueuedAction returned false.  Loop does not call WaitForSomethingToDo (and thus does not AutoReset the threadWakeupNotifier) at all if IssueNextQueuedAction returns true (to indicate that an action was performed).</summary>
        AutoResetIsUsedToResetNotifierWhenNoActionsHaveBeenPerformed = 2,
    }

    /// <summary>
    /// This enumeration defines the behaviors/handling that the SimpleActivePartBase class can now implement for base class level GoOnline and GoOffline actions.
    /// <para/>None (0), BasePerformMethodsSucceed, GoOnlineUpdatesBaseUseState, GoOfflineUpdatesBaseUseState, All,
    /// <para/>Note: The SimpleActivePartBase part expicitly initializes its GoOnlineAndGoOfflineHandling to GoOnlineAndGoOfflineHandling.All by default.
    /// </summary>
    [Flags]
    public enum GoOnlineAndGoOfflineHandling : int
    {    
        /// <summary>
        /// Selects basic handling.  Base UseState is not automatically updated and base Peform methods return not implemented result code.
        /// </summary>
        None = 0,

        /// <summary>
        /// Selects that internal default implementation of PerformGoOnline and PerformGoOffline will succeed.
        /// </summary>
        BasePerformMethodsSucceed = 1,

        /// <summary>
        /// Selects that execution of the GoOnline action sets Base UseState to AttemptOnline before starting initialize and then to Online or AttempOnlineFailed based on success of the normal PerformGoOnline method.
        /// </summary>
        GoOnlineUpdatesBaseUseState = 2,

        /// <summary>
        /// Selects that execution of the GoOffline action sets Base UseState to Offline before calling the normal PerformGoOffline method.
        /// </summary>
        GoOfflineUpdatesBaseUseState = 4,

        /// <summary>
        /// Selects that when GoOnline fails, the resulting UseState is set to AttemptOnlineFailed.  (Default behavior is to set the UseState to Online)
        /// </summary>
        GoOnlineFailureSetsUseStateToAttemptOnlineFailed = 8,

        /// <summary>
        /// This option may only be combined with GoOnlineUpdatesBaseUseState.  When selected, if the derived classes PerformGoOnlineAction method explicitly changes the Base UseState from the pre-entry assigned value of AttemptOnline
        /// the outer state handling code will leave the derived classes value unchanged.  When not enabled, the outer method will still override the custom value and replace it with the value as determined by the outer value.
        /// </summary>
        AcceptCustomChangeFromAttemptOnlineState = 16,

        /// <summary>
        /// When this option is enabled, successful GoOnline(false) operations will set the UseState to OnlineUnintialized if the UseState was not Online when the operation was started.
        /// </summary>
        UseOnlineUnititializedState = 32,

        /// <summary>
        /// Selects that execution of GoOnline and GoOffline actions using base class will succeed and will automatically update the BaseUseState.
        /// <para/>(BasePerformMethodsSucceed | GoOnlineUpdatesBaseUseState | GoOfflineUpdatesBaseUseState | GoOnlineFailureSetsUseStateToAttemptOnlineFailed)
        /// </summary>
        All = (BasePerformMethodsSucceed | GoOnlineUpdatesBaseUseState | GoOfflineUpdatesBaseUseState | GoOnlineFailureSetsUseStateToAttemptOnlineFailed),
    }

    /// <summary>
    /// This enumeration is used to define obtional behaviors for the overall part.
    /// <para/>None (0x00), PerformActionPublishesActionInfo (0x01), UseMainThreadFailedState (0x02), MainThreadStartSetsStateToOffline (0x04), MainThreadStopSetsStateToStoppedIfIsOnlineOrAttemptOnline (0x08),
    /// MainThreadStopSetsStateToStoppedIfOffline (0x10), PerformMainLoopServiceCallsServiceBusyConditionChangeDetection (0x20)
    /// </summary>
    [Flags]
    public enum SimpleActivePartBehaviorOptions
    {
        /// <summary>Selects basic (default) handling. [0x00]</summary>
        None = 0x00,

        /// <summary>
        /// Requests that the part autotmatically creates a ActionInfo and a LastActionInfo IVA and that it generate and publishes information about the current action
        /// to the ActionInfo IVA at start and end of the CurrentAction and that it publish the final IActionInfo for the current action into the LastActionInfo when the current
        /// action has been completed.  [0x01]
        /// </summary>
        PerformActionPublishesActionInfo = 0x01,

        /// <summary>When this behavior is enabled, the MainThreadFcn exception handler will change the UseState MainThreadFailed to indicate that the part's main thread has ended. [0x02]</summary>
        UseMainThreadFailedState = 0x02,

        /// <summary>When this behavior is selected, the MainThreadFcn will set the UseState to Offline when the part is started.  [0x04]</summary>
        MainThreadStartSetsStateToOffline = 0x04,

        /// <summary>When this behavior is selected, the MainThreadFcn will set the UseState to Stopped when the part is stopped if the UseState is in an IsOnlineOrAttemptOnline state.  [0x08]</summary>
        MainThreadStopSetsStateToStoppedIfIsOnlineOrAttemptOnline = 0x08,

        /// <summary>When this behavior is selected, the MainThreadFcn will set the UseState to Stopped when the part is stopped if the UseState is Offline.  [0x10]</summary>
        MainThreadStopSetsStateToStoppedIfOffline = 0x10,

        /// <summary>When this behavior is selected, the PerformMainLoopService will call ServiceBusyConditionChangeDetection.  When not selected, only the MainThreadFcn will call this method. [0x20]</summary>
        PerformMainLoopServiceCallsServiceBusyConditionChangeDetection = 0x20,
    }

    /// <summary>
    /// Public and published interface about an action.
    /// </summary>
    public interface IActionInfo
    {
        /// <summary>Gives the basic "name" of the action</summary>
        string Description { get; }

        /// <summary>Gives the basic "name" of the action with details (such as the content of parameters and/or of any NamedParamValues)</summary>
        string DescriptionWithDetails { get; }

        /// <summary>Gives the last seens IActionState for this action</summary>
        IActionState ActionState { get; }

        /// <summary>Reteurns true if this ActionInfo instance is empty</summary>
        bool IsEmpty { get; }

        /// <summary>Custom variant of normal ToString method that gives caller access to which parts of the action they want included in the string.</summary>
        string ToString(ToStringSelect toStringSelect);
    }

    /// <summary>
    /// Implementation object for IActionInfo.  This class is immutable.
    /// </summary>
    [DataContract(Namespace = Constants.ModularNameSpace), Serializable]
    public class ActionInfo : IActionInfo
    {
        /// <summary>
        /// Constructs an empty ActionInfo object.
        /// </summary>
        public ActionInfo()
        {
            Description = string.Empty;
            DescriptionWithDetails = string.Empty;
            ActionState = ActionStateCopy.Empty;
        }

        /// <summary>
        /// Constructs an ActionInfo object with contents derived from the given action's description and state.
        /// </summary>
        public ActionInfo(IProviderFacet ipf)
            : this(ipf, ipf.ActionState)
        { }

        /// <summary>
        /// Constructs an ActionInfo object with contents derived from the given action's description and the given actionState.
        /// </summary>
        public ActionInfo(IProviderFacet ipf, IActionState actionState)
        {
            IClientFacet icf = ipf as IClientFacet;
            if (icf != null)
            {
                Description = icf.ToString(ToStringSelect.JustMesg);
                DescriptionWithDetails = icf.ToString(ToStringSelect.MesgAndDetail);
            }
            else
            {
                Description = DescriptionWithDetails = ipf.ToString();
            }

            ActionState = actionState;
        }

        /// <summary>
        /// Constructs an ActionInfo object with descriptions copied from the given copyDescriptionsFrom object and with the given actionState
        /// </summary>
        public ActionInfo(IActionInfo copyDescriptionsFrom, IActionState actionState)
        {
            Description = copyDescriptionsFrom.Description;
            DescriptionWithDetails = copyDescriptionsFrom.DescriptionWithDetails;
            ActionState = actionState;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public ActionInfo(IActionInfo rhs)
        {
            Description = rhs.Description;
            DescriptionWithDetails = rhs.DescriptionWithDetails;
            ActionState = rhs.ActionState;
        }

        /// <summary>
        /// Helper function for debugging and logging
        /// </summary>
        public override string ToString()
        {
            return ToString(ToStringSelect.MesgDetailAndState);
        }

        /// <summary>Reteurns true if this ActionInfo instance is empty</summary>
        public bool IsEmpty 
        {
            get
            {
                return (Description.IsNullOrEmpty() && DescriptionWithDetails.IsNullOrEmpty() && ActionState.IsEmpty());
            }
        }

        /// <summary>Custom variant of normal ToString method that gives caller access to which parts of the action they want included in the string.</summary>
        public string ToString(ToStringSelect toStringSelect)
        {
            string actionName = (toStringSelect == ToStringSelect.JustMesg) ? Description : DescriptionWithDetails;

            if (IsEmpty)
                return "[EmtpyActionState]";
            else if (ActionState == null)
                return "{0} [state is null]".CheckedFormat(actionName);
            else if (ActionState.Succeeded)
                return "{0} Succeeded".CheckedFormat(actionName);
            else if (ActionState.Failed)
                return "{0} Failed: {1}".CheckedFormat(actionName, ActionState.ResultCode);
            else
                return "{0} {1}".CheckedFormat(actionName, ActionState.StateCode);
        }

        /// <summary>Gives the basic "name" of the action</summary>
        [DataMember(Order = 100)]
        public string Description { get; private set; }

        /// <summary>Gives the basic "name" of the action with details (such as the content of parameters and/or of any NamedParamValues)</summary>
        [DataMember(Order = 200)]
        public string DescriptionWithDetails { get; private set; }

        /// <summary>Gives the last seens IActionState for this action</summary>
        public IActionState ActionState { get; private set; }

        /// <summary>Support for DataContract serialization of this object</summary>
        [DataMember(Order = 300, Name = "ActionState")]
        private ActionStateCopy ActionStateCopy
        {
            get { return new ActionStateCopy(ActionState); }
            set { ActionState = value; }
        }

        private static readonly IActionInfo emptyActionInfo = new ActionInfo();

        public static IActionInfo EmptyActionInfo { get { return emptyActionInfo; } }
    }

    /// <summary>
    /// This structure contains all of the "settings" values that are used by SimpleActivePartBase's implementation.
    /// This structure is typically used by derived objects to comparmentalize and simplify how they configure their 
    /// base class.  This will also allow this object to provide a variety of static preconfigured settings values.
    /// It is also expected to simplify addition of new settings in the future
    /// </summary>
    public struct SimpleActivePartBaseSettings
    {
        /// <summary>
        /// When this property is set to true, the BaseState will automatically transition between Busy and Idle when Actions implementations are invoked and return.  
        /// When it is false the derived object will be entirely responsible for causing the component to transition between the Busy and Idle states.  
        /// <para/>Defaults to false
        /// </summary>
        public bool AutomaticallyIncAndDecBusyCountAroundActionInvoke { get; set; }

        /// <summary>
        /// Defines the logic that this SimpleActivePart is using for clearing the threadWakeupNotifier object that is used in WaitForSomethingToDo calls.
        /// <para/>Defaults to ThreadWakeupNotifierResetHandling.ExplicitlyResetNotifierAtStartOfEachLoop
        /// </summary>
        public ThreadWakeupNotifierResetHandling ThreadWakeupNotifierResetHandling { get; set; }

        /// <summary>
        /// Defines the maximum number of Actions that can be invoked per iteration of the outer service loop (ie per call to PerformMainLoopService/WaitForSomethingToDo).
        /// <para/>Default value is 1 (applied during SetupForUse), setter will Clip the given value to be between 1 and 100.
        /// </summary>
        public int MaxActionsToInvokePerServiceLoop { get { return maxActionsToInvokePerServiceLoop; } set { maxActionsToInvokePerServiceLoop = value.Clip(1, 100); } }
        private int maxActionsToInvokePerServiceLoop;

        /// <summary>
        /// Defines this part's base state handling for the GoOnline and GoOffline actions.  Each derived class should update this value as desired.
        /// <para/>Defaults to GoOnlineAndGoOfflineHandling.None for backwards compatibility.
        /// </summary>
        public GoOnlineAndGoOfflineHandling GoOnlineAndGoOfflineHandling { get; set; }

        /// <summary>Set this to an IPI to request the part to register itself with a specific IPI instance.  Explicitly set this to null to indicate that the part should not register itself with any IPI instance (even the default one)</summary>
        public IPartsInterconnection RegisterPartWith { get { return registerPartWith; } set { registerPartWith = value; registerPartWithHasBeenSet = true; } }
        internal IPartsInterconnection registerPartWith;
        internal bool registerPartWithHasBeenSet;

        /// <summary>
        /// Defines this part's behavior option flag value(s).
        /// <para/>Default to SimpleActivePartBehaviorOptions.None for backwards compatibility.
        /// </summary>
        public SimpleActivePartBehaviorOptions SimpleActivePartBehaviorOptions { get; set; }

        /// <summary>
        /// Contains the default WaitTimeLimit for the Part's thread.  Generally used when calling WaitForSomethingToDo().  Setter clamps value to be between minWaitTimeLimit (0.0) and maxWaitTimeLimit (0.5) seconds
        /// <para/>This will be set to 0.10 seconds in SetupForUse if no explicit value has been provided earlier.
        /// </summary>
        public TimeSpan WaitTimeLimit { get { return waitTimeLimit; } set { waitTimeLimit = value.Clip(minWaitTimeLimit, maxWaitTimeLimit); waitTimeLimitHasBeenSet = true; } }
        private TimeSpan waitTimeLimit;
        private bool waitTimeLimitHasBeenSet;

        /// <summary>Defines the maximum value that the WaitTimeLimit property can be set to.  0.5 seconds.</summary>
        public static readonly TimeSpan maxWaitTimeLimit = TimeSpan.FromSeconds(0.5);

        /// <summary>Defines the minimum value that the WaitTimeLimit property can be set to.  0.0 seconds.</summary>
        public static readonly TimeSpan minWaitTimeLimit = TimeSpan.FromSeconds(0.0);

        /// <summary>
        /// Contains SimplePartBaseSettings that will be used by the SimplePartBase class
        /// </summary>
        public SimplePartBaseSettings SimplePartBaseSettings { get { return simplePartBaseSettings; } set { simplePartBaseSettings = value; } }
        internal SimplePartBaseSettings simplePartBaseSettings;

        /// <summary>
        /// This method is called during Settings assignment.  It applies any required settings changes of struct default values.
        /// <para/>if MaxActionsToInvokePerServiceLoop is zero, it will be set to 1.
        /// <para/>if WaitTimeLimit property has not been explicitly set, it will be set to 0.1 seconds
        /// </summary>
        public SimpleActivePartBaseSettings SetupForUse()
        {
            if (maxActionsToInvokePerServiceLoop == 0)
                maxActionsToInvokePerServiceLoop = 1;

            if (!waitTimeLimitHasBeenSet)
                WaitTimeLimit = TimeSpan.FromSeconds(0.1);

            simplePartBaseSettings.SetupForUse();

            return this;
        }

        /// <summary>
        /// returns a constructor default SimpleActivePartBaseSettings value.
        /// <para/>Please note: unless explicitly assigned by the client the default, unset, value of WaitTimeLimit will be replaced with 0.1 seconds by the Part when it uses this object's SetupForUse method.
        /// </summary>
        public static SimpleActivePartBaseSettings DefaultVersion0 { get { return new SimpleActivePartBaseSettings(); } }

        /// <summary>
        /// returns the first non-constructor default SimpleActivePartBaseSettings value (established under MosaicLibCS 0.1.6.0):
        /// <para/>AutomaticallyIncAndDecBusyCountAroundActionInvoke = true,
        /// <para/>GoOnlineAndGoOfflineHandling = GoOnlineAndGoOfflineHandling.All,
        /// <para/>SimpleActivePartBehaviorOptions = SimpleActivePartBehaviorOptions.PerformActionPublishesActionInfo,
        /// <para/>SimplePartBaseSettings = SimplePartBaseSettings.DefaultVersion1 (SimplePartBaseBehavior = SimplePartBaseBehavior.All (TreatPartAsBusyWhenQueueIsNotEmpty | TreatPartAsBusyWhenInternalPartBusyCountIsNonZero)),
        /// <para/>Please note: unless explicitly assigned by the client the default, unset, value of WaitTimeLimit will be replaced with 0.1 seconds by the Part when it uses this object's SetupForUse method.
        /// </summary>
        public static SimpleActivePartBaseSettings DefaultVersion1 
        { 
            get 
            { 
                return new SimpleActivePartBaseSettings() 
                { 
                    AutomaticallyIncAndDecBusyCountAroundActionInvoke = true,
                    GoOnlineAndGoOfflineHandling = GoOnlineAndGoOfflineHandling.All,
                    SimpleActivePartBehaviorOptions = SimpleActivePartBehaviorOptions.PerformActionPublishesActionInfo,
                    SimplePartBaseSettings = SimplePartBaseSettings.DefaultVersion1,
                }; 
            } 
        }

        /// <summary>
        /// returns the second non-constructor default SimpleActivePartBaseSettings value (established under MosaicLibCS 0.1.6.1):
        /// <para/>AutomaticallyIncAndDecBusyCountAroundActionInvoke = true,
        /// <para/>GoOnlineAndGoOfflineHandling = GoOnlineAndGoOfflineHandling.All | GoOnlineAndGoOfflineHandling.UseOnlineUnititializedState | GoOnlineAndGoOfflineHandling.AcceptCustomChangeFromAttemptOnlineState,
        /// <para/>SimpleActivePartBehaviorOptions = SimpleActivePartBehaviorOptions.PerformActionPublishesActionInfo | SimpleActivePartBehaviorOptions.MainThreadStartSetsStateToOffline | SimpleActivePartBehaviorOptions.MainThreadStopSetsStateToStoppedIfIsOnline | SimpleActivePartBehaviorOptions.MainThreadStopSetsStateToStoppedIfOffline | SimpleActivePartBehaviorOptions.UseMainThreadFailedState | SimpleActivePartBehaviorOptions.PerformMainLoopServiceCallsServiceBusyConditionChangeDetection,
        /// <para/>SimplePartBaseSettings = SimplePartBaseSettings.DefaultVersion2 (SimplePartBaseBehavior = SimplePartBaseBehavior.All (TreatPartAsBusyWhenQueueIsNotEmpty | TreatPartAsBusyWhenInternalPartBusyCountIsNonZero)),
        /// <para/>Please note: unless explicitly assigned by the client the default, unset, value of WaitTimeLimit will be replaced with 0.1 seconds by the Part when it uses this object's SetupForUse method.
        /// </summary>
        public static SimpleActivePartBaseSettings DefaultVersion2
        {
            get
            {
                return new SimpleActivePartBaseSettings()
                {
                    AutomaticallyIncAndDecBusyCountAroundActionInvoke = true,
                    GoOnlineAndGoOfflineHandling = GoOnlineAndGoOfflineHandling.All | GoOnlineAndGoOfflineHandling.UseOnlineUnititializedState | GoOnlineAndGoOfflineHandling.AcceptCustomChangeFromAttemptOnlineState,
                    SimpleActivePartBehaviorOptions = SimpleActivePartBehaviorOptions.PerformActionPublishesActionInfo | SimpleActivePartBehaviorOptions.MainThreadStartSetsStateToOffline | SimpleActivePartBehaviorOptions.MainThreadStopSetsStateToStoppedIfIsOnlineOrAttemptOnline | SimpleActivePartBehaviorOptions.MainThreadStopSetsStateToStoppedIfOffline | SimpleActivePartBehaviorOptions.UseMainThreadFailedState | SimpleActivePartBehaviorOptions.PerformMainLoopServiceCallsServiceBusyConditionChangeDetection,
                    SimplePartBaseSettings = SimplePartBaseSettings.DefaultVersion2,
                };
            }
        }
    }

    /// <summary>Standard extension methods wrapper class/namespace</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given GoOnlineAndGoOfflineHandling value has the indicated flag set (present)
        /// </summary>
        public static bool CheckFlag(this SimpleActivePartBaseSettings settings, GoOnlineAndGoOfflineHandling checkFlag)
        {
            return ((settings.GoOnlineAndGoOfflineHandling & checkFlag) == checkFlag);
        }

        /// <summary>
        /// Returns true if the given SimpleActivePartBehaviorOptions value has the indicated flag value(s) set (present)
        /// </summary>
        public static bool CheckFlag(this SimpleActivePartBaseSettings settings, SimpleActivePartBehaviorOptions checkFlag)
        {
            return ((settings.SimpleActivePartBehaviorOptions & checkFlag) == checkFlag);
        }

        /// <summary>
        /// SimpleActivePartBaseSettings content builder helper extension method.  
        /// </summary>
        public static SimpleActivePartBaseSettings Build(this SimpleActivePartBaseSettings settings,
                                                         bool? automaticallyIncAndDecBusyCountAroundActionInvoke = null,
                                                         int? maxActionsToInvokePerServiceLoop = null,
                                                         GoOnlineAndGoOfflineHandling? goOnlineAndOfflineHandling = null,
                                                         SimpleActivePartBehaviorOptions? simpleActivePartBehaviorOptions = null,
                                                         TimeSpan? waitTimeLimit = null,
                                                         SimplePartBaseSettings? simplePartBaseSettings = null,
                                                         IValuesInterconnection partBaseIVI = null,
                                                         bool setBaseStatePublicationValueNameToNull = false,
                                                         Logging.LogGate? initialInstanceLogGate = null,
                                                         string logGroupName = null,
                                                         IPartsInterconnection registerPartWith = null,
                                                         bool disablePartRegistration = false,
                                                         LoggingOptionSelect? loggingOptionSelect = null
                                                         )
        {
            if (automaticallyIncAndDecBusyCountAroundActionInvoke.HasValue)
                settings.AutomaticallyIncAndDecBusyCountAroundActionInvoke = automaticallyIncAndDecBusyCountAroundActionInvoke.Value;

            if (maxActionsToInvokePerServiceLoop.HasValue)
                settings.MaxActionsToInvokePerServiceLoop = maxActionsToInvokePerServiceLoop.Value;

            if (goOnlineAndOfflineHandling.HasValue)
                settings.GoOnlineAndGoOfflineHandling = goOnlineAndOfflineHandling.Value;

            if (simpleActivePartBehaviorOptions.HasValue)
                settings.SimpleActivePartBehaviorOptions = simpleActivePartBehaviorOptions.Value;

            if (waitTimeLimit.HasValue)
                settings.WaitTimeLimit = waitTimeLimit.Value;

            if (simplePartBaseSettings.HasValue)
                settings.SimplePartBaseSettings = simplePartBaseSettings.Value;

            if (partBaseIVI != null)
                settings.simplePartBaseSettings.PartBaseIVI = partBaseIVI;

            if (setBaseStatePublicationValueNameToNull)
                settings.simplePartBaseSettings.BaseStatePublicationValueName = null;

            if (initialInstanceLogGate != null)
                settings.simplePartBaseSettings.InitialInstanceLogGate = initialInstanceLogGate;

            if (logGroupName != null)
                settings.simplePartBaseSettings.LogGroupName = logGroupName;

            if (disablePartRegistration)
                settings.RegisterPartWith = null;
            else if (registerPartWith != null)
                settings.RegisterPartWith = registerPartWith;

            if (loggingOptionSelect != null)
                settings.simplePartBaseSettings.LoggingOptionSelect = loggingOptionSelect ?? default(LoggingOptionSelect);

            return settings;
        }
    }

    /// <summary>
	/// This abstract class is the standard base class to be used by most types of Active Parts.  
	/// It derives from SimplePartBase, and implements IActivePartBase and INotifyable.
	/// </summary>
	/// <remarks>
	/// This class requires that the derived class implement the following methods:
	///		[none - all virtual methods have defaults provided].  Class is marked abstract so derived class must be provided in any case.
	/// 
	/// This class allows the derived class to override the following methods/properties:
	///		DisposeCalledPassdown, 
	///		StartPart, StopPart,
	///		CreateGoOnlineAction (x2), CreateGoOfflineAction, CreateServiceAction (x2),
	///		PerformGoOnlineAction (x2), PerformGoOnlineAndInitializeAction, PerformGoOfflineAction (x2), PerformServiceAction (x2)
	///		MainThreadFcn, PerformMainLoopService, IssueNextQueuedAction, PerformAction, WaitForSomethingToDo
	///		
	///		Of these the following are expeted to be overriden in most Active Parts:
	///		DisposeCalledPassdown, PerformGoOnlineAction, PerformGoOfflineAction, PerformMainLoopService
	/// 
	/// This class defines the following sub-classes for local use and for use by derived classes:
	///		BasicActionImpl, StringActionImpl.
	///		
	/// INotifyable interface allows class instance to become the receiver for a BasicNotification event.  
    /// This behavior sets the mThreadWakeupNotifier so that the thread will wakeup whenever the object is signaled/Notified.
	/// </remarks>
	public abstract class SimpleActivePartBase : SimplePartBase, IActivePartBase, INotifyable
	{
        //-----------------------------------------------------------------
        #region sub classes (BasicActionImpl, ParamActionImplBase, BoolActionImpl, StringActionImpl)

        /// <summary>Normal Implementation object for IBaseAction type Actions.  Derives from ActionImplBase{NullObj, NullObj}.</summary>
		public class BasicActionImpl : ActionImplBase<NullObj, NullObj>, IBasicAction
		{
            /// <summary>Constructor for use with simple action method delegate: ActionMethodDelegateStrResult.</summary>
            public BasicActionImpl(ActionQueue actionQ, ActionMethodDelegateStrResult method, string mesg, ActionLogging loggingReference, string mesgDetails = null)
                : base(actionQ, method, loggingReference, mesg: mesg, mesgDetails: mesgDetails) 
            {}
            /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{NullObj, NullObj}.</summary>
            public BasicActionImpl(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, NullObj> method, string mesg, ActionLogging loggingReference, string mesgDetails = null)
                : base(actionQ, null, false, method, loggingReference, mesg: mesg, mesgDetails: mesgDetails) 
            { }
            /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{NullObj, NullObj}.</summary>
            public BasicActionImpl(ActionQueue actionQ, FullActionMethodDelegate<NullObj, NullObj> method, string mesg, ActionLogging loggingReference, string mesgDetails = null)
                : base(actionQ, null, false, method, loggingReference, mesg: mesg, mesgDetails: mesgDetails) 
            { }
		}

        /// <summary>Implementation object for IClientFacetWithParam{ParamType} type Actions.  Derives from ActionImplBase{ParamType, NullObj}.</summary>
        public class ParamActionImplBase<ParamType> : ActionImplBase<ParamType, NullObj>, IBasicAction, IClientFacetWithParam<ParamType>
		{
            /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{ParamType, NullObj}.</summary>
            public ParamActionImplBase(ActionQueue actionQ, ParamType paramValue, ActionMethodDelegateActionArgStrResult<ParamType, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, false, method, loggingReference, mesg: mesg, mesgDetails: "{0}".CheckedFormat(paramValue)) 
            { }
            /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{ParamType, NullObj}.</summary>
            public ParamActionImplBase(ActionQueue actionQ, ParamType paramValue, FullActionMethodDelegate<ParamType, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, false, method, loggingReference, mesg: mesg, mesgDetails: "{0}".CheckedFormat(paramValue)) 
            { }

            /// <summary>
            /// Allows the caller to attempt to set the Action's ParamValue to the given value and updates the Logging.MesgDetail to value.ToString() if the value was updated successfully.
            /// Overrides ActionImplBase{ParamType, NullObj} version of same method.
            /// </summary>
            /// <returns>true if the overriden SetParamValue method succeeded.</returns>
			public override bool SetParamValue(ParamType value) 
            { 
                bool done = base.SetParamValue(value); 
                if (done)
                    Logging.MesgDetail = "{0}".CheckedFormat(value);
                return done; 
            }

            /// <summary>Get or Set the Param value.  Setter updates the Logging.MesgDetail to value.ToString() after successfully setting the base.ParamValue to the given value.</summary>
            /// <exception cref="System.FieldAccessException">thrown if setter is used when IsParamValueSettable is false.</exception>
            public override ParamType ParamValue 
            { 
                set 
                { 
                    base.ParamValue = value;
                    Logging.MesgDetail = "{0}".CheckedFormat(value);
                } 
            }
		}

        /// <summary>Implementation object for IBoolParamAction type Actions.  Derives from ParamActionImplBase{bool}.</summary>
        public class BoolActionImpl : ParamActionImplBase<bool>, IBoolParamAction
        {
            /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{bool, NullObj}.</summary>
            public BoolActionImpl(ActionQueue actionQ, bool paramValue, ActionMethodDelegateActionArgStrResult<bool, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, method, mesg, loggingReference) 
            { }
            /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{bool, NullObj}.</summary>
            public BoolActionImpl(ActionQueue actionQ, bool paramValue, FullActionMethodDelegate<bool, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, method, mesg, loggingReference) 
            { }
        }

        /// <summary>Implementation object for IStringParamAction type Actions.  Derives from ParamActionImplBase{string}.</summary>
        public class StringActionImpl : ParamActionImplBase<string>, IStringParamAction
		{
            /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{string, NullObj}.</summary>
            public StringActionImpl(ActionQueue actionQ, string paramValue, ActionMethodDelegateActionArgStrResult<string, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, method, mesg, loggingReference) 
            { }
            /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{string, NullObj}.</summary>
            public StringActionImpl(ActionQueue actionQ, string paramValue, FullActionMethodDelegate<string, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, method, mesg, loggingReference) 
            { }
		}

		#endregion

		//-----------------------------------------------------------------
		#region CTOR and DTOR (et. al.)

        /// <summary>
        /// Constructor variant: caller provides PartID.
        /// PartType will be automatically derived from the class name of the derived type (aka the name of the caller's DeclaringType) 
        /// <para/>PartID: use given value, PartType: use declaring class name (by reflection), WaitTimeLimit: 0.1 sec, enableQueue:true, queueSize:10
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="initialSettings">Defines the initial set of settings that the part will use.  If this value is null then the part uses default settings with WaitTimeLimit set to 0.1 seconds.</param>
        /// <param name="enableQueue">Set to true for the parts ActionQueue to be enabled even before the part has been started.  Set to false to prevent actions from being started until this part has been started and has enabled its queue.</param>
        /// <param name="queueSize">Defines the maximum number of pending actions that may be placed in the queue before further attempts to start new actions will be blocked and will fail.</param>
        public SimpleActivePartBase(string partID, SimpleActivePartBaseSettings? initialSettings = null, bool enableQueue = true, int queueSize = 10)
            : this(partID, new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(), initialSettings: initialSettings, enableQueue: enableQueue, queueSize: queueSize) 
        {}

        /// <summary>
        /// Constructor variant: caller provides PartID, nominal Service loop WaitTimeLimit.
        /// PartType will be automatically derived from the class name of the derived type (aka the name of the caller's DeclaringType) 
        /// queueSize and initial value for queue enable are optional (default to 10 and true)
        /// <para/>PartID: use given value, PartType: use declaring class name (by reflection), WaitTimeLimit: use given value, enableQueue:use given value, queueSize:use given value
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="waitTimeLimit">Defines the nominal maximum period that the part's outer main thread loop will wait for the next notify occurrance.  Sets the default "spin" rate for the part.</param>
        /// <param name="enableQueue">Set to true for the parts ActionQueue to be enabled even before the part has been started.  Set to false to prevent actions from being started until this part has been started and has enabled its queue.</param>
        /// <param name="queueSize">Defines the maximum number of pending actions that may be placed in the queue before further attempts to start new actions will be blocked and will fail.</param>
        public SimpleActivePartBase(string partID, TimeSpan waitTimeLimit, bool enableQueue = true, int queueSize = 10)
            : this(partID, new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(), waitTimeLimit, enableQueue, queueSize)
        {}

        /// <summary>
        /// Constructor variant: caller provides PartID, PartType, nominal Service loop WaitTimeLimit.
        /// queueSize and initial value for queue enable are optional (default to 10 and true)
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        /// <param name="waitTimeLimit">Defines the nominal maximum period that the part's outer main thread loop will wait for the next notify occurrance.  Sets the default "spin" rate for the part.</param>
        /// <param name="enableQueue">Set to true for the parts ActionQueue to be enabled even before the part has been started.  Set to false to prevent actions from being started until this part has been started and has enabled its queue.</param>
        /// <param name="queueSize">Defines the maximum number of pending actions that may be placed in the queue before further attempts to start new actions will be blocked and will fail.</param>
        public SimpleActivePartBase(string partID, string partType, TimeSpan waitTimeLimit, bool enableQueue = true, int queueSize = 10)
            : this(partID, partType, enableQueue : enableQueue, queueSize : queueSize,
                    initialSettings: new SimpleActivePartBaseSettings() 
                        { 
                            WaitTimeLimit = waitTimeLimit, 
                            SimplePartBaseSettings = new SimplePartBaseSettings() { SimplePartBaseBehavior = SimplePartBaseBehavior.TreatPartAsBusyWhenInternalPartBusyCountIsNonZero } 
                        }
                    )
        { }

        /// <summary>
        /// Constructor variant: caller provides PartID, PartType, nominal Service loop WaitTimeLimit.
        /// queueSize and initial value for queue enable are optional (default to 10 and true)
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        /// <param name="initialSettings">Defines the initial set of settings that the part will use.  If this value is null then the part uses default settings with WaitTimeLimit set to 0.1 seconds.</param>
        /// <param name="enableQueue">Set to true for the parts ActionQueue to be enabled even before the part has been started.  Set to false to prevent actions from being started until this part has been started and has enabled its queue.</param>
        /// <param name="queueSize">Defines the maximum number of pending actions that may be placed in the queue before further attempts to start new actions will be blocked and will fail.</param>
        public SimpleActivePartBase(string partID, string partType, SimpleActivePartBaseSettings ? initialSettings = null, bool enableQueue = true, int queueSize = 10)
            : base(partID, partType, initialSettings: (initialSettings.HasValue ? ((SimplePartBaseSettings ?) initialSettings.Value.SimplePartBaseSettings) : null))
        {
            if (initialSettings != null)
                settings = initialSettings.GetValueOrDefault();

            settings = settings.SetupForUse();

			actionQ = new ActionQueue(partID + ".q", enableQueue, queueSize);

            IBasicNotificationList notificiationList = actionQ.NotifyOnEnqueue;
            notificiationList.AddItem(threadWakeupNotifier);
            AddExplicitDisposeAction(() => 
                {
                    notificiationList.RemoveItem(threadWakeupNotifier);
                    threadWakeupNotifier.Release();
                });

			actionLoggingReference = new ActionLogging(Log, ActionLoggingConfig.Info_Error_Debug_Debug);

            if (!settings.registerPartWithHasBeenSet)
                Interconnect.Parts.Parts.Instance.RegisterPart(this);
            else if (settings.registerPartWith != null)
                settings.registerPartWith.RegisterPart(this);
        }

        /// <summary>
        /// Protected sealed implementation for DisposableBase.Dispose(DisposeType) abstract method.
        /// During an explicit dispose this will Stop the part and then it will invoke the DisposeCallPassdown to 
        /// allow derived classes to implement custom dispose logic after the part has been stopped.
        /// <para/>This method is sealed so that sub classes are required to make use of the DisposeCalledPassdown pattern or to add their
        /// explicitDiposeAction to the list thereof by calling the PartBaseBase.AddExplicitDisposeAction to add such an action to the PartBaseBase's
        /// list of such actions that it will invoke (in LIFO order) when it is being disposed.
        /// </summary>
		protected sealed override void Dispose(DisposeType disposeType)
		{
			if (disposeType == DisposeType.CalledExplicitly)
				StopPart();

			DisposeCalledPassdown(disposeType);

            base.Dispose(disposeType);
		}

        /// <summary>
        /// Protected virtual method that may be overriden by by derived classes to implement disposal of any resources that have been allocated.
        /// Implementation at this level has an empty body.
        /// </summary>
        /// <param name="disposeType">Indicates if the Dispose in progress is DisposeType.CalledExplicitly or if it has been DisposeType.CalledByFinalizer</param>
		protected virtual void DisposeCalledPassdown(DisposeType disposeType) 
        {}

		#endregion

        //-----------------------------------------------------------------
        #region Operational Settings

        /// <summary>
        /// Settings property gives derived objects access to all of the base class "settings" as a single set.
        /// The use of this property will apply the SetupForUse method to the given value and then replace the use of prior seperate settings values in the SimpleActivePartBase class with the updated given value.
        /// </summary>
        public new SimpleActivePartBaseSettings Settings 
        { 
            get { return settings; }
            protected set 
            { 
                settings = value.SetupForUse(); 
                base.Settings = settings.SimplePartBaseSettings; 
            } 
        }

        /// <summary>
        /// This protected Settings field gives derived objects direct read/write access to the SimpleActivePartSettings storage that is used by the part.  
        /// This may be useful in cases where the derived part wants to be able to make incremental changes to the current Settings without using the standard value object property pattern for incremental changes.
        /// <para/>WARNING: if a derived part replaces the settings using this method, then it must use the SetupForUse settings method on the new value before assigning it to this field.
        /// </summary>
        protected new SimpleActivePartBaseSettings settings = new SimpleActivePartBaseSettings();

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        public bool AutomaticallyIncAndDecBusyCountAroundActionInvoke  { get { return settings.AutomaticallyIncAndDecBusyCountAroundActionInvoke; } protected set { settings.AutomaticallyIncAndDecBusyCountAroundActionInvoke = value; } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        public ThreadWakeupNotifierResetHandling ThreadWakeupNotifierResetHandling { get { return settings.ThreadWakeupNotifierResetHandling; } protected set { settings.ThreadWakeupNotifierResetHandling = value; } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        public int MaxActionsToInvokePerServiceLoop { get { return settings.MaxActionsToInvokePerServiceLoop; } set { settings.MaxActionsToInvokePerServiceLoop = value.Clip(1, 100); } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        protected GoOnlineAndGoOfflineHandling GoOnlineAndGoOfflineHandling { get { return settings.GoOnlineAndGoOfflineHandling; } set { settings.GoOnlineAndGoOfflineHandling = value; } }

        [Obsolete("Please replace the use of this method with the corresponding extension method on the matching part's Settings property (2017-01-20)")]
        protected bool CheckFlag(GoOnlineAndGoOfflineHandling checkFlag) { return settings.CheckFlag(checkFlag); }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        protected SimpleActivePartBehaviorOptions SimpleActivePartBehaviorOptions { get { return settings.SimpleActivePartBehaviorOptions; } set { settings.SimpleActivePartBehaviorOptions = value; } }

        /// <summary>
        /// Returns true if property SimpleActivePartBehaviorOptions has the indicated flag value(s) set in its current value
        /// </summary>
        [Obsolete("Please replace the use of this method with the corresponding extension method on the matching part's Settings property (2017-01-20)")]
        protected bool CheckFlag(SimpleActivePartBehaviorOptions checkFlag) { return settings.CheckFlag(checkFlag); }

        [Obsolete("Please replace the use of this property with use of the corresponding one in the part's Settings (2017-01-20)")]
        protected TimeSpan WaitTimeLimit { get { return settings.WaitTimeLimit; } set { settings.WaitTimeLimit = value; } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        protected TimeSpan waitTimeLimit { get { return settings.WaitTimeLimit; } set { settings.WaitTimeLimit = value; } }

        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        protected static TimeSpan maxWaitTimeLimit { get { return SimpleActivePartBaseSettings.maxWaitTimeLimit; } }
        [Obsolete("Please replace the use of this property with the corresponding one in the part's Settings (2017-01-20)")]
        protected static TimeSpan minWaitTimeLimit { get { return SimpleActivePartBaseSettings.minWaitTimeLimit; } }

        #endregion

        //-----------------------------------------------------------------
        #region ActionLogger adjustments

        /// <summary>Setting this property causes the part's reference ActionLogger to be updated to use the newly specified emitter levels for all newly created ActionLoggers.</summary>
        /// <remarks>
        /// This proerty is only intended to allow derived classes to change the ActionLoggingConfig during part construction.
        /// Changing this property while actions are being created will allow the actions resulting log messages to be taken from an arbitrary mix of the prior and new mesg levels as 
        /// the reference copy of the emitters may be updating while the reference actionLogger is being copied.
        /// </remarks>
        protected ActionLoggingConfig ActionLoggingConfig { get { return actionLoggingReference.Config; } set { actionLoggingReference.Config = value; } }

        #endregion

        //-----------------------------------------------------------------
		#region IActivePartBase interface methods

        private volatile bool hasBeenStarted = false;
        private volatile bool hasBeenStopped = false;
        private volatile bool hasStopBeenRequested = false;

        /// <summary>
        /// Stub for behavior derived objects would like to perform before the main action queue is enabled.
        /// WARNING: this method is not called by the Part's thread - it is called by some external client thread.
        /// </summary>
        protected virtual void PreStartPart() 
        { }

        /// <summary>
        /// Method used to Start the part.  Some parts automatically start whenever a GoOnlineAction is created.
        /// This method may only safely be used on a part that has not already been started and which is not already being
        /// started on another thread.
        /// </summary>
        public virtual void StartPart()
		{
			using (Logging.EnterExitTrace t = new Logging.EnterExitTrace(Log, "StartPart", Logging.MesgType.Debug))
			{
                PreStartPart();

				if (actionQ != null)
					actionQ.QueueEnable = true;
				else
					throw new System.NullReferenceException(FmtStdEC("Failed to construct action queue"));

                if (mainThread == null)
                {
                    mainThread = new System.Threading.Thread(MainThreadFcn)
                    {
                        Name = PartID,
                    };

                    mainThread.Start();
                }
                else if (mainThread.ThreadState == System.Threading.ThreadState.Unstarted)
                {
                    mainThread.Start();
                }

                hasBeenStarted = true;
			}
		}

        /// <summary>
        /// Stub for behavior derived objects would like to perform immediately before the main action queue is disabled.
        /// WARNING: this method is not called by the Part's thread - it is called by some external client thread.
        /// </summary>
        protected virtual void PreStopPart() 
        { }

        /// <summary>
        /// Method is used to gracefully stop the Part's main thread, typically at the end of the current action.
        /// </summary>
		public virtual void StopPart()
		{
			using (Logging.EnterExitTrace t = new Logging.EnterExitTrace(Log, "StopPart", Logging.MesgType.Debug))
			{
                PreStopPart();

                if (actionQ != null)
					actionQ.QueueEnable = false;	// this tells the thread to exit (if it is already running)

                hasStopBeenRequested = true; 

                if (mainThread != null)
				{
					threadWakeupNotifier.Notify();

					mainThread.Join();
                    joinedThread = mainThread;
					mainThread = null;
				}

				Log.Info.Emit("Part stopped");

                hasBeenStopped = true;
			}
		}

        /// <summary>Returns true if the part has been started.  May be true before main loop has started.</summary>
        public virtual bool HasBeenStarted { get { return (hasBeenStarted); } }
        /// <summary>Returns true if the part has been stopped.  Will be true if the Part's main thread has stopped or if the Part's main actionQ has been disabled.</summary>
        public virtual bool HasBeenStopped { get { return (hasBeenStopped || !AreAnyActionQueueEnabled); } }
        /// <summary>Returns true if the part has been started and it has not been stopped (yet).</summary>
        public virtual bool IsRunning { get { return (HasBeenStarted && !HasBeenStopped); } }

        /// <summary>static mutex object used to make certain that only one thread at a time runs each Part's StartIfNeeded pattern.</summary>
		private static readonly object startIfNeededMutex = new object();

        /// <summary>
        /// This method determines if the part has already been started and starts the part if not.
        /// This method is serialized through the use of a lock so that it gives consistent behavior even
        /// if it is called from multiple threads.  Use of this method cannot be safely combined with the use of the direct StartPart method.
        /// Locks the startIfNeeded mutex and then calls StartPart if the part's HasBeenStarted flag is false.
        /// Use of mutex protects against re-enterant use of CreateGoOnlineAction but not against concurrent patterns where 
        /// CreateGoOnlineAction is used concurrently with an explicit call to StartPart.
        /// <para/>Supports call chaining
        /// </summary>
        public IActivePartBase StartPartIfNeeded()
		{
            if (HasBeenStarted)
                return this;

			lock (startIfNeededMutex)		// only one caller of StartPartIfNeeded will be processed at a time (system wide...)
			{
                if (!HasBeenStarted)
					StartPart();
			}

            return this;
		}

        /// <summary>
        /// Provide the default parameterless CreateGoOnlineAction implementation.  Synonym for calling CreateGoOnlineAction(false).
        /// </summary>
		public virtual IBasicAction CreateGoOnlineAction()
		{
			return (IBasicAction) CreateGoOnlineAction(false);
		}

        /// <summary>
        /// Provide the default CreateGoOnlineAction(bool andInitialize) implementation.  
        /// </summary>
        /// <param name="andInitialize">caller passes in true if this GoOnline action should also initialize the part.</param>
		public virtual IBoolParamAction CreateGoOnlineAction(bool andInitialize)
		{
			StartPartIfNeeded();

			ActionMethodDelegateActionArgStrResult<bool, NullObj> method  = OuterPerformGoOnlineAction;
            IBoolParamAction action = new BoolActionImpl(actionQ, andInitialize, method, "GoOnline", ActionLoggingReference);
			return action;
		}

        /// <summary>
        /// Provide the default CreateGoOfflineAction implementation.
        /// </summary>
		public virtual IBasicAction CreateGoOfflineAction()
		{
			ActionMethodDelegateActionArgStrResult<NullObj, NullObj> method  = OuterPerformGoOfflineAction;
			IBasicAction action = new BasicActionImpl(actionQ, method, "GoOffline", ActionLoggingReference);
			return action;
		}

        /// <summary>
        /// Method creates an unconfigured Service Action.  Caller is expected to update the Param with the name of the desired service before running.
        /// </summary>
		public virtual IStringParamAction CreateServiceAction() { return CreateServiceAction(string.Empty); }

        /// <summary>
        /// Method creates a Service Action with the Param preconfigured with the given <paramref name="serviceName"/> value.
        /// </summary>
        public virtual IStringParamAction CreateServiceAction(string serviceName) 
		{
            IStringParamAction action = new StringActionImpl(actionQ, serviceName, PerformServiceAction, "Service", ActionLoggingReference) as IStringParamAction;
			return action;
		}

        /// <summary>
        /// Method creates a Service Action using the <paramref name="serviceName"/> and <paramref name="namedParamValues"/> as the param value and NamedParamValues.
        /// </summary>
        public virtual IStringParamAction CreateServiceAction(string serviceName, INamedValueSet namedParamValues)
        {
            IStringParamAction action = new StringActionImpl(actionQ, serviceName, PerformServiceAction, "Service", ActionLoggingReference) as IStringParamAction;
            action.NamedParamValues = namedParamValues;

            return action;
        }

        // provide default CreateServiceAction method that creates one that will fail
        //	when it is run.  Sub-class may override the given DoRunServiceAction method
        //	to implement the ability to run services

		#endregion

		//-----------------------------------------------------------------
		#region INotifyable Members

        /// <summary>Implementation method for the INotifyable interface.  Requests that the Part's thread wakeup if it is waiting on the threadWakeupNotifier.</summary>
		public virtual void Notify()
		{
			threadWakeupNotifier.Notify();
		}

		#endregion

		//-----------------------------------------------------------------
		#region action related methods which may be re-implemented by sub-class

        /// <summary>
        /// This is the outer method that the corresponding action delegate points to.
        /// </summary>
        private string OuterPerformGoOnlineAction(IProviderActionBase<bool, NullObj> action)
        {
            string description = action.ToString(ToStringSelect.MesgAndDetail);
            bool andInitialize = action.ParamValue;
            bool setBaseUseState = settings.CheckFlag(GoOnlineAndGoOfflineHandling.GoOnlineUpdatesBaseUseState);

            IBaseState entryBaseState = BaseState;

            if (setBaseUseState)
            {
                SetBaseState(UseState.AttemptOnline, "{0} Started".CheckedFormat(description), true);
            }

            string result;

            try
            {
                result = PerformGoOnlineAction(action);
            }
            catch (System.Exception ex)
            {
                if (setBaseUseState && BaseState.UseState == UseState.AttemptOnline && settings.CheckFlag(GoOnlineAndGoOfflineHandling.GoOnlineFailureSetsUseStateToAttemptOnlineFailed))
                {
                    result = "Derived class PerformGoOnlineAction method threw exception: {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                    SetBaseState(UseState.AttemptOnlineFailed, "{0} failed: {1}".CheckedFormat(description, result));

                    return result;
                }
                else
                {
                    Log.Trace.Emit("{0}: Derived class PerformGoOnlineAction method threw exception: {1} [rethrowing]", description, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));

                    throw;
                }
            }

            if (setBaseUseState)
            {
                if (BaseState.UseState != UseState.AttemptOnline && settings.CheckFlag(GoOnlineAndGoOfflineHandling.AcceptCustomChangeFromAttemptOnlineState))
                {
                    Log.Trace.Emit("{0}: PerformGoOnlineAction explicitly changed the UseState to '{1}' [will not overwrite here]", description, BaseState.UseState);
                }
                else if (result == string.Empty || action.ActionState.Succeeded)
                {
                    if (andInitialize || entryBaseState.UseState == UseState.Online || entryBaseState.UseState == UseState.OnlineBusy || !settings.CheckFlag(GoOnlineAndGoOfflineHandling.UseOnlineUnititializedState))
                        SetBaseState(UseState.Online, "{0} Completed".CheckedFormat(description), true);
                    else
                        SetBaseState(UseState.OnlineUninitialized, "{0} Completed (starting BaseState was {1})".CheckedFormat(description, entryBaseState.ToString(Part.BaseState.ToStringSelect.UseStateNoPrefix | Part.BaseState.ToStringSelect.ConnState)), true);
                }
                else 
                {
                    UseState nextUseState = settings.CheckFlag(GoOnlineAndGoOfflineHandling.GoOnlineFailureSetsUseStateToAttemptOnlineFailed) ? UseState.AttemptOnlineFailed : UseState.Online;

                    if (result != null)
                        SetBaseState(nextUseState, "{0} Failed: {1}".CheckedFormat(description, result), true);
                    else if (action.ActionState.Failed)
                        SetBaseState(nextUseState, "{0} Failed: {1}".CheckedFormat(description, action.ActionState.ResultCode), true);
                }
            }

            return result;
        }

        /// <summary>Provide overridable virtual passthrough PerformGoOnlingAction implementation.  Invokes PerformGoOnlineActionEx(action, action.Param, action.NamedParamValues)</summary>
		protected virtual string PerformGoOnlineAction(IProviderActionBase<bool, NullObj> action)
		{
			return PerformGoOnlineActionEx(action, action.ParamValue, action.NamedParamValues);
		}

        /// <summary>
        /// Provide overridable virtual passthrough PerformGoOnlingAction implementation.  Invokes PerformGoOnlineAction(andInitialize)
        /// <para/>Note: alternate method name is choosen so that new signature will not generally overlap with derived part types that already have this signature pattern present
        /// </summary>
        protected virtual string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
        {
            return PerformGoOnlineAction(andInitialize);
        }

        /// <summary>Stub method provides overridable virtual default GoOnlineAction.  Fails if BasePerformMethodsSucceed flag is not set in GoOnlineAndGoOfflineHandling property value.</summary>
		protected virtual string PerformGoOnlineAction(bool andInitialize)
		{
            if (settings.CheckFlag(GoOnlineAndGoOfflineHandling.BasePerformMethodsSucceed))
            {
                return string.Empty;
            }
            else
            {
                // NOTE: this method is not intended to be called by derived classes as it allways failes
                if (andInitialize)
                    return "Action:GoOnlineAndInitialize not implemented";
                else
                    return "Action:GoOnline not implemented";
            }
		}

        private string OuterPerformGoOfflineAction(IProviderActionBase<NullObj, NullObj> action)
        {
            string description = action.ToString(ToStringSelect.MesgAndDetail);

            if (settings.CheckFlag(GoOnlineAndGoOfflineHandling.GoOfflineUpdatesBaseUseState))
                SetBaseState(UseState.Offline, "{0} Started".CheckedFormat(description), true);

            string result = PerformGoOfflineAction(action);

            return result;
        }

        /// <summary>Provide overridable virtual passthrough PerformGoOfflineAction implementation.  Invokes PerformGoOfflineAction();</summary>
		protected virtual string PerformGoOfflineAction(IProviderActionBase action)
		{
			return PerformGoOfflineAction();
		}

        /// <summary>Stub method provides overridable virtual default GoOfflineAction.  Fails if BasePerformMethodsSucceed flag is not set in GoOnlineAndGoOfflineHandling property value.</summary>
        protected virtual string PerformGoOfflineAction()
		{
            if (settings.CheckFlag(Part.GoOnlineAndGoOfflineHandling.BasePerformMethodsSucceed))
            {
                return string.Empty;
            }
            else
            {
                return "Action:GoOffline not implemented";
            }
		}

        /// <summary>Provide overridable virtual passthrough PerformServiceAction implementation.  Invokes PerformServiceActionEx(action, action.ParamValue, action.NamedParamValues).</summary>
		protected virtual string PerformServiceAction(IProviderActionBase<string, NullObj> action)
		{
			if (action == null)
				return "SimpleActivePartBase::PerformServiceAction: given empty action pointer";

            return PerformServiceActionEx(action, action.ParamValue, action.NamedParamValues);
		}

        /// <summary>
        /// Provide overridable virtual passthrough PerformServiceAction implementation.  Invokes PerformServiceAction(serviceName).  
        /// <para/>Note: alternate method name is choosen so that new signature will not generally overlap with derived part types that already have this signature pattern present
        /// </summary>
        protected virtual string PerformServiceActionEx(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            if (ipf == null)
                return "SimpleActivePartBase::PerformServiceAction: given empty action pointer";

            return PerformServiceAction(serviceName);
        }

        /// <summary>Stub method provides overridable virtual default ServiceAction.  Always fails.</summary>
		/// <returns>"Action:Service(serviceName): there is no implementation for this requested service action."</returns>
		protected virtual string PerformServiceAction(string serviceName)
		{
			return "Action:Service({0}): there is no implementation for this requested service action.".CheckedFormat(serviceName);
		}

		#endregion

		//-----------------------------------------------------------------
		#region object service thread related methods

        /// <summary>
        /// Default main loop method for this part.  invoked PerformMainLoopService, IssueNextQueueAction and WaitForSomethingToDo in a loop until actionQ has been disabled.
        /// </summary>
		protected virtual void MainThreadFcn()
		{
			using (Logging.EnterExitTrace entryExitTrace = new Logging.EnterExitTrace(Log, "MainThreadFcn", Logging.MesgType.Debug))
            {
                System.Exception exceptionDetected = null;

                try
                {
                    LogThreadInfo(Log.Debug);

                    if (settings.CheckFlag(SimpleActivePartBehaviorOptions.MainThreadStartSetsStateToOffline))
                        SetBaseState(UseState.Offline, "Part has been started");

                    // The following is the part's main loop:
                    //	Loop until the action queue has been disabled

                    mainThreadStartingActionList.Where(action => action != null).DoForEach(action => action());

                    while (actionQ.QueueEnable)
                    {
                        if (settings.ThreadWakeupNotifierResetHandling == ThreadWakeupNotifierResetHandling.ExplicitlyResetNotifierAtStartOfEachLoop)
                        {
                            // Reset the thread notification signal state so that we will only signal
                            //	if a new notification has occurred since we started this loop.  We do not want to prevent the loop from sleeping because of somthing that
                            //	we did before the loop started

                            threadWakeupNotifier.Reset();
                        }

                        PerformMainLoopService();

                        int didActionCount = 0;
                        do
                        {
                            if (IssueNextQueuedAction())
                                didActionCount++;
                            else
                                break;
                        } while (didActionCount < settings.MaxActionsToInvokePerServiceLoop);

                        if (didActionCount == 0)
                        {
                            // no action was performed - call WaitForSomethingToDo to slow the background spin loop rate to the rate determined by the WaitTimeLimit property.
                            WaitForSomethingToDo();
                        }
                        else if (settings.ThreadWakeupNotifierResetHandling == ThreadWakeupNotifierResetHandling.AutoResetIsUsedToResetNotifier)
                        {
                            WaitForSomethingToDo(TimeSpan.Zero);
                        }

                        if (!settings.CheckFlag(SimpleActivePartBehaviorOptions.PerformMainLoopServiceCallsServiceBusyConditionChangeDetection))
                            ServiceBusyConditionChangeDetection();
                    }
                }
                catch (System.Exception ex)
                {
                    exceptionDetected = ex;

                    // disable the queue so that clients will not block indefinitely after this part's main thread has stopped processing actions.
                    actionQ.QueueEnable = false;

                    Log.Debug.Emit("{0} failed with unexpected {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.Full));

                    var mesg = "Caught unexpected {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage));

                    entryExitTrace.ExtraMessage = mesg;
                }

                try
                {
                    mainThreadStoppingActionList.Where(action => action != null).DoForEach(action => action());
                }
                catch (System.Exception ex)
                {
                    if (exceptionDetected == null)
                        exceptionDetected = ex;

                    Log.Debug.Emit("{0} failed during execution of stopping action list: {1}", Fcns.CurrentMethodName, ex.ToString(ExceptionFormat.Full));

                    if (entryExitTrace.ExtraMessage.IsNullOrEmpty())
                        entryExitTrace.ExtraMessage = "Caught unexpected {0} during stopping action list".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage));
                }

                if (exceptionDetected == null)
                {
                    string mesg = "Normal exit";

                    if (PrivateBaseState.IsOnlineOrAttemptOnline && settings.CheckFlag(SimpleActivePartBehaviorOptions.MainThreadStopSetsStateToStoppedIfIsOnlineOrAttemptOnline))
                        SetBaseState(UseState.Stopped, (mesg = "Part stopped by request [from {0}]".CheckedFormat(PrivateBaseState.ToString(Part.BaseState.ToStringSelect.UseStateNoPrefix | Part.BaseState.ToStringSelect.ConnState))));
                    else if (PrivateBaseState.UseState == UseState.Offline && settings.CheckFlag(SimpleActivePartBehaviorOptions.MainThreadStopSetsStateToStoppedIfOffline))
                        SetBaseState(UseState.Stopped, (mesg = "Part stopped by request [from {0}]".CheckedFormat(PrivateBaseState.ToString(Part.BaseState.ToStringSelect.UseStateNoPrefix | Part.BaseState.ToStringSelect.ConnState))));

                    if (entryExitTrace.ExtraMessage.IsNullOrEmpty())
                        entryExitTrace.ExtraMessage = mesg;
                }
                else
                {
                    string mesg = "Caught unexpected {0}".CheckedFormat(exceptionDetected.ToString(ExceptionFormat.TypeAndMessage));

                    if (settings.CheckFlag(SimpleActivePartBehaviorOptions.UseMainThreadFailedState))
                        SetBaseState(UseState.MainThreadFailed, mesg);

                    if (entryExitTrace.ExtraMessage.IsNullOrEmpty())
                        entryExitTrace.ExtraMessage = mesg;
                }
            }
		}

        private List<System.Action> mainThreadStartingActionList = new List<System.Action>();
        private List<System.Action> mainThreadStoppingActionList = new List<System.Action>();

        /// <summary>
        /// Adds the given <paramref name="action"/> to the list of actions that will be performed by the MainThreadFcn just before it enters its main spin loop.
        /// Caller can use the optional <paramref name="addSelect"/> parameter to determine if the action is to be Appended to (default), or Prefixed to the list.
        /// </summary>
        protected void AddMainThreadStartingAction(System.Action action, ActionListAddSelection addSelect = ActionListAddSelection.Append)
        {
            if (addSelect == ActionListAddSelection.Prefix)
                mainThreadStartingActionList.Insert(0, action);
            else
                mainThreadStartingActionList.Add(action);
        }

        /// <summary>
        /// Adds the given <paramref name="action"/> to the list of actions that will be performed by the MainThreadFcn just after it leaves its main spin loop.
        /// Caller can use the optional <paramref name="addSelect"/> parameter to determine if the action is to be Appended to, or Prefixed to  (default) the list.
        /// </summary>
        protected void AddMainThreadStoppingAction(System.Action action, ActionListAddSelection addSelect = ActionListAddSelection.Prefix)
        {
            if (addSelect == ActionListAddSelection.Prefix)
                mainThreadStoppingActionList.Insert(0, action);
            else
                mainThreadStoppingActionList.Add(action);
        }

        /// <summary>
        /// Enumeration defines the list position that MainThreadActionList Actions can be added to the Stopping and Starting list.
        /// <para/>Append (0 - default), Prefix
        /// </summary>
        protected enum ActionListAddSelection
        {
            /// <summary>Selects that the action is appended to the list</summary>
            Append = 0,

            /// <summary>Selects that the action is added to the front of the list</summary>
            Prefix,
        }

        /// <summary>
        /// Generates and emits a log message including the current Managed Thread's name and id as well as the current Win32 ThreadID on which this managed thread is running.
        /// </summary>
        protected void LogThreadInfo(Logging.IMesgEmitter emitter)
        {
            System.Threading.Thread currentThread = System.Threading.Thread.CurrentThread;
            emitter.Emit("ThreadInfo: Name:'{0}', Managed ThreadID:{1:d4}, Win32 ThreadID:${2:x4}", (currentThread.Name ?? String.Empty), currentThread.ManagedThreadId, Utils.Win32.GetCurrentThreadId());
        }

        /// <summary>
        /// Returns true once part stop has been requested (gets set after PreStopPart completes and actionQ has been disabled)
        /// </summary>
        protected bool HasStopBeenRequested { get { return (hasStopBeenRequested || (actionQ != null && !actionQ.QueueEnable)); } }

        /// <summary>
        /// Base class method that can be overriden by derived classes to perform periodic service under control of the Part's main loop.
        /// <para/>If the SimpleActivePartBehaviorOptions.PerformMainLoopServiceCallsServiceBusyConditionChangeDetection behavior is selected then this method calls ServiceBusyConditionChangeDetection();
        /// </summary>
		protected virtual void PerformMainLoopService() 
		{
            if (settings.CheckFlag(SimpleActivePartBehaviorOptions.PerformMainLoopServiceCallsServiceBusyConditionChangeDetection))
                ServiceBusyConditionChangeDetection();
		}

        /// <summary>
        /// A derived class should override this method so that it can be used to service each of the ActionQueue instance for cancel requests when they are not empty
        /// </summary>
        protected override void ServiceActionQueueCancelRequests()
        {
            ActionQueue.ServiceCancelRequests();

            base.ServiceActionQueueCancelRequests();
        }

        /// <summary>
        /// Attempts to get and perform the next action from the actionQ by calling PerformAction on it.
        /// <para/>True if an action was performed or false otherwise.
        /// </summary>
        /// <remarks>
        /// This method does not need to put a busyFlag around pulling the next action from the queue since the the concept of being busy
        /// is only visible outside of the part when publishing the BaseState.  As such the busyFlag is only used in the PerformAction method
        /// that this method calls.
        /// </remarks>
		protected virtual bool IssueNextQueuedAction()
		{
            IProviderFacet action = actionQ.GetNextAction();

		    if (action == null)
			    return false;

            PerformAction(action);

			return true;
		}

        /// <summary>
        /// Sets the CurrentAction to the given action value, asks the given action to IssueAndInvokeAction and then sets CurrentAction to null.
        /// </summary>
		protected virtual void PerformAction(IProviderFacet action)
		{
            string actionName = (action != null) ? action.ToString(ToStringSelect.MesgAndDetail) : null;

            IActionInfo actionInfo = null;

            if (settings.CheckFlag(SimpleActivePartBehaviorOptions.PerformActionPublishesActionInfo) && action != null)
                actionInfo = new ActionInfo(action); ;

            IDisposable busyFlag = null;
            try
            {
                CurrentAction = action;

                if (actionInfo != null)
                    PublishActionInfo(actionInfo);

                if (settings.AutomaticallyIncAndDecBusyCountAroundActionInvoke)
                    busyFlag = CreateInternalBusyFlagHolderObject("Issuing next queued action", actionName);

                if (action != null)
                    action.IssueAndInvokeAction();
            }
            finally
            {
                if (actionInfo != null)
                    PublishActionInfo(new ActionInfo(actionInfo, action.ActionState));

                Fcns.DisposeOfObject(ref busyFlag);

                CurrentAction = null;
            }
		}

        private void SetupActionInfoPublisherIVAsIfNeeded()
        {
            if (actionInfoIVA == null)
            {
                ivi = base.settings.PartBaseIVI ?? Values.Instance;

                actionInfoIVA = ivi.GetValueAccessor<IActionInfo>("{0}.ActionInfo".CheckedFormat(PartID)).Set(EmptyActionInfo);
                lastActionInfoIVA = ivi.GetValueAccessor<IActionInfo>("{0}.LastActionInfo".CheckedFormat(PartID)).Set(EmptyActionInfo);
                actionInfoIVAArray = new IValueAccessor[] { actionInfoIVA, lastActionInfoIVA };
                actionInfoIVAArrayLength = actionInfoIVAArray.Length;
            }
        }

        protected void PublishActionInfo(IActionInfo actionInfo)
        {
            SetupActionInfoPublisherIVAsIfNeeded();

            if (!actionInfo.ActionState.IsComplete)
            {
                actionInfoIVA.Set(actionInfo);
            }
            else
            {
                lastActionInfoIVA.Value = actionInfoIVA.Value = actionInfo;
                ivi.Set(actionInfoIVAArray, numEntriesToSet: actionInfoIVAArrayLength, optimize: true);
            }
        }

        private IValueAccessor<IActionInfo> actionInfoIVA, lastActionInfoIVA;
        private IValueAccessor[] actionInfoIVAArray;
        private int actionInfoIVAArrayLength;
        private IValuesInterconnection ivi;

        /// <summary>Returns the IProviderFacet for the current action that the Part's main service loop is currently in IssueAndInvokeAction on, or null if none.</summary>
        protected IProviderFacet CurrentAction { get; private set; }

        /// <summary>Returns the string description of the current action (with details, without state), or the empty string if the CurrentAction is null</summary>
        protected string CurrentActionDescription 
        { 
            get { return ((CurrentAction != null) ? CurrentAction.ToString(ToStringSelect.MesgAndDetail) : string.Empty); } 
        }

        /// <summary>Returns the IActionState for the current action that the Part's main service loop is currently in IssueAndInvokeAction on, or an Initial/Empty IActionState if there is no such action.</summary>
        protected IActionState CurrentActionState { get { return ((CurrentAction != null) ? CurrentAction.ActionState : EmptyActionState); } }

        /// <summary>Contains the ActionState that is used for the CurrentActionState when there is no CurrentAction.</summary>
        protected readonly IActionState EmptyActionState = new ActionStateCopy();

        /// <summary>Contains an IActionInfo that is empty.</summary>
        protected readonly IActionInfo EmptyActionInfo = new ActionInfo();

        /// <summary>Requests to cancel the CurrentAction if it is non null and its state IsPendingCompletion</summary>
        protected void RequestCancelCurrentAction()
        {
            if (CurrentActionState.IsPendingCompletion)
                CurrentAction.RequestCancel();
        }

        /// <summary>
        /// Waits for threadWakeupNotifier to be signaled or default waitTimeLimit to elapse (used to set Parts's default spin rate).
        /// <para/>True if the object was signaled or false if the timeout caused flow to return to the caller.
        /// </summary>
		protected bool WaitForSomethingToDo()
		{
			return WaitForSomethingToDo(threadWakeupNotifier, settings.WaitTimeLimit);
		}

        /// <summary>
        /// Waits for threadWakeupNotifier to be signaled or given waitTimeLimit to elapse.
        /// <para/>True if the object was signaled or false if the timeout caused flow to return to the caller.
        /// </summary>
        protected bool WaitForSomethingToDo(TimeSpan useWaitTimeLimit)
		{
			return WaitForSomethingToDo(threadWakeupNotifier, useWaitTimeLimit);
		}

        /// <summary>
        /// Most generic version of WaitForSomethingToDo.  caller provides IWaitable object and waitTimeLimit.
        /// <para/>True if the object was signaled or false if the timeout caused flow to return to the caller.
        /// <para/>This version is virtual so it can be overriden in drived classes.
        /// </summary>
        protected virtual bool WaitForSomethingToDo(Utils.IWaitable waitable, TimeSpan useWaitTimeLimit)
		{
            bool signaled = waitable.Wait(useWaitTimeLimit);

            return signaled;
		}

		#endregion

		//-----------------------------------------------------------------
		#region private and protected fields and related properties

        /// <summary>Protected readonly WaitEventNotifier used by the Part's main thread as part of the WaitForSomethingToDo pattern.</summary>
        protected readonly WaitEventNotifier threadWakeupNotifier = new WaitEventNotifier(WaitEventNotifier.Behavior.WakeOne);

        private System.Threading.Thread mainThread = null;
        private System.Threading.Thread joinedThread = null;

        /// <summary>Protected field used to define the default ActionLogging instance that is cloned when creating new actions.</summary>
        private ActionLogging actionLoggingReference = null;

        /// <summary>
        /// Protected get only access to the property that is used by the Part to reference its underlying default ActionQueue instance.  
        /// </summary>
        protected ActionQueue ActionQueue { get { return actionQ; } }

        /// <summary>
        /// Protected get only access to the property that is used by the Part to reference its underlying default ActionQueue instance.  
        /// This property is using the symbol name of a prior field to permit get-only access in derived classes.
        /// </summary>
        protected ActionQueue actionQ { get; private set; }

        /// <summary>Returns true if all ActionQueues are empty.</summary>
        protected override bool AreAllActionQueuesEmpty { get { return ActionQueue.IsEmpty; } }

        /// <summary>Returns true if the part has a queue and if that queue is enabled.</summary>
        public virtual bool AreAnyActionQueueEnabled { get { return (actionQ != null && actionQ.QueueEnable); } }

        /// <summary>ActionLogging object that is used as the reference for commands created by SimpleActivePartBase and by many derived Part objects.</summary>
        protected ActionLogging ActionLoggingReference { get { return actionLoggingReference; } }

		#endregion

		//-----------------------------------------------------------------
	}

	#endregion

    #region IClientFacet ExtentionMethods (StartPartInline, StopPartInline, RunGoOnlineActionInline, RunGoOnlineAction, RunGoOfflineActionInline, RunGoOfflineAction, RunServiceActionInline, RunServiceAction)

    /// <summary>Standard extension methods wrapper class/namespace</summary>
    public static partial class ExtentionMethods
    {
        /// <summary>Calls StartPart on the given part.  Returns the given part to support call chaining.</summary>
        public static TPartType StartPartInline<TPartType>(this TPartType part) where TPartType : IActivePartBase
        {
            part.StartPart();

            return part;
        }

        /// <summary>Calls StopPart on the given part.  Returns the given part to support call chaining.</summary>
        public static TPartType StopPartInline<TPartType>(this TPartType part) where TPartType : IActivePartBase
        {
            part.StopPart();

            return part;
        }

        /// <summary>Creates and runs a GoOnline(<paramref name="andInitialize"/>) action on the given part.  Returns the given part to support call chaining.</summary>
        public static TPartType RunGoOnlineActionInline<TPartType>(this TPartType part, bool andInitialize = true, INamedValueSet namedParamValues = null) where TPartType : IActivePartBase
        {
            string ec = part.RunGoOnlineAction(andInitialize, namedParamValues: namedParamValues);

            return part;
        }

        /// <summary>Creates and runs a GoOnline(<paramref name="andInitialize"/>) action on the given part and returns the resulting result code.</summary>
        public static string RunGoOnlineAction<TPartType>(this TPartType part, bool andInitialize = true, INamedValueSet namedParamValues = null) where TPartType : IActivePartBase
        {
            string ec = part.CreateGoOnlineAction(andInitialize).SetNamedParamValues(namedParamValues, ifNotNull: true).Run();

            return ec;
        }

        /// <summary>Creates and runs a GoOffline action on the given part.  Returns the given part to support call chaining.</summary>
        public static TPartType RunGoOfflineActionInline<TPartType>(this TPartType part, INamedValueSet namedParamValues = null) where TPartType : IActivePartBase
        {
            part.CreateGoOfflineAction().SetNamedParamValues(namedParamValues, ifNotNull: true).RunInline();

            return part;
        }

        /// <summary>Creates and runs a GoOffline action on the given part and returns the resulting result code.</summary>
        public static string RunGoOfflineAction<TPartType>(this TPartType part, INamedValueSet namedParamValues = null) where TPartType : IActivePartBase
        {
            string ec = part.CreateGoOfflineAction().SetNamedParamValues(namedParamValues, ifNotNull: true).Run();

            return ec;
        }

        /// <summary>Creates and runs a Service action on the given part.  Returns the given part to support call chaining.</summary>
        public static TPartType RunServiceActionInline<TPartType>(this TPartType part, string serviceName, INamedValueSet namedParamValues = null) where TPartType : IActivePartBase
        {
            part.RunServiceAction(serviceName, namedParamValues);

            return part;
        }

        /// <summary>Creates and runs a Service action on the given part and returns the resulting result code.</summary>
        public static string RunServiceAction<TPartType>(this TPartType part, string serviceName, INamedValueSet namedParamValues = null) where TPartType : IActivePartBase
        {
            string ec = part.CreateServiceAction(serviceName).SetNamedParamValues(namedParamValues, ifNotNull: true).Run();

            return ec;
        }
    }

    #endregion
    
    //-----------------------------------------------------------------
}
