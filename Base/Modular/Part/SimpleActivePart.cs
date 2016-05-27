//-------------------------------------------------------------------
/*! @file SimpleActivePart.cs
 * @brief This file contains the templatized definition of a base class that can be used to build many types of active parts.  This functionality is part of the Modular.Part namespace in this library.
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
    using MosaicLib.Modular.Common;

	//-----------------------------------------------------------------
	#region SimpleActivePartBase

    /// <summary>
    /// Defines the logic that a SimpleActivePart uses for clearing the threadWakeupNotifier object that is used in WaitForSomethingToDo calls.
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
    /// </summary>
    [Flags]
    public enum GoOnlineAndGoOfflineHandling : int
    {    
        /// <summary>
        /// Selects basic (default) handling.  Base UseState is not automatically updated and base Peform methods return not implemented result code.
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
        /// Selects that exeuction of the GoOffline action sets Base UseState to Offline before calling the normal PerformGoOffline method.
        /// </summary>
        GoOfflineUpdatesBaseUseState = 4,
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
	/// INotifyable interface allows class instance to become the receiver for a BasicNotification event.  This behavior sets the mThreadWakeupNotifier so that
	/// the thread will wakeup whenever the object is signaled/Notified.
	/// </remarks>
	public abstract class SimpleActivePartBase : SimplePartBase, IActivePartBase, INotifyable
	{
		//-----------------------------------------------------------------
		#region sub classes

        /// <summary>Normal Implementation object for IBaseAction type Actions.  Derives from ActionImplBase{NullObj, NullObj}.</summary>
		public class BasicActionImpl : ActionImplBase<NullObj, NullObj>, IBasicAction
		{
            /// <summary>Constructor for use with simple action method delegate: ActionMethodDelegateStrResult.</summary>
			public BasicActionImpl(ActionQueue actionQ, ActionMethodDelegateStrResult method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, method, new ActionLogging(mesg, loggingReference)) 
            {}
            /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{NullObj, NullObj}.</summary>
            public BasicActionImpl(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, null, false, method, new ActionLogging(mesg, loggingReference)) 
            { }
            /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{NullObj, NullObj}.</summary>
            public BasicActionImpl(ActionQueue actionQ, FullActionMethodDelegate<NullObj, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, null, false, method, new ActionLogging(mesg, loggingReference)) 
            { }
		}

        /// <summary>Implementation object for IClientFacetWithParam{ParamType} type Actions.  Derives from ActionImplBase{ParamType, NullObj}.</summary>
        public class ParamActionImplBase<ParamType> : ActionImplBase<ParamType, NullObj>, IBasicAction, IClientFacetWithParam<ParamType>
		{
            /// <summary>Constructor for use with more complete action method delegate: ActionMethodDelegateActionArgStrResult{ParamType, NullObj}.</summary>
            public ParamActionImplBase(ActionQueue actionQ, ParamType paramValue, ActionMethodDelegateActionArgStrResult<ParamType, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, false, method, new ActionLogging(mesg, "{0}".CheckedFormat(paramValue), loggingReference)) 
            { }
            /// <summary>Constructor for use with full action method delegate: FullActionMethodDelegate{ParamType, NullObj}.</summary>
            public ParamActionImplBase(ActionQueue actionQ, ParamType paramValue, FullActionMethodDelegate<ParamType, NullObj> method, string mesg, ActionLogging loggingReference) 
                : base(actionQ, paramValue, false, method, new ActionLogging(mesg, "{0}".CheckedFormat(paramValue), loggingReference)) 
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
        /// PartType will be automatically derived from the class name of the derived type
        /// <para/>PartID: use given value, PartType: use declaring class name (by reflection), WaitTimeLimit: 0.1 sec, enableQueue:true, queueSize:10
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        public SimpleActivePartBase(string partID) 
            : this(partID, new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(), TimeSpan.FromSeconds(0.1), true, 10) 
        {}

        /// <summary>
        /// Constructor variant: caller provides PartID and nominal Service loop WaitTimeLimit
        /// PartType will be automatically derived from the class name of the derived type
        /// <para/>PartID: use given value, PartType: use declaring class name (by reflection), WaitTimeLimit: use given value, enableQueue:true, queueSize:10
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="waitTimeLimit">Defines the nominal maximum period that the part's outer main thread loop will wait for the next notify occurrance.  Sets the default "spin" rate for the part.</param>
        public SimpleActivePartBase(string partID, TimeSpan waitTimeLimit) 
            : this(partID, new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(), waitTimeLimit, true, 10) 
        {}

        /// <summary>
        /// Constructor variant: caller provides PartID, nominal Service loop WaitTimeLimit, initial queue enable, and maximum queue size.
        /// PartType will be automatically derived from the class name of the derived type
        /// <para/>PartID: use given value, PartType: use declaring class name (by reflection), WaitTimeLimit: use given value, enableQueue:use given value, queueSize:use given value
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="waitTimeLimit">Defines the nominal maximum period that the part's outer main thread loop will wait for the next notify occurrance.  Sets the default "spin" rate for the part.</param>
        /// <param name="enableQueue">Set to true for the parts ActionQueue to be enabled even before the part has been started.  Set to false to prevent actions from being started until this part has been started and has enabled its queue.</param>
        /// <param name="queueSize">Defines the maximum number of pending actions that may be placed in the queue before further attempts to start new actions will be blocked and will fail.</param>
        public SimpleActivePartBase(string partID, TimeSpan waitTimeLimit, bool enableQueue, int queueSize)
            : this(partID, new System.Diagnostics.StackFrame(1).GetMethod().DeclaringType.ToString(), waitTimeLimit, enableQueue, queueSize)
        {}

        /// <summary>
        /// Constructor variant: caller provides PartID and PartType
        /// <para/>PartID: use given value, PartType: use given value, WaitTimeLimit: 0.1 sec, enableQueue:true, queueSize:10
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        public SimpleActivePartBase(string partID, string partType) 
            : this(partID, partType, TimeSpan.FromSeconds(0.1), true, 10) 
        {}

        /// <summary>
        /// Constructor variant: caller provides PartID, PartType and nominal Service loop WaitTimeLimit
        /// <para/>PartID: use given value, PartType: use given value, WaitTimeLimit: use given value, enableQueue:true, queueSize:10
        /// </summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        /// <param name="waitTimeLimit">Defines the nominal maximum period that the part's outer main thread loop will wait for the next notify occurrance.  Sets the default "spin" rate for the part.</param>
        public SimpleActivePartBase(string partID, string partType, TimeSpan waitTimeLimit) 
            : this(partID, partType, waitTimeLimit, true, 10) 
        {}

        /// <summary>Constructor variant: caller provides PartID, PartType, nominal Service loop WaitTimeLimit, initial queue enable, and maximum queue size.</summary>
        /// <param name="partID">Gives the PartID/Name that the part will report and use.</param>
        /// <param name="partType">Gives the PartType that this part will report</param>
        /// <param name="waitTimeLimit">Defines the nominal maximum period that the part's outer main thread loop will wait for the next notify occurrance.  Sets the default "spin" rate for the part.</param>
        /// <param name="enableQueue">Set to true for the parts ActionQueue to be enabled even before the part has been started.  Set to false to prevent actions from being started until this part has been started and has enabled its queue.</param>
        /// <param name="queueSize">Defines the maximum number of pending actions that may be placed in the queue before further attempts to start new actions will be blocked and will fail.</param>
        public SimpleActivePartBase(string partID, string partType, TimeSpan waitTimeLimit, bool enableQueue, int queueSize)
            : base(partID, partType)
		{
            TreatPartAsBusyWhenInternalPartBusyCountIsNonZero = true;       // allow derived objects to default to be able to use CreateInternalBusyFlagHolderObject

            this.waitTimeLimit = waitTimeLimit;

			actionQ = new ActionQueue(partID + ".q", enableQueue, queueSize);

            IBasicNotificationList notificiationList = actionQ.NotifyOnEnqueue;
            notificiationList.AddItem(threadWakeupNotifier);
            AddExplicitDisposeAction(() => 
                {
                    notificiationList.RemoveItem(threadWakeupNotifier);
                    threadWakeupNotifier.Release();
                });

			actionLoggingReference = new ActionLogging(Log, ActionLoggingConfig.Info_Error_Debug_Debug);

            GoOnlineAndGoOfflineHandling = (GoOnlineAndGoOfflineHandling.BasePerformMethodsSucceed | GoOnlineAndGoOfflineHandling.GoOnlineUpdatesBaseUseState | GoOnlineAndGoOfflineHandling.GoOfflineUpdatesBaseUseState);

            Interconnect.Parts.Parts.Instance.RegisterPart(this);
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
        /// When this property is set to true, the BaseState will automatically transition between Busy and Idle when Actions implementations are invoked and return.  
        /// When it is false the derived object will be entirely responsible for causing the component to transition between the Busy and Idle states.  This value
        /// defaults to True.
        /// </summary>
        public bool AutomaticallyIncAndDecBusyCountAroundActionInvoke { get; set; }

        /// <summary>Defines the logic that this SimpleActivePart is using for clearing the threadWakeupNotifier object that is used in WaitForSomethingToDo calls.</summary>
        public ThreadWakeupNotifierResetHandling ThreadWakeupNotifierResetHandling { get; set; }

        /// <summary>
        /// Defines the maximum number of Actions that can be invoked per iteration of the outer service loop (ie per call to PerformMainLoopService/WaitForSomethingToDo).
        /// <para/>Defaults to 1, setter will clamp the given value to be between 1 and 100.
        /// </summary>
        public int MaxActionsToInvokePerServiceLoop { get { return maxActionsToInvokePerServiceLoop; } set { maxActionsToInvokePerServiceLoop = Math.Max(1, Math.Min(100, value)); } }
        private int maxActionsToInvokePerServiceLoop = 1;

        /// <summary>
        /// Defines this parts base state handling for the GoOnline and GoOffline actions.
        /// </summary>
        protected GoOnlineAndGoOfflineHandling GoOnlineAndGoOfflineHandling { get; set; }

        /// <summary>
        /// Returns true GoOnlineAndGoOfflineHandling has the indicated flag value(s) set in its current value
        /// </summary>
        protected bool CheckFlag(GoOnlineAndGoOfflineHandling flag)
        {
            return ((GoOnlineAndGoOfflineHandling & flag) == flag);
        }

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
		private static object startIfNeededMutex = new object();

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

			ActionMethodDelegateActionArgStrResult<bool, NullObj> method  = PerformGoOnlineAction;
            IBoolParamAction action = new BoolActionImpl(actionQ, andInitialize, method, "GoOnline", ActionLoggingReference);
			return action;
		}

        /// <summary>
        /// Provide the default CreateGoOfflineAction implementation.
        /// </summary>
		public virtual IBasicAction CreateGoOfflineAction()
		{
			ActionMethodDelegateActionArgStrResult<NullObj, NullObj> method  = PerformGoOfflineAction;
			IBasicAction action = new BasicActionImpl(actionQ, method, "GoOffline", ActionLoggingReference);
			return action;
		}

        /// <summary>
        /// Method creates an unconfigured Service Action.  Caller is expected to update the Param with the name of the desired service before running.
        /// </summary>
		public virtual IStringParamAction CreateServiceAction() { return CreateServiceAction(string.Empty); }

        /// <summary>
        /// Method creates a Service Action with the Param preconfigured with the given value.
        /// </summary>
		public virtual IStringParamAction CreateServiceAction(string paramValue) 
		{
			IStringParamAction action = new StringActionImpl(actionQ, paramValue, PerformServiceAction, "Service", ActionLoggingReference) as IStringParamAction;
			return action;
		}

        /// <summary>
        /// Method creates a Service Action with the Param and NamedParamValues preconfigured with the given values.
        /// </summary>
        public virtual IStringParamAction CreateServiceAction(string paramValue, INamedValueSet namedParamValues)
        {
            IStringParamAction action = new StringActionImpl(actionQ, paramValue, PerformServiceAction, "Service", ActionLoggingReference) as IStringParamAction;
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
		public void Notify()
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
            bool setBaseUseState = CheckFlag(GoOnlineAndGoOfflineHandling.GoOnlineUpdatesBaseUseState);

            if (setBaseUseState)
            {
                SetBaseState(UseState.AttemptOnline, "{0} Started".CheckedFormat(description), true);
            }

            string result = PerformGoOnlineAction(action);

            if (setBaseUseState)
            {
                if (result == string.Empty || action.ActionState.Succeeded)
                    SetBaseState(UseState.Online, "{0} Completed".CheckedFormat(description), true);
                else if (result != null)
                    SetBaseState(UseState.Online, "{0} Failed: {1}".CheckedFormat(description, result), true);
                else if (action.ActionState.Failed)
                    SetBaseState(UseState.Online, "{0} Failed: {1}".CheckedFormat(description, action.ActionState.ResultCode), true);
            }

            return result;
        }

		/// <summary>Provide passthrough PerformGoOnlingAction implementation.  Invokes PerformGoOnlineAction(action.Param)</summary>
		protected virtual string PerformGoOnlineAction(IProviderActionBase<bool, NullObj> action)
		{
			return PerformGoOnlineAction(action.ParamValue);
		}

		/// <summary>Stub method provides default GoOnlineAction.  Fails if BasePerformMethodsSucceed flag is not set in GoOnlineAndGoOfflineHandling property value.</summary>
		protected virtual string PerformGoOnlineAction(bool andInitialize)
		{
            if (CheckFlag(Part.GoOnlineAndGoOfflineHandling.BasePerformMethodsSucceed))
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

        private string OuterPerformGoOfflineAction(IProviderActionBase<bool, NullObj> action)
        {
            string description = action.ToString(ToStringSelect.MesgAndDetail);

            if (CheckFlag(GoOnlineAndGoOfflineHandling.GoOfflineUpdatesBaseUseState))
                SetBaseState(UseState.Offline, "{0} Started".CheckedFormat(description), true);

            string result = PerformGoOfflineAction(action);

            return result;
        }

		/// <summary>Provide passthrough PerformGoOfflineAction implementation.  Invokes PerformGoOfflineAction();</summary>
		protected virtual string PerformGoOfflineAction(IProviderActionBase action)
		{
			return PerformGoOfflineAction();
		}

        /// <summary>Stub method provides default GoOfflineAction.  Fails if BasePerformMethodsSucceed flag is not set in GoOnlineAndGoOfflineHandling property value.</summary>
        protected virtual string PerformGoOfflineAction()
		{
            if (CheckFlag(Part.GoOnlineAndGoOfflineHandling.BasePerformMethodsSucceed))
            {
                return string.Empty;
            }
            else
            {
                return "Action:GoOffline not implemented";
            }
		}

		/// <summary>Provide passthrough PerformGoOfflineAction implementation.  Invokes PerformGoOfflineAction();</summary>
		protected virtual string PerformServiceAction(IProviderActionBase<string, NullObj> action)
		{
			if (action == null)
				return "SimpleActivePartBase::PerformServiceAction: given empty action pointer";

			return PerformServiceAction(action.ParamValue);
		}

		/// <summary>Stub method provides default ServiceAction.  Always fails.</summary>
		/// <returns>"Action:Service(serviceName): there is no implementation for this requested service action."</returns>
		protected virtual string PerformServiceAction(string serviceName)
		{
			return "Action:Service(" + serviceName + "): there is no implementation for this requested service action.";
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
                try
                {
                    LogThreadInfo(Log.Debug);

                    // The following is the part's main loop:
                    //	Loop until the action queue has been disabled

                    while (actionQ.QueueEnable)
                    {
                        if (ThreadWakeupNotifierResetHandling == ThreadWakeupNotifierResetHandling.ExplicitlyResetNotifierAtStartOfEachLoop)
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
                        } while (didActionCount < MaxActionsToInvokePerServiceLoop);

                        if (didActionCount == 0)
                        {
                            // no action was performed - call WaitForSomethingToDo to slow the background spin loop rate to the rate determined by the WaitTimeLimit property.
                            WaitForSomethingToDo();
                        }
                        else if (ThreadWakeupNotifierResetHandling == ThreadWakeupNotifierResetHandling.AutoResetIsUsedToResetNotifier)
                        {
                            WaitForSomethingToDo(TimeSpan.Zero);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // disable the queue so that clients will not block indefinitely after this part's main thread has stopped processing actions.
                    actionQ.QueueEnable = false;

                    string methodName = new System.Diagnostics.StackFrame().GetMethod().Name;
                    Log.Debug.Emit("{0} failed with unexpected excpetion: {1}", ex);

                    entryExitTrace.ExtraMessage = "Caught unexpected exception: {0} '{1}'".CheckedFormat(ex.GetType(), ex.Message);
                }

                if (entryExitTrace.ExtraMessage.IsNullOrEmpty())
                    entryExitTrace.ExtraMessage = "Normal exit";
			}
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
        /// empty virtual base class method that can be overriden by derived classes to perform periodic service under control of the Part's main loop.
        /// </summary>
		protected virtual void PerformMainLoopService() 
		{
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
            string actionName = (action != null) ? action.ToString() : null;

            IDisposable busyFlag = null;
            try
            {
                CurrentAction = action;

                if (AutomaticallyIncAndDecBusyCountAroundActionInvoke)
                    busyFlag = CreateInternalBusyFlagHolderObject("Issuing next queued action", actionName);

                if (action != null)
                    action.IssueAndInvokeAction();
            }
            finally
            {
                Fcns.DisposeOfObject(ref busyFlag);

                CurrentAction = null;
            }
		}

        /// <summary>Returns the IProviderFacet for the current action that the Part's main service loop is currently in IssueAndInvokeAction on, or null if none.</summary>
        protected IProviderFacet CurrentAction { get; private set; }

        /// <summary>Returns the IActionState for the current action that the Part's main service loop is currently in IssueAndInvokeAction on, or an Initial/Empty IActionState if there is no such action.</summary>
        protected IActionState CurrentActionState { get { return ((CurrentAction != null) ? CurrentAction.ActionState : EmptyActionState); } }

        /// <summary>Contains the ActionState that is used for the CurrentActionState when there is no CurrentAction.</summary>
        protected readonly IActionState EmptyActionState = new ActionStateCopy();

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
			return WaitForSomethingToDo(threadWakeupNotifier, waitTimeLimit);
		}

        /// <summary>
        /// Waits for threadWakeupNotifier to be signaled or given waitTimeLimit to elapse.
        /// <para/>True if the object was signaled or false if the timeout caused flow to return to the caller.
        /// </summary>
        protected bool WaitForSomethingToDo(TimeSpan waitTimeLimit)
		{
			return WaitForSomethingToDo(threadWakeupNotifier, waitTimeLimit);
		}

        /// <summary>
        /// Most generic version of WaitForSomethingToDo.  caller provides IWaitable object and waitTimeLimit.
        /// <para/>True if the object was signaled or false if the timeout caused flow to return to the caller.
        /// </summary>
        protected virtual bool WaitForSomethingToDo(Utils.IWaitable waitable, TimeSpan waitTimeLimit)
		{
            bool signaled = waitable.Wait(waitTimeLimit);

            return signaled;
		}

		#endregion

		//-----------------------------------------------------------------
		#region private and protected fields and related properties (includuing BaseState implementation)

        /// <summary>Protected get/set field contains the default WaitTimeLimit for the Part's thread.  Generally used when calling WaitForSomethingToDo().  Setter clamps value to be between minWaitTimeLimit (0.0) and maxWaitTimeLimit (0.5) seconds</summary>
        protected TimeSpan WaitTimeLimit { get { return innerWaitTimeLimitStore; } set { innerWaitTimeLimitStore = ((value >= minWaitTimeLimit) ? ((value <= maxWaitTimeLimit) ? value : maxWaitTimeLimit) : minWaitTimeLimit); } }

        /// <summary>Protected readonly field contains the default waitTimeLimit for the Part's thread.  Generally used when calling WaitForSomethingToDo().</summary>
        protected TimeSpan waitTimeLimit { get { return innerWaitTimeLimitStore; } private set { innerWaitTimeLimitStore = value; } }

        /// <summary>Defines the maximum value that the WaitTimeLimit property can be set to.  0.5 seconds.</summary>
        protected static readonly TimeSpan maxWaitTimeLimit = TimeSpan.FromSeconds(0.5);
        /// <summary>Defines the minimum value that the WaitTimeLimit property can be set to.  0.0 seconds.</summary>
        protected static readonly TimeSpan minWaitTimeLimit = TimeSpan.FromSeconds(0.0);
        /// <summary>Internal backing storage for the waitTimeLimit and WaitTimeLimit properties</summary>
        private TimeSpan innerWaitTimeLimitStore;

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

    #region IClientFacet ExtentionMethods

    /// <summary>
    /// common "namespace" class to define extension methods within.
    /// </summary>
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

        /// <summary>Creates and runs a GoOnline(andInitialize) action on the given part.  Returns the given part to support call chaining.</summary>
        public static TPartType RunGoOnlineActionInline<TPartType>(this TPartType part, bool andInitialize) where TPartType : IActivePartBase
        {
            part.CreateGoOnlineAction(andInitialize).RunInline();

            return part;
        }
    }

    #endregion
    
    //-----------------------------------------------------------------
}
