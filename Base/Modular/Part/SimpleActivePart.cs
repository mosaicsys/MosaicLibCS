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

		public class BasicActionImpl : ActionImplBase<NullObj, NullObj>, IBasicAction
		{
			public BasicActionImpl(ActionQueue actionQ, ActionMethodDelegateStrResult method, string mesg, ActionLogging loggingReference) : base(actionQ, method, new ActionLogging(mesg, loggingReference)) {}
			public BasicActionImpl(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, null, false, method, new ActionLogging(mesg, loggingReference)) { }
			public BasicActionImpl(ActionQueue actionQ, FullActionMethodDelegate<NullObj, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, null, false, method, new ActionLogging(mesg, loggingReference)) { }
		}

		public class ParamActionImplBase<ParamType> : ActionImplBase<ParamType, NullObj>, IBasicAction, IClientFacetWithParam<ParamType>
		{
			public ParamActionImplBase(ActionQueue actionQ, ParamType paramValue, ActionMethodDelegateActionArgStrResult<ParamType, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, paramValue, false, method, new ActionLogging(mesg, paramValue.ToString(), loggingReference)) { }
			public ParamActionImplBase(ActionQueue actionQ, ParamType paramValue, FullActionMethodDelegate<ParamType, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, paramValue, false, method, new ActionLogging(mesg, paramValue.ToString(), loggingReference)) { }

			public override bool SetParamValue(ParamType value) { bool done = base.SetParamValue(value); if (done) Logging.MesgDetail = value.ToString(); return done; }
			public override ParamType ParamValue { set { base.ParamValue = value; Logging.MesgDetail = value.ToString(); } }
		}

        public class BoolActionImpl : ParamActionImplBase<bool>, IBoolParamAction
        {
			public BoolActionImpl(ActionQueue actionQ, bool paramValue, ActionMethodDelegateActionArgStrResult<bool, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, paramValue, method, mesg, loggingReference) { }
            public BoolActionImpl(ActionQueue actionQ, bool paramValue, FullActionMethodDelegate<bool, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, paramValue, method, mesg, loggingReference) { }
        }

		public class StringActionImpl : ParamActionImplBase<string>, IStringParamAction
		{
			public StringActionImpl(ActionQueue actionQ, string paramValue, ActionMethodDelegateActionArgStrResult<string, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, paramValue, method, mesg, loggingReference) { }
			public StringActionImpl(ActionQueue actionQ, string paramValue, FullActionMethodDelegate<string, NullObj> method, string mesg, ActionLogging loggingReference) : base(actionQ, paramValue, method, mesg, loggingReference) { }
		}

		#endregion

		//-----------------------------------------------------------------
		#region CTOR and DTOR (et. al.)

		public SimpleActivePartBase(string partID, string partType) : this(partID, partType, TimeSpan.FromSeconds(0.1), true, 10) {}
		public SimpleActivePartBase(string partID, string partType, TimeSpan waitTimeLimit) : this(partID, partType, waitTimeLimit, true, 10) {}
		public SimpleActivePartBase(string partID, string partType, TimeSpan waitTimeLimit, bool enableQueue, int queueSize) : base(partID, partType)
		{
            TreatPartAsBusyWhenInternalPartBusyCountIsNonZero = true;       // allow derived objects to default to be able to use CreateInternalBusyFlagHolderObject

            this.waitTimeLimit = waitTimeLimit;

			actionQ = new ActionQueue(partID + ".q", enableQueue, queueSize);
			// NOTE: mActionQ.NotifyOnEnqueue.AddItem(mThreadWakeupNotifier) is performed as part of StartPart - which must not be invoked until the derived class is fully constructed.

			actionLoggingReference = new ActionLogging(Log, ActionLoggingConfig.Info_Error_Debug_Debug);
		}
	
		protected sealed override void Dispose(DisposeType disposeType)
		{
			if (disposeType == DisposeType.CalledExplicitly)
				StopPart();

			DisposeCalledPassdown(disposeType);
		}

		protected virtual void DisposeCalledPassdown(DisposeType disposeType) {}

		#endregion

        //-----------------------------------------------------------------
        #region Operational Settings

        /// <summary>
        /// When this property is set to true, the BaseState will automatically transition between Busy and Idle when Actions implementations are invoked and return.  
        /// When it is false the derived object will be entirely responsible for causing the component to transition between the Busy and Idle states.  This value
        /// defaults to True.
        /// </summary>
        public bool AutomaticallyIncAndDecBusyCountAroundActionInvoke { get; set; }

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

        // private volatile bool isStarting = false;        // currently unused
        private volatile bool hasBeenStarted = false;
        private volatile bool hasBeenStopped = false;
        private volatile bool hasStopBeenRequested = false;

        /// <summary>Stub for behavior derived objects would like to perform before the main action queue is enabled.</summary>
        protected virtual void PreStartPart() { }

		public virtual void StartPart()
		{
			using (Logging.EnterExitTrace t = new Logging.EnterExitTrace(Log, "StartPart", Logging.MesgType.Debug))
			{
                // isStarting = true;       // flag is not used at present

                PreStartPart();

				if (actionQ != null)
				{
					actionQ.NotifyOnEnqueue.OnNotify += this.Notify;
					actionQ.QueueEnable = true;
				}
				else
					throw new System.NullReferenceException(FmtStdEC("Failed to construct action queue"));

				if (mainThread == null)
					mainThread = new System.Threading.Thread(MainThreadFcn);

				if (mainThread == null)
					throw new System.NullReferenceException(FmtStdEC("Failed to construct service thread"));

				mainThread.Start();

                hasBeenStarted = true;
			}
		}

        /// <summary>Stub for behavior derived objects would like to perform immediately before the main action queue is disabled.</summary>
        protected virtual void PreStopPart() { }

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
					mainThread = null;
				}

				Log.Info.Emit("Part stopped");

                hasBeenStopped = true;
			}
		}

        public bool HasBeenStopped { get { return (hasBeenStopped || (actionQ != null && !actionQ.QueueEnable)); } }
        public bool HasBeenStarted { get { return (hasBeenStarted); } }
        public bool IsRunning { get { return (HasBeenStarted && !HasBeenStopped); } }

		private static object startIfNeededMutex = new object();

		protected void StartPartIfNeeded()
		{
			lock (startIfNeededMutex)		// only one caller of StartPartIfNeeded will be processed at a time (system wide...)
			{
                if (!HasBeenStarted)
					StartPart();
			}
		}

		public virtual IBasicAction CreateGoOnlineAction()
		{
			return (IBasicAction) CreateGoOnlineAction(false);
		}

		public virtual IBoolParamAction CreateGoOnlineAction(bool andInitialize)
		{
			StartPartIfNeeded();

			ActionMethodDelegateActionArgStrResult<bool, NullObj> method  = PerformGoOnlineAction;
            IBoolParamAction action = new BoolActionImpl(actionQ, andInitialize, method, "GoOnline", ActionLoggingReference);
			return action;
		}

		public virtual IBasicAction CreateGoOfflineAction()
		{
			ActionMethodDelegateActionArgStrResult<NullObj, NullObj> method  = PerformGoOfflineAction;
			IBasicAction action = new BasicActionImpl(actionQ, method, "GoOffline", ActionLoggingReference);
			return action;
		}

		// provide default CreateServiceAction method that creates one that will fail
		//	when it is run.  Sub-class may override the given DoRunServiceAction method
		//	to implement the ability to run services

		public virtual IStringParamAction CreateServiceAction() { return CreateServiceAction(string.Empty); }

		public virtual IStringParamAction CreateServiceAction(string paramValue) 
		{
			IStringParamAction action = new StringActionImpl(actionQ, paramValue, PerformServiceAction, "Service", ActionLoggingReference) as IStringParamAction;
			return action;
		}

		#endregion

		//-----------------------------------------------------------------
		#region INotifyable Members

		public void Notify()
		{
			threadWakeupNotifier.Notify();
		}

		#endregion

		//-----------------------------------------------------------------
		#region action related methods which may be re-implemented by sub-class

		/// <summary>Provide passthrough PerformGoOnlingAction implementation.  Invokes PerformGoOnlineAction(action.Param)</summary>
		protected virtual string PerformGoOnlineAction(IProviderActionBase<bool, NullObj> action)
		{
			return PerformGoOnlineAction(action.ParamValue);
		}

		/// <summary>Stub method provides default GoOnlineAction.  Always fails.</summary>
		/// <param name="andInitialize">true if caller wants method to initialize the part as then go online</param>
		/// <returns>"Action:YYY not implemented"</returns>
		protected virtual string PerformGoOnlineAction(bool andInitialize)
		{
			// NOTE: this method is not intended to be called by derived classes as it allways failes
			if (andInitialize)
				return "Action:GoOnlineAndInitialize not implemented";
			else
				return "Action:GoOnline not implemented";
		}

		/// <summary>Provide passthrough PerformGoOfflineAction implementation.  Invokes PerformGoOfflineAction();</summary>
		protected virtual string PerformGoOfflineAction(IProviderActionBase action)
		{
			return PerformGoOfflineAction();
		}

		/// <summary>Stub method provides default GoOfflineAction.  Always fails.</summary>
		/// <returns>"Action:GoOffline not implemented"</returns>
		protected virtual string PerformGoOfflineAction()
		{
			return "Action:GoOffline not implemented";
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
			using (Logging.EnterExitTrace t = new Logging.EnterExitTrace(Log, "MainThreadFcn", Logging.MesgType.Debug))
			{
				// The following is the part's main loop:
				//	Loop until the action queue has been disabled

				while (actionQ.QueueEnable)
				{
					// Reset the thread notification signal state so that we will only signal
					//	if a new notification has occurred since we started this loop.  We do not want to prevent the loop from sleeping because of somthing that
					//	we did before the loop started

					threadWakeupNotifier.Reset();

					PerformMainLoopService();

					if (!IssueNextQueuedAction())
						WaitForSomethingToDo();
				}
			}
		}

        /// <summary>
        /// Returns true once part stop has been requested (gets set after PreStopPart completes and actionQ has been disabled)
        /// </summary>
        protected bool HasStopBeenRequested { get { return (hasStopBeenRequested || (actionQ != null && !actionQ.QueueEnable)); } }

        /// <summary>
        /// empty base class method that can be overriden by derived classes to perform periodic service under control of the Part's main loop.
        /// </summary>
		protected virtual void PerformMainLoopService() 
		{
		}

        /// <summary>
        /// Attempts to get and perform the next action from the actionQ.
        /// </summary>
        /// <returns>True if an action was performed or false otherwise.</returns>
		protected virtual bool IssueNextQueuedAction()
		{
            IProviderFacet action = actionQ.GetNextAction();

		    if (action == null)
			    return false;

            PerformAction(action);

			return true;
		}

        /// <summary>Returns the IProviderFacet for the current action that the Part's main service loop is currently in IssueAndInvokeAction on, or null if none.</summary>
        protected IProviderFacet CurrentAction { get; private set; }

        /// <summary>Returns the IActionState for the current action that the Part's main service loop is currently in IssueAndInvokeAction on, or an Initial/Empty IActionState if there is no such action.</summary>
        protected IActionState CurrentActionState { get { return ((CurrentAction != null) ? CurrentAction.ActionState : EmptyActionState); } }

        protected readonly IActionState EmptyActionState = new ActionStateCopy();

        /// <summary>Requests to cancel the CurrentAction if it is non null and its state IsPendingCompletion</summary>
        protected void RequestCancelCurrentAction() 
        { 
            if (CurrentActionState.IsPendingCompletion) 
                CurrentAction.RequestCancel(); 
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

        /// <summary>Waits for threadWakeupNotifier to be signaled or default waitTimeLimit to elapse (used to set Parts's default spin rate).</summary>
        /// <returns>True if the object was signaled or false if the timeout caused flow to return to the caller.</returns>
		protected bool WaitForSomethingToDo()
		{
			return WaitForSomethingToDo(threadWakeupNotifier, waitTimeLimit);
		}

        /// <summary>Waits for threadWakeupNotifier to be signaled or given waitTimeLimit to elapse.</summary>
        /// <returns>True if the object was signaled or false if the timeout caused flow to return to the caller.</returns>
        protected bool WaitForSomethingToDo(TimeSpan waitTimeLimit)
		{
			return WaitForSomethingToDo(threadWakeupNotifier, waitTimeLimit);
		}

        /// <summary>Most generic version of WaitForSomethingToDo.  caller provides IWaitable object and waitTimeLimit.</summary>
        /// <returns>True if the object was signaled or false if the timeout caused flow to return to the caller.</returns>
        protected virtual bool WaitForSomethingToDo(Utils.IWaitable waitable, TimeSpan waitTimeLimit)
		{
			return waitable.Wait(waitTimeLimit);
		}

		#endregion

		//-----------------------------------------------------------------
		#region private and protected fields and related properties (includuing BaseState implementation)

		protected readonly TimeSpan waitTimeLimit;
		protected WaitEventNotifier threadWakeupNotifier = new WaitEventNotifier(WaitEventNotifier.Behavior.WakeOne);
		protected ActionQueue actionQ = null;
		protected System.Threading.Thread mainThread = null;
		protected ActionLogging actionLoggingReference = null;

        protected ActionQueue ActionQueue { get { return actionQ; } }

        protected override bool AreAllActionQueuesEmpty { get { return ActionQueue.IsEmpty; } }

        /// <summary>ActionLogging object that is used as the reference for commands created by SimpleActivePartBase and by many derived Part objects.</summary>
        protected ActionLogging ActionLoggingReference { get { return actionLoggingReference; } }

		#endregion

		//-----------------------------------------------------------------
	}

	#endregion

	//-----------------------------------------------------------------
}
