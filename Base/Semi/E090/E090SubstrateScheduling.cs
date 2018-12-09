//-------------------------------------------------------------------
/*! @file E090SubstrateScheduling.cs
 *  @brief 
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Text;

using MosaicLib;
using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Part;
using MosaicLib.Semi.E039;
using MosaicLib.Semi.E090;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Semi.E090.SubstrateScheduling
{
    /// <summary>
    /// This is the interface that is expected to be supported by any tool class used to implement customer specific substrate scheduling rules so that these rules
    /// may be used within common Scheduler Parts and constructs in a way that allows the rules to be reused in different environements.  
    /// This is specifically intended to support seamless use of such a tool class in both Semi standard hosted enironments and in much simpler unit testing environements, amongst other cases.
    /// </summary>
    public interface ISubstrateSchedulerTool<TSubstTrackerType, TProcessSpec, TProcessStepSpec> 
        where TSubstTrackerType : SubstrateAndProcessTrackerBase<TProcessSpec, TProcessStepSpec>
        where TProcessSpec : IProcessSpec<TProcessStepSpec>
        where TProcessStepSpec: IProcessStepSpec
    {
        /// <summary>Gives the INotifyable target that this tool is to attach to any Action it creates to notify the hosting part when the action completes</summary>
        INotifyable HostingPartNotifier { get; set; }

        /// <summary>Called when a new substrate tracker has been added.</summary>
        void Add(TSubstTrackerType substTracker);

        /// <summary>Called while dropping a substrate tracker</summary>
        void Drop(TSubstTrackerType substTracker);

        /// <summary>
        /// This method returns a list of reasons why the scheduler cannot be transitioned from the current given <paramref name="partBaseState"/> to the given <paramref name="requestedUseState"/>
        /// This is generally used when attempting to GoOnline, in which case the <paramref name="andInitialize"/> method will be provided as well.
        /// <para/>Note that when going offline, this method is intended to be advisory - it shall return the reasons why going offline might be a problem but there is no requirement that the caller will do anything specific with any non-empty resulting list in this case.
        /// </summary>
        IList<string> VerifyUseStateChange(IBaseState partBaseState, UseState requestedUseState, bool andInitialize = false);

        /// <summary>
        /// Called periodically.  
        /// <paramref name="recentTrackerChangeMayHaveOccurred"/> will be true if any substrate observer's flagged IsUpdateNeeded since the last Service call, or if the caller otherwise believes that a substrate tracker change may have occurred. 
        /// This method returns a count of the number of change it made or observed to allow the caller to optimze its spin loop sleep behavior to increase responsiveness in relation to loosely coupled state transitions.
        /// <para/>This method is expected to have different behavior based on the given <paramref name="partBaseState"/>.
        /// </summary>
        int Service(bool recentTrackerChangeMayHaveOccurred, ref SubstrateStateTally substrateStateTally, IBaseState partBaseState);
    }

    /// <summary>
    /// This is the interface provided by the SubstrateAndProcessTrackerBase class
    /// <para/>Please note that it cannot be used to construct or initialize a SubstrateTrackerBase instance.
    /// </summary>
    public interface ISubstrateAndProcessTrackerBase<TProcessSpecType, TProcessStepSpecType> : ISubstrateTrackerBase
        where TProcessSpecType : IProcessSpec<TProcessStepSpecType>
        where TProcessStepSpecType : IProcessStepSpec
    {
        TProcessSpecType ProcessSpec { get; }

        void Add(ProcessStepTrackerResultItem<TProcessStepSpecType> trackerResultItem, bool autoAdvanceRemainingStepSpecList = true, bool autoLatchFinalSPS = true);

        IList<ProcessStepTrackerResultItem<TProcessStepSpecType>> TrackerStepResultList { get; }

        IList<TProcessStepSpecType> RemainingStepSpecList { get; }
        TProcessStepSpecType NextStepSpec { get; }
    }

    /// <summary>
    /// Basic generic interface for a top level process specification.
    /// <para/>Contains a RecipeName and RecipeVariables.
    /// </summary>
    public interface IProcessSpec
    {
        string RecipeName { get; }
        INamedValueSet RecipeVariables { get; }
    }

    /// <summary>
    /// Templated extension of the basic IProcessSpec interface that adds the <typeparamref name="TProcessStepSpecType"/> and a read only list of same called Steps.
    /// </summary>
    public interface IProcessSpec<TProcessStepSpecType> : IProcessSpec
        where TProcessStepSpecType : IProcessStepSpec
    {
        ReadOnlyIList<TProcessStepSpecType> Steps { get; }
    }

    /// <summary>
    /// Basic interface for the specification of a process step.
    /// This includes a list of usable location names and a set of step recipe variables.
    /// It is generally updated with its StepNum and ProcessSpec when it is added to the ProcessSpec's Steps using the SetProcessSpecAndStepNum method which must only be used once.
    /// </summary>
    public interface IProcessStepSpec
    {
        IProcessSpec ProcessSpec { get; }
        int StepNum { get; }
        ReadOnlyIList<string> UsableLocNameList { get; }
        INamedValueSet StepVariables { get; }

        void SetProcessSpecAndStepNum(IProcessSpec processSpec, int stepNum);
    }

    /// <summary>
    /// Interface for results produced by running a process step spec.  Contains a ResultCode and an SPS.
    /// </summary>
    public interface IProcessStepResult
    {
        string ResultCode { get; }
        SubstProcState SPS { get; }
    }

    /// <summary>
    /// This structure is generated and recorded for each completed process step.
    /// </summary>
    public struct ProcessStepTrackerResultItem<TProcessStepSpecType> where TProcessStepSpecType : IProcessStepSpec
    {
        public string LocName { get; set; }
        public TProcessStepSpecType StepSpec { get; set; }
        public IProcessStepResult StepResult { get; set; }

        public override string ToString()
        {
            if (StepSpec != null && StepResult != null && StepResult.ResultCode.IsNullOrEmpty() && (StepResult.SPS == SubstProcState.Processed || StepResult.SPS == SubstProcState.ProcessStepCompleted))
                return "Step {0} completed successfully at loc:{1} [{2}]".CheckedFormat(StepSpec.StepNum, LocName, StepResult.SPS);
            else if (StepSpec != null && StepResult != null)
                return "Step {0} failed at loc:{1} [{2} {3}]".CheckedFormat(StepSpec.StepNum, LocName, StepResult.SPS, StepResult.ResultCode);
            else
                return "Invalid Result Item [LocName:{0} StepSpec:{1} StepResult:{2}]".CheckedFormat(LocName, StepSpec, StepResult);
        }
    }

    /// <summary>
    /// This is the interface provided by the SubstrateTrackerBase class.  
    /// <para/>Please note that it cannot be used to construct or initialize a SubstrateTrackerBase instance.
    /// </summary>
    public interface ISubstrateTrackerBase
    {
        int ServiceDropReasonAssertion();
        int ServiceBasicSJSStateChangeTriggers(ServiceBasicSJSStateChangeTriggerFlags flags);
        void SetSubstrateJobState(SubstrateJobState sjs, string reason, bool ifNeeded = true);

        E039ObjectID SubstID { get; }
        IE039TableUpdater E039TableUpdater { get; }
        Logging.IBasicLogger Logger { get; }

        E090SubstObserver SubstObserver { get; }
        E090SubstInfo Info { get; }
        QpcTimeStamp LastUpdateTimeStamp { get; set; }

        bool IsUpdateNeeded { get; }
        bool UpdateIfNeeded(bool forceUpdate = false);

        bool IsDropRequested { get; }
        string DropRequestReason { get; set; }

        IJobTrackerLinkage JobTrackerLinkage { get; set; }

        SubstrateJobRequestState SubstrateJobRequestState { get; set; }
    }

    /// <summary>
    /// Flags used with ISubstrateTrackerBase.ServiceBasicSJSStateChangeTriggers to control which of its available behaviors shall be enabled for a given use situation.
    /// <para/>None (0x00), EnableInfoTriggeredRules (0x01), EnableWaitingForStartRules (0x02), EnableAutoStart (0x04), EnablePausingRules (0x08),
    /// EnableStoppingRules (0x10), EnableAbortingRules (0x20), EnableRunningRules (0x40), All (0x7f)
    /// </summary>
    [Flags]
    public enum ServiceBasicSJSStateChangeTriggerFlags : int
    {
        None = 0x00,
        EnableInfoTriggeredRules = 0x01,
        EnableWaitingForStartRules = 0x02,
        EnableAutoStart = 0x04,
        EnablePausingRules = 0x08,
        EnableStoppingRules = 0x10,
        EnableAbortingRules = 0x20,
        EnableRunningRules = 0x40,
        EnableAbortedAtWork = 0x80,

        All = (EnableInfoTriggeredRules | EnableWaitingForStartRules | EnableAutoStart | EnablePausingRules | EnableStoppingRules | EnableAbortingRules | EnableRunningRules),
    }

    /// <summary>
    /// This interface may be used by a Job Tracker object that would like to be informed when a dependent substrate's observer has been updated.
    /// </summary>
    public interface IJobTrackerLinkage
    {
        /// <summary>
        /// Returns the "identity" of the job that has been linked to this substrate
        /// </summary>
        string ID { get; }

        /// <summary>
        /// Set to indicate that a dependent substrate's observer has been updated, cleared by the related Job Tracker entity once it has processed the change.
        /// </summary>
        bool SubstrateTrackerHasBeenUpdated { get; set; }

        /// <summary>
        /// Returns true if the corresponding job has had its DropRequestReason set to a non-empty value.
        /// </summary>
        bool IsDropRequested { get; }

        /// <summary>
        /// When non-empty, this string indicates the reason why this Job object is requesting that it be removed from the system.
        /// </summary>
        string DropRequestReason { get; }
    }

    /// <summary>
    /// This is a variant of an E090SubstLocObserver that accepts an IDictionary that gives the mapping of the known set of substrate trackers (of type <typeparamref name="TSubstrateAndProcessTrackerType"/>)
    /// and which looks up and obtains the tracker instance, placing it in the Tracker property, any time the underlying E090 SubstLoc's contents change, based on the FullName of the Subst object that is currently in the location.
    /// </summary>
    public class SubstLocObserverWithTrackerLookup<TSubstrateAndProcessTrackerType> 
        : E090SubstLocObserver
    {
        public SubstLocObserverWithTrackerLookup(E039ObjectID substLocID, IDictionary<string, TSubstrateAndProcessTrackerType> fullNameToSubstTrackerDictionaryIn)
            : this(substLocID.GetPublisher(), fullNameToSubstTrackerDictionaryIn)
        { }

        public SubstLocObserverWithTrackerLookup(ISequencedObjectSource<IE039Object, int> objLocPublisher, IDictionary<string, TSubstrateAndProcessTrackerType> fullNameToSubstTrackerDictionaryIn)
            : base(objLocPublisher, alsoObserveContents: true)
        {
            fullNameToSubstrTrackerDictionary = fullNameToSubstTrackerDictionaryIn;
        }

        protected override void UpdateContainsObject(IE039Object obj)
        {
            base.UpdateContainsObject(obj);

            Tracker = fullNameToSubstrTrackerDictionary.SafeTryGetValue(ContainsSubstInfo.ObjID.FullName);
        }

        public TSubstrateAndProcessTrackerType Tracker { get; private set; }

        protected IDictionary<string, TSubstrateAndProcessTrackerType> fullNameToSubstrTrackerDictionary;
    }

    /// <summary>
    /// This is a useful base class that allows the user to combine Substrate Tracking and Substrate Process Tracking.
    /// </summary>
    public class SubstrateAndProcessTrackerBase<TProcessSpecType, TProcessStepSpecType> 
        : SubstrateTrackerBase, ISubstrateAndProcessTrackerBase<TProcessSpecType, TProcessStepSpecType>
        where TProcessSpecType : IProcessSpec<TProcessStepSpecType>
        where TProcessStepSpecType : IProcessStepSpec
    {
        public virtual void Setup(IE039TableUpdater e039TableUpdater, E039ObjectID substID, Logging.IBasicLogger logger, TProcessSpecType processSpec)
        {
            base.Setup(e039TableUpdater, substID, logger);
            ProcessSpec = processSpec;

            remainingStepSpecList = new List<TProcessStepSpecType>(ProcessSpec.Steps);
        }

        public TProcessSpecType ProcessSpec { get; private set; }

        public virtual void Add(ProcessStepTrackerResultItem<TProcessStepSpecType> trackerResultItem, bool autoAdvanceRemainingStepSpecList = true, bool autoLatchFinalSPS = true)
        {
            trackerStepResultList.Add(trackerResultItem);

            if (autoAdvanceRemainingStepSpecList && remainingStepSpecList.Count > 0)
                remainingStepSpecList.RemoveAt(0);

            if (!autoAdvanceRemainingStepSpecList)
            { }
            else if (NextStepSpec != null)
            {
                Logger.Debug.Emit("Completed {0} for {1}: Next Step: [{2}]", trackerResultItem, SubstID.FullName, NextStepSpec);
            }
            else
            {
                if (autoLatchFinalSPS)
                    E039TableUpdater.SetSubstProcState(SubstObserver, GetFinalSPS());

                Logger.Debug.Emit("Completed {0} for {1}: No more steps. [inferredSPS:{2}]", trackerResultItem, SubstID.FullName, SubstObserver.Info.InferredSPS);
            }
        }

        protected virtual SubstProcState GetFinalSPS()
        {
            var finalSPS = trackerStepResultList.Aggregate(SubstObserver.Info.InferredSPS, (a, b) => a.MergeWith(b.StepResult.SPS));

            return (finalSPS.IsProcessStepComplete() ? SubstProcState.Processed : finalSPS);
        }

        public virtual IList<ProcessStepTrackerResultItem<TProcessStepSpecType>> TrackerStepResultList { get { return trackerStepResultList; } }

        public virtual IList<TProcessStepSpecType> RemainingStepSpecList { get { return remainingStepSpecList; } }
        public virtual TProcessStepSpecType NextStepSpec { get { return RemainingStepSpecList.FirstOrDefault(); } }

        protected List<TProcessStepSpecType> remainingStepSpecList;
        protected List<ProcessStepTrackerResultItem<TProcessStepSpecType>> trackerStepResultList = new List<ProcessStepTrackerResultItem<TProcessStepSpecType>>();
    }

    /// <summary>
    /// Useful and/or base class to be used to contain a specified process includings one or more steps.
    /// </summary>
    public class ProcessSpecBase<TProcessStepSpecType> : IProcessSpec<TProcessStepSpecType>
        where TProcessStepSpecType : IProcessStepSpec
    {
        public ProcessSpecBase(string recipeName, INamedValueSet recipeVariables = null, IEnumerable<TProcessStepSpecType> stepSpecSet = null)
        {
            RecipeName = recipeName.MapNullToEmpty();
            RecipeVariables = recipeVariables.ConvertToReadOnly();
            Steps = new ReadOnlyIList<TProcessStepSpecType>(stepSpecSet.SafeToArray().Select((stepSpec, idx) => KVP.Create(idx + 1, stepSpec)).DoForEach(kvp => kvp.Value.SetProcessSpecAndStepNum(this, kvp.Key)).Select(kvp => kvp.Value));
        }

        public string RecipeName { get; private set; }
        public INamedValueSet RecipeVariables { get; private set; }
        public ReadOnlyIList<TProcessStepSpecType> Steps { get; private set; }

        public override string ToString()
        {
            return "ProcessSpec Rcp:'{0}' RcpVars:{1} Steps:{2}".CheckedFormat(RecipeName, RecipeVariables.SafeToStringSML(), Steps.SafeCount());
        }
    }

    /// <summary>
    /// Useful and/or base class to be used as a process step spec implementation class.
    /// </summary>
    public class ProcessStepSpecBase : IProcessStepSpec
    {
        public ProcessStepSpecBase(IEnumerable<string> usableLocSet, INamedValueSet stepVariables = null)
            : this(null, 0, usableLocSet, stepVariables)
        { }

        public ProcessStepSpecBase(int stepNum, IEnumerable<string> usableLocSet, INamedValueSet stepVariables = null)
            : this (null, stepNum, usableLocSet, stepVariables)
        { }

        public ProcessStepSpecBase(IProcessSpec processSpec, int stepNum, IEnumerable<string> usableLocSet, INamedValueSet stepVariables = null)
        {
            ProcessSpec = processSpec;
            StepNum = stepNum;
            UsableLocNameList = new ReadOnlyIList<string>(usableLocSet);
            StepVariables = stepVariables.ConvertToReadOnly();
        }

        public IProcessSpec ProcessSpec { get; private set; }
        public int StepNum { get; private set; }
        public ReadOnlyIList<string> UsableLocNameList { get; private set; }
        public INamedValueSet StepVariables { get; private set; }

        public void SetProcessSpecAndStepNum(IProcessSpec processSpec, int stepNum)
        {
            if (ProcessSpec != null || StepNum != 0)
                throw new System.InvalidOperationException("This method is not valid once a non-null ProcessSpec, or a non-zero StepNum has been assigned to this step");

            ProcessSpec = processSpec;
            StepNum = stepNum;
        }

        public override string ToString()
        {
            var rcpName = (ProcessSpec != null ? ProcessSpec.RecipeName : "[NullProcessSpec]");
            return "ProcessStepSpec Rcp:'{0}' StepNum:{1} UsableLocNameList:[{2}] StepVars:{3}".CheckedFormat(rcpName, StepNum, String.Join(", ", UsableLocNameList), StepVariables.SafeToStringSML());
        }
    }

    /// <summary>
    /// Useful and/or base class to be used as a process step result implementation class.
    /// </summary>
    public class ProcessStepResultBase : IProcessStepResult
    {
        public ProcessStepResultBase(string resultCode = "", SubstProcState sps = SubstProcState.Undefined, SubstProcState fallbackFailedSPS = SubstProcState.Rejected, SubstProcState defaultSucceededSPS = SubstProcState.ProcessStepCompleted)
        {
            ResultCode = resultCode.MapNullToEmpty();
            SPS = (sps != SubstProcState.Undefined) ? sps : (ResultCode.IsNullOrEmpty() ? defaultSucceededSPS : fallbackFailedSPS);
        }

        public string ResultCode { get; private set; }
        public SubstProcState SPS { get; private set; }
    }

    /// <summary>
    /// This is the base substrate tracking class for use with E090 Substrate Scheduling.
    /// </summary>
    public class SubstrateTrackerBase : ISubstrateTrackerBase
    {
        public virtual void Setup(IE039TableUpdater e039TableUpdater, E039ObjectID substID, Logging.IBasicLogger logger)
        {
            SubstID = substID.MapNullToEmpty();
            E039TableUpdater = e039TableUpdater;
            Logger = logger;

            var substPublisher = E039TableUpdater.GetPublisher(SubstID);

            if (substPublisher != null)
                SubstObserver = new E090SubstObserver(substPublisher);
            else
                throw new SubstrateSchedulingException("No substrate object found for given id '{0}'".CheckedFormat(SubstID.FullName));

            SetSubstrateJobState(SubstrateJobState.WaitingForStart, Fcns.CurrentMethodName);
        }

        public virtual int ServiceDropReasonAssertion()
        {
            int didSomethingCount = 0;

            if (DropRequestReason.IsNullOrEmpty())
            {
                string nextDropReasonRequest = String.Empty;

                if (SubstObserver.Info.SPS.IsProcessingComplete() && SubstObserver.Info.STS != SubstState.AtWork)
                {
                    if (JobTrackerLinkage == null)
                        nextDropReasonRequest = "Substrate processing done and no Job was linked to it";
                    else if (JobTrackerLinkage.IsDropRequested)
                        nextDropReasonRequest = "Substrate processing done and linked Job is requesting to be dropped [{0}]".CheckedFormat(JobTrackerLinkage.DropRequestReason);
                }
                else if (SubstObserver.Info.IsFinal)              // this is the normal substrate removed case
                {
                    if (JobTrackerLinkage == null)
                        nextDropReasonRequest = "Substrate Object has been removed and no Job was linked to it";
                    else if (JobTrackerLinkage.IsDropRequested)
                        nextDropReasonRequest = "Substrate Object has been removed and linked Job is requesting to be dropped [{0}]".CheckedFormat(JobTrackerLinkage.DropRequestReason);
                }
                else if (SubstObserver.Object == null)      // this is not an expected case
                {
                    nextDropReasonRequest = "Substrate Object has been removed unexpectedly";
                }
                else if (SubstObserver.Object.IsEmpty)      // this is not an expected case
                {
                    nextDropReasonRequest = "Substrate Object has been emptied unexpectedly";
                }

                if (DropRequestReason != nextDropReasonRequest)
                {
                    DropRequestReason = nextDropReasonRequest;      // setter logs changes in reason

                    didSomethingCount++;
                }
            }

            return didSomethingCount;
        }

        public virtual int ServiceBasicSJSStateChangeTriggers(ServiceBasicSJSStateChangeTriggerFlags flags)
        {
            int didSomethingCount = 0;

            bool enableInfoTriggeredRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableInfoTriggeredRules) != 0);
            bool enableWaitingForStartRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableWaitingForStartRules) != 0);
            bool enableAutoStart = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableAutoStart) != 0);
            bool enablePausingRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnablePausingRules) != 0);
            bool enableStoppingRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableStoppingRules) != 0);
            bool enableAbortingRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableAbortingRules) != 0);
            bool enableRunningRules = ((flags & ServiceBasicSJSStateChangeTriggerFlags.EnableRunningRules) != 0);

            var stInfo = SubstObserver.Info;
            SubstState sts = stInfo.STS;
            bool stsIsAtSource = sts.IsAtSource();
            bool stsIsAtDestination = sts.IsAtDestination();
            bool stsIsAtWork = sts.IsAtWork();
            bool isAtSrcLoc = stInfo.LocID == stInfo.LinkToSrc.ToID.Name;
            bool isAtDestLoc = stInfo.LocID == stInfo.LinkToDest.ToID.Name;

            SubstProcState sps = stInfo.SPS;
            bool spsIsNeedsProcessing = sps.IsNeedsProcessing();

            SubstrateJobState nextSJS = SubstrateJobState.Initial;
            string reason = null;

            TimeSpan stateAge = LastUpdateTimeStamp.Age;

            if (enableInfoTriggeredRules)
            {
                if (sps == SubstProcState.Lost)
                {
                    nextSJS = SubstrateJobState.Lost;
                    reason = "Substrate has been marked Lost";
                }
                else if (stInfo.SJRS == SubstrateJobRequestState.Return)
                {
                    if (stsIsAtSource || stsIsAtDestination || isAtSrcLoc || isAtDestLoc)
                        nextSJS = SubstrateJobState.Returned;
                    else
                        nextSJS = SubstrateJobState.Returning;
                }
                else if (stsIsAtSource)
                {
                    switch (sps)
                    {
                        case SubstProcState.Skipped: nextSJS = SubstrateJobState.Skipped; break;
                        default: break;
                    }
                }
                else if (stsIsAtDestination)
                {
                    switch (sps)
                    {
                        case SubstProcState.Processed: nextSJS = SubstrateJobState.Processed; break;
                        case SubstProcState.Rejected: nextSJS = SubstrateJobState.Rejected; break;
                        case SubstProcState.Skipped: nextSJS = SubstrateJobState.Skipped; break;
                        case SubstProcState.Stopped: nextSJS = SubstrateJobState.Stopped; break;
                        case SubstProcState.Aborted: nextSJS = SubstrateJobState.Aborted; break;
                        default: break;
                    }
                }
                else if (Info.IsFinal)
                {
                    nextSJS = SubstrateJobState.Removed;
                    reason = "Substrate has been removed/deleted unexpectedly";
                }

                if (nextSJS != SubstrateJobState.Initial && reason.IsNullOrEmpty())
                    reason = "Substrate reached a final state processing/transport state";
            }

            if (nextSJS == SubstrateJobState.Initial && stsIsAtWork && sps == SubstProcState.Aborted && enableAbortingRules && flags.IsSet(ServiceBasicSJSStateChangeTriggerFlags.EnableAbortedAtWork))
            {
                nextSJS = E090.SubstrateJobState.Aborted;
                reason = "Substrate reached Aborted state AtWork";
            }

            if (nextSJS == SubstrateJobState.Initial)
            {
                switch (SubstrateJobState)
                {
                    case SubstrateJobState.WaitingForStart:
                        if (enableWaitingForStartRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Run && enableAutoStart)
                            {
                                nextSJS = SubstrateJobState.Running;
                                reason = "Run requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Pause)
                            {
                                nextSJS = SubstrateJobState.Pausing;
                                reason = "Pause requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Stop)
                            {
                                nextSJS = SubstrateJobState.Stopping;
                                reason = "Stop requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                        }
                        break;

                    case SubstrateJobState.Pausing:
                        if (enablePausingRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Stop)
                            {
                                nextSJS = SubstrateJobState.Stopping;
                                reason = "Stop requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                            else if (spsIsNeedsProcessing && stsIsAtSource)
                            {
                                nextSJS = SubstrateJobState.Paused;
                                reason = "Paused condition reached";
                            }
                        }
                        break;

                    case SubstrateJobState.Stopping:
                        if (enableStoppingRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                            else if (stsIsAtSource)
                            {
                                nextSJS = SubstrateJobState.Skipped;
                                reason = "Stop completed";
                            }
                        }
                        break;

                    case SubstrateJobState.Aborting:
                        if (enableAbortingRules)
                        {
                            if (stsIsAtSource)
                            {
                                nextSJS = SubstrateJobState.Skipped;
                                reason = "Abort completed";
                            }
                        }
                        break;

                    case SubstrateJobState.Running:
                        if (enableRunningRules)
                        {
                            if (SubstrateJobRequestState == SubstrateJobRequestState.Pause)
                            {
                                nextSJS = SubstrateJobState.Pausing;
                                reason = "Pause requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Stop)
                            {
                                nextSJS = SubstrateJobState.Stopping;
                                reason = "Stop requested";
                            }
                            else if (SubstrateJobRequestState == SubstrateJobRequestState.Abort)
                            {
                                nextSJS = SubstrateJobState.Aborting;
                                reason = "Abort requested";
                            }
                        }
                        break;

                    case SubstrateJobState.Paused:
                    case SubstrateJobState.Processed:
                    case SubstrateJobState.Stopped:
                    case SubstrateJobState.Aborted:
                    default:
                        break;
                }
            }

            if (nextSJS != SubstrateJobState.Initial && SubstrateJobState != nextSJS)
            {
                SetSubstrateJobState(nextSJS, "{0} [{1} {2} {3}]".CheckedFormat(reason, sps, sts, SubstrateJobRequestState));
                didSomethingCount++;
            }

            return didSomethingCount;
        }

        private const E090StateUpdateBehavior finalSPSUpdateBehavior = (E090StateUpdateBehavior.StandardSPSUpdate | E090StateUpdateBehavior.BasicSPSLists);
        private const E090StateUpdateBehavior pendingSPSUpdateBehavior = (E090StateUpdateBehavior.PendingSPSUpdate | E090StateUpdateBehavior.BasicSPSLists);

        public virtual void SetSubstrateJobState(SubstrateJobState sjs, string reason, bool ifNeeded = true)
        {
            var entrySJS = SubstrateJobState;

            if (ifNeeded && entrySJS == sjs)
            {
                Logger.Debug.Emit("{0}({1}, sjs:{2}, reason:'{3}'): Not needed", Fcns.CurrentMethodName, SubstID.FullName, sjs, reason);
                return;
            }

            Logger.Trace.Emit("{0}({1}, sjs:{2}, reason:'{3}'{4})  current sjs:{5}", Fcns.CurrentMethodName, SubstID.FullName, sjs, reason, ifNeeded ? ", IfNeeded" : "", entrySJS);

            SubstrateJobState = sjs;

            List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();
            updateItemList.Add(new E039UpdateItem.SetAttributes(SubstID, new NamedValueSet() { { "SJS", SubstrateJobState } }));

            switch (SubstrateJobState)
            {
                case SubstrateJobState.Processed:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    else if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Processed, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Stopping:
                    break;

                case SubstrateJobState.Stopped:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    else if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Stopped, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Aborting:
                    if (Info.InferredSPS != SubstProcState.Aborted)
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Aborted, updateBehavior: pendingSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Aborted:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    else if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Aborted, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Skipped:
                    if (Info.SPS.IsProcessingComplete())
                    { } // nothing more to do
                    if (Info.InferredSPS.IsProcessingComplete())
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: Info.InferredSPS, updateBehavior: finalSPSUpdateBehavior);
                    else
                        updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Skipped, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Lost:
                    updateItemList.GenerateE090UpdateItems(Info, spsParam: SubstProcState.Lost, updateBehavior: finalSPSUpdateBehavior);
                    break;

                case SubstrateJobState.Returned:
                case SubstrateJobState.Returning:
                case SubstrateJobState.Held:
                case SubstrateJobState.RoutingAlarm:
                default:
                    break;
            }

            if (updateItemList.Count > 0)
            {
                // check settings to see if we need to explicitly add an external sync item
                if (E090.Settings.GetUseExternalSync(checkNoteSubstrateMovedAdditions: false, checkSetSubstProcStateAdditions: true, checkGenerateUpdateItemAdditions: true))
                    updateItemList.Add(syncExternal);

                E039TableUpdater.Update(updateItemList.ToArray()).Run();
            }

            Logger.Debug.Emit("{0}: SJS changed to {1} [from:{2} reason:{3}]", SubstID.FullName, sjs, entrySJS, reason);
        }

        public E039ObjectID SubstID { get; private set; }
        public IE039TableUpdater E039TableUpdater { get; private set; }
        public Logging.IBasicLogger Logger { get; private set; }

        public E090SubstObserver SubstObserver { get; private set; }
        public E090SubstInfo Info { get { return SubstObserver.Info; } }
        public QpcTimeStamp LastUpdateTimeStamp { get; set; }

        public virtual bool IsUpdateNeeded { get { return SubstObserver.IsUpdateNeeded; } set { SubstObserver.IsUpdateNeeded = value; } }
        public virtual bool UpdateIfNeeded(bool forceUpdate = false)
        {
            if (!IsUpdateNeeded && !forceUpdate)
                return false;

            bool changed = SubstObserver.Update(forceUpdate: forceUpdate);
            if (changed)
            {
                if (JobTrackerLinkage != null && !JobTrackerLinkage.SubstrateTrackerHasBeenUpdated)
                    JobTrackerLinkage.SubstrateTrackerHasBeenUpdated = true;

                LastUpdateTimeStamp = QpcTimeStamp.Now;
            }

            return changed;
        }

        public bool IsDropRequested { get { return !DropRequestReason.IsNullOrEmpty(); } }
        public string DropRequestReason
        {
            get { return _dropReasonRequest; }
            set
            {
                var entryDropReasonRequest = _dropReasonRequest;

                _dropReasonRequest = value.MapNullToEmpty();

                IsUpdateNeeded = true;

                if (_dropReasonRequest != entryDropReasonRequest)
                {
                    if (entryDropReasonRequest.IsNullOrEmpty())
                        Logger.Trace.Emit("{0}: Drop reason requested as '{1}' [{2}]", SubstID.FullName, _dropReasonRequest, SubstObserver.Info.ToString(includeSubstID: false));
                    else
                        Logger.Trace.Emit("{0}: Drop reason request changed to '{1}' [from:'{2}', {3}]", SubstID.FullName, _dropReasonRequest, entryDropReasonRequest, SubstObserver.Info.ToString(includeSubstID: false));
                }
            }
        }
        private string _dropReasonRequest = String.Empty;

        public IJobTrackerLinkage JobTrackerLinkage
        {
            get { return _jobTrackerLinkage; }
            set
            {
                var entryLinkageID = (_jobTrackerLinkage != null) ? _jobTrackerLinkage.ID : null;

                _jobTrackerLinkage = value;
                var newLinkageID = (_jobTrackerLinkage != null) ? _jobTrackerLinkage.ID : null;

                IsUpdateNeeded = true;

                if (entryLinkageID != newLinkageID)
                {
                    if (!newLinkageID.IsNullOrEmpty() && entryLinkageID.IsNullOrEmpty())
                        Logger.Trace.Emit("{0}: Linkage added to job id:{1}", SubstID.FullName, newLinkageID);
                    else if (newLinkageID.IsNullOrEmpty() && !entryLinkageID.IsNullOrEmpty())
                        Logger.Trace.Emit("{0}: Linkage removed from job id:{1}", SubstID.FullName, entryLinkageID);
                    else
                        Logger.Trace.Emit("{0}: Linkage changed to job id:{1} [from:{2}]", SubstID.FullName, newLinkageID, entryLinkageID);
                }
            }
        }
        private IJobTrackerLinkage _jobTrackerLinkage = null;

        public SubstrateJobRequestState SubstrateJobRequestState
        {
            get { return _substrateJobRequestState; }
            set
            {
                if (_substrateJobRequestState != value)
                {
                    _substrateJobRequestState = value;

                    E039TableUpdater.Update(new E039UpdateItem.SetAttributes(SubstID, new NamedValueSet() { { "SJRS", value } })).Run();
                }
            }
        }
        private SubstrateJobRequestState _substrateJobRequestState;

        public SubstrateJobState SubstrateJobState { get; set; }
        public string SubstrateJobStateReason { get; set; }

        protected static readonly E039UpdateItem.SyncExternal syncExternal = new E039UpdateItem.SyncExternal();
    }

    /// <summary>
    /// This object type is used to count up the states of a set of one or more SubstrateTrackerBase objects to give a quick overview of the distribution of such a set of subsrates.
    /// </summary>
    public struct SubstrateStateTally
    {
        public int total;

        public int stsAtSource, stsAtWork, stsAtDestination, stsOther, stsLostAnywhere, stsRemovedAnywhere, stsLostOrRemovedAnywhere;
        
        public int spsNeedsProcessing;
        public int spsInProcess;
        public int spsProcessed, spsStopped, spsRejected, spsAborted, spsSkipped, spsLost;
        public int spsProcessStepCompleted, spsOther;

        public int sjsWaitingForStart, sjsRunning, sjsProcessed, sjsRejected, sjsSkipped, sjsPausing, sjsPaused, sjsStopping, sjsStopped, sjsAborting, sjsAborted, sjsLost, sjsReturning, sjsReturned, sjsHeld, sjsRoutingAlarm, sjsRemoved, sjsOther;
        public int sjsAbortedAtDestination;

        public void Add(SubstrateTrackerBase st)
        {
            total++;

            var info = st.SubstObserver.Info;

            var sts = info.STS;
            var inferredSPS = info.InferredSPS;

            if (info.SPS == SubstProcState.Lost)
            {
                stsLostAnywhere++;
                stsLostOrRemovedAnywhere++;
            }
            else if (info.IsFinal)
            {
                stsRemovedAnywhere++;
                stsLostOrRemovedAnywhere++;
            }
            else
            {
                switch (sts)
                {
                    case SubstState.AtSource: stsAtSource++; break;
                    case SubstState.AtWork: stsAtWork++; break;
                    case SubstState.AtDestination: stsAtDestination++; break;
                    default: stsOther++; break;
                }
            }

            switch (inferredSPS)
            {
                case SubstProcState.NeedsProcessing: spsNeedsProcessing++; break;
                case SubstProcState.InProcess: spsInProcess++; break;
                case SubstProcState.Processed: spsProcessed++; break;
                case SubstProcState.Stopped: spsStopped++; break;
                case SubstProcState.Rejected: spsRejected++; break;
                case SubstProcState.Aborted: spsAborted++; break;
                case SubstProcState.Skipped: spsSkipped++; break;
                case SubstProcState.Lost: spsLost++; break;
                case SubstProcState.ProcessStepCompleted: spsProcessStepCompleted++; break;
                default: spsOther++; break;
            }

            switch (st.SubstrateJobState)
            {
                case SubstrateJobState.WaitingForStart: sjsWaitingForStart++; break;
                case SubstrateJobState.Running: sjsRunning++; break;
                case SubstrateJobState.Processed: sjsProcessed++; break;
                case SubstrateJobState.Rejected: sjsRejected++; break;
                case SubstrateJobState.Skipped: sjsSkipped++; break;
                case SubstrateJobState.Pausing: sjsPausing++; break;
                case SubstrateJobState.Paused: sjsPaused++; break;
                case SubstrateJobState.Stopping: sjsStopping++; break;
                case SubstrateJobState.Stopped: sjsStopped++; break;
                case SubstrateJobState.Aborting: sjsAborting++; if (sts == SubstState.AtDestination) sjsAbortedAtDestination++; break;
                case SubstrateJobState.Aborted: sjsAborted++; break;
                case SubstrateJobState.Lost: sjsLost++; break;
                case SubstrateJobState.Returning: sjsReturning++; break;
                case SubstrateJobState.Returned: sjsReturned++; break;
                case SubstrateJobState.Held: sjsHeld++; break;
                case SubstrateJobState.RoutingAlarm: sjsRoutingAlarm++; break;
                case SubstrateJobState.Removed: sjsRemoved++; break;
                default: sjsOther++; break;
            }
        }

        private static string CustomToString(INamedValueSet nvs, string emptyString = "[Empty]")
        {
            StringBuilder sb = new StringBuilder();

            foreach (var nv in nvs)
            {
                int count = nv.VC.GetValue<int>(rethrow: false);

                if (count > 0)
                {
                    if (sb.Length != 0)
                        sb.Append(" ");

                    sb.CheckedAppendFormat("{0}:{1}", nv.Name, count);
                }
            }

            return sb.ToString().MapNullOrEmptyTo(emptyString);
        }

        private NamedValueSet GetSTSNVS()
        {
            return new NamedValueSet()
            {
                { "AtSource", stsAtSource },
                { "AtWork", stsAtWork },
                { "AtDestination", stsAtDestination },
                { "Lost", stsLostAnywhere },
                { "Removed", stsRemovedAnywhere },
                { "Other", stsOther },
            };
        }

        private NamedValueSet GetSPSNVS()
        {
            return new NamedValueSet()
            {
                { "NeedsProcessing", spsNeedsProcessing },
                { "InProcess", spsInProcess },
                { "ProcessStepCompleted", spsProcessStepCompleted },
                { "Processed", spsProcessed },
                { "Rejected", spsRejected },
                { "Stopped", spsStopped },
                { "Skipped", spsSkipped },
                { "Aborted", spsAborted },
                { "Lost", spsLost },
                { "Other", spsOther },
            };
        }

        private NamedValueSet GetSJSNVS()
        {
            return new NamedValueSet()
            {
                { "WaitingForStart", sjsWaitingForStart },
                { "Running", sjsRunning },
                { "Processed", sjsProcessed },
                { "Rejected", sjsRejected },
                { "Skipped", sjsSkipped},
                { "Pausing", sjsPausing },
                { "Paused", sjsPaused },
                { "Stopping", sjsStopping },
                { "Stopped", sjsStopped },
                { "Aborting", sjsAborting },
                { "Aborted", sjsAborted },
                { "Lost", sjsLost },
                { "Returning", sjsReturning },
                { "Returned", sjsReturned },
                { "Held", sjsHeld },
                { "RoutingAlarm", sjsRoutingAlarm },
                { "Removed", sjsRemoved },
                { "Other", sjsOther },
            };
        }

        [Obsolete("Please switch to the use of the STSToString method. (2018-12-08)")]
        public string SLSToString() { return STSToString(); }

        public string STSToString() { return CustomToString(GetSTSNVS()); }

        public string SPSToString() { return CustomToString(GetSPSNVS()); }

        public string SJSToString() { return CustomToString(GetSJSNVS()); }

        public override string ToString()
        {
            return "sts:[{0}] sps:[{1}] sjs:[{2}]".CheckedFormat(CustomToString(GetSTSNVS(), emptyString: "None"), CustomToString(GetSPSNVS(), emptyString: "None"), CustomToString(GetSJSNVS(), emptyString: "None"));
        }
    }

    /// <summary>
    /// Exception class that is intended for use in this substrate scheduling, and related, code.
    /// </summary>
    public class SubstrateSchedulingException : System.Exception
    {
        public SubstrateSchedulingException(string mesg, System.Exception innerException = null) 
            : base(mesg, innerException) 
        { }
    }
}
