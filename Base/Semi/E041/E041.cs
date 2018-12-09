//-------------------------------------------------------------------
/*! @file E041.cs
 *	@brief This file provides definitions related to the use of an Annunciator concept that is generally based on the E041 Semi standard.
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2015 Mosaic Systems Inc.
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
using System.Text;
using System.Runtime.Serialization;

using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;

using MosaicLib.Semi.E005;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Semi.E041
{
	//-------------------------------------------------------------------
    // Note: the E041 standard (dated 1995) is based on the concept of an Exception and tends
    // to prefix the standard's defined types and values with "EX".  This is assumed to
    // be a shorthand for the term "Exception".
    //
    // To improve the overall usefulness of the concepts, definitions, types, and objects defined here, 
    // the term Exception is being replaced here with the term Annunciator (and the prefix is changed from "EX" to "AN").  
    // This is intended to clarify and convey the concept that such a term can be used for well known 
    // situations where the system wishes to provide an indication of an Alarm or Error condition but, 
    // additionally, that the term can be used in places where the system wishes to provide indications 
    // of Warning, non-fatal or other non-error attention conditions.  The general ability to use a common 
    // annunciator mechanism and to associated it with a specific set of actions that a user (or other external 
    // agent/decision authority) may request is a more generally capable use model.  It is a 
    // proper super-set of the functions required to support the E041 Exception use model and as such can 
    // covers that case as well.
    //
    // In addition where E041 refers to a recovery action, we will simply refer to an 
    // action name since the more general term is more useful for situations where the 
    // annunciator is simply being used as a way to prompt the user/decision authority to select a desired action
    // at an appropriate time.
    //
    // Please NOTE that the term action name as used with objects in the E041 namespace does not
    // imply any relationship to, or use of, the MosaicLib.Modular.Action patterns, objects or types.

    /*
     * Note 2: The E041 Annunciator system is being significantly altered in the MosaicLibCS version.
     * - The Part accessible portions (IANSource, IANCondition, IANOccurrence) are now focused on the form and utility of supporting the client code that uses the desired annunciator source object. 
     * - the annunciator manager is now a SimpleActivePart and all external interactions with it are marshaled as such.
     * - the annunciator manager has the ability to generate and publish a history of changes to each annunciator's state over time (including action management from outside).
     * - the annuncaitor manager has the ability to keep track of the "active" annunciators and to publish a collection of these that is suitable for use in the GUI.
     * 
     * The focus on the types and classes defined here are changed to be more focused on the utility to the annunciator source object types, and are somewhat less focused on 
     * the exact behavior of annunciator related visualization.  In particular the visibility state from the C++ library has been removed.  The implementation of visibility is now locally 
     * rendered by whether a given IANSource is present in the active annunciator set.  The GUI may further modify this logic as desired to control which active annunciators are actually
     * displayed to the user and/or how they are presented.  
     * 
     * The combination of the annunciator source and the ANMangerPart are now entirely responsible for which annunciators are retained in the set of active annuncators.  
     * For IANCondition and IANOccurrence use model objects, the ANManagerPart generally controls when the related annunciator accepts an Acknoweldge request and thus becomes non-signaling.  
     * For the more general IANSource use model objects, the client code is entirely responsible for when it's annunciators transition from signaling to non-signaling 
     * (using the Clear method).  This may be done with or without having any form of interaction with the user/decision authority.
     */

    //-------------------------------------------------------------------

    #region Constants

    /// <summary>
    /// Constants
    /// </summary>
    public static partial class Constants
    {
        /// <summary>This is the standard string that is used as the action for IANOccurrence and IANCondition annunciators. "Acknowledge"</summary>
        public const string Acknowledge = "Acknowledge";

        /// <summary>This is shorthand string that is commonly used by clients as a form of Acknowledge action.  "Ack"</summary>
        public const string Ack = "Ack";
    }

    #endregion

    #region ANType, ANSignalState

    /// <summary>
    /// Defines an extended version of the E041 ANType.  
    /// The values used here are intended to be usable as E005.ALCD values, however the predefined ALCD values do not appear to have a one to one relationship with the 
    /// matching E041 types.  As such the values assigned here are derived from custom values defined in the customer specific range of ALCD values.
    /// <para/>Attention (100), Warning (101), Error (102), Alarm (103), Dynamic (104)
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.SemiNameSpace)]
    public enum ANType : int
	{
        /// <summary>
        /// This is not an indication of any form of failure.  
        /// It is simply used to inform the user/decision authority of some occurrence and optionally request/prompt the user/decision authority to provide some input.  
        /// These generally do not have an associated ALID. (100)
        /// </summary>
        [EnumMember]
        Attention = E005.ALCD.E041_Attention,

        /// <summary>Similar to an alarm but not generally passed to the host, may not have a known ALID</summary>
        [EnumMember]
        Warning = E005.ALCD.E041_Warning,

        /// <summary>Error annunciators are similar to Alarm annunciators except that they support (recovery) action invocation by the currently active decision authority.</summary>
        [EnumMember]
        Error = E005.ALCD.E041_Error,

        /// <summary>
        /// Under E041 Alarm annunciators are used simply to report exception conditions but they do not offer or support specific (recovery) action invocation by the decision authority.  
        /// As such Alarm annunciators cannot be used with speicific (recovery) actions.  
        /// Please <see cref="IANCondition"/> for use pattern interface that is intended to be used with this ANType.
        /// </summary>
        [EnumMember]
        Alarm = E005.ALCD.E041_Alarm,

        /// <summary>
        /// Interpretaion of this annunciator type is state and context dependant, especially in relation to when this annunciator is passed to a host.  May or may not have a known ALID.
        /// Internally this annunciator is generally treated as an Attention type.
        /// </summary>
        [EnumMember]
        Dynamic = E005.ALCD.E041_Dynamic,
    }

    /// <summary>
    /// This enum defines the signaling state produced by the annunciator source.  
    /// This state includes the effects of operations that the source can perform including 
    /// Clear, Post, NoteActionStarted, NoteActionCompleted, NoteActionFailed, and NoteActionAborted, 
    /// <para/>Off (0), On, OnAndWaiting, OnAndActionActive, OnAndActionCompleted, OnAndActionFailed, OnAndActionAborted
	/// </summary>
    [DataContract(Namespace = MosaicLib.Constants.SemiNameSpace)]
    public enum ANSignalState : int 
	{
        /// <summary>The annunciator is not signaling (default value).</summary>
        /// <remarks>Normal transitions: -> On (Post), -> OnAndWaiting (Post)</remarks>
        [EnumMember]
        Off = 0,

        /// <summary>The annunciator is posted with no enabled actions.</summary>
        /// <remarks>Normal transitions: -> OnAndWaiting (Post), -> Off (Clear)</remarks>
        [EnumMember]
        On,

        /// <summary>The annunciator is posted with one or more actions enabled.  An action may or not have been selected.</summary>
        /// <remarks>Normal transitions: -> OnAndActionActive (NoteActionStarted, NoteActionCompleted, NoteActionFailed), -> On (Post), -> Off (Clear)</remarks>
        [EnumMember]
        OnAndWaiting,

        /// <summary>A selected action has been accepted and started.</summary>
        /// <remarks>Normal transitions: -> OnAndActionComplete (NoteActionCompleted), -> OnAndActionFailed (NoteActionFailed), -> OnAndActionAborted (NoteActionAborted)</remarks>
        [EnumMember]
        OnAndActionActive,

        /// <summary>A selected action is complete.</summary>
        /// <remarks>Normal transitions: -> Off (Clear), -> On (Post), -> OnAndWaiting (Post) </remarks>
        [EnumMember]
        OnAndActionCompleted,

        /// <summary>A selected action failed.</summary>
        /// <remarks>Normal transitions: -> Off (Clear), -> On (Post), -> OnAndWaiting (Post) </remarks>
        [EnumMember]
        OnAndActionFailed,

        /// <summary>A selected action was aborted.</summary>
        /// <remarks>Normal transitions: -> Off (Clear), -> On (Post), -> OnAndWaiting (Post) </remarks>
        [EnumMember]
        OnAndActionAborted,
	}

    /// <summary>
    /// This is a hybrd enum.  Some of its valid values have well defined enumerations, while most of its valid values result from casting other Int32 values to this type.
    /// <para/>None (0), Lookup (-1), OptLookup(-2)
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.SemiNameSpace)]
    public enum ANAlarmID : int
    {
        /// <summary>Use this value when no AlarmID is known or desired [0]</summary>
        [EnumMember]
        None = 0,

        /// <summary>Use a value between 1 and this value for known AlarmIDs [Int32.MaxValue]</summary>
        [EnumMember]
        MaxValue = Int32.MaxValue,

        /// <summary>Use this value as a specification placeholder to indicate that the ALID needs to be looked up from the name of the annunciato [-1]r</summary>
        [EnumMember]
        Lookup = -1,

        /// <summary>Use this value as a specification placeholder to indicate that the ALID can be looked up from the name of the annunciator but that no error should be produced if no matching ALID is found. [-2]</summary>
        [EnumMember]
        OptLookup = -2,
    }

    /// <summary>
    /// Defines the current state of the ALID lookup process for a given IANSource
    /// <para/>None (0), Defined (1), Found (2), NotFound (3), Pending (4)
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.SemiNameSpace)]
    public enum ALIDLookupState : int
    {
        /// <summary>Client defined the ALID as ANAlarmID.None</summary>
        [EnumMember]
        None = 0,

        /// <summary>Client defined the ALID as an explicit value</summary>
        [EnumMember]
        Defined = 1,

        /// <summary>IANManager has found an ALID to use with this source</summary>
        [EnumMember]
        Found = 2,

        /// <summary>IANManager did not find an ALID to use with this source</summary>
        [EnumMember]
        NotFound = 3,

        /// <summary>Client requested ALID lookup but the IANManager has not been given a non-null IE30ALIDHandlerFacet to use.</summary>
        [EnumMember]
        Pending = 4,
    }

    #endregion

    #region ExtensionMethods

    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given alid value is one of the special values that indicates a lookup is needed.</summary>
        public static bool IsLookup(this ANAlarmID alid) 
        {
            return (alid == ANAlarmID.Lookup || alid == ANAlarmID.OptLookup);
        }

        /// <summary>Returns true if the given alid value is ANAlarmID.None</summary>
        public static bool IsNone(this ANAlarmID alid)
        {
            return (alid == ANAlarmID.None);
        }

        /// <summary>Returns true if the given alid value has been explicitly provided by the client (i.e. it is neither None, Lookup, nor OptLookup)</summary>
        public static bool IsDefined(this ANAlarmID alid)
        {
            return (!alid.IsLookup() && !alid.IsNone());
        }

        /// <summary>
        /// Returns true if the given signalState value indicates that the annunciator is signaling (it has been Posted and has not been Cleared)
        /// <para/>aka the signal state is not Off
        /// </summary>
        public static bool IsSignaling(this ANSignalState anSignalState)
        {
            return (anSignalState != ANSignalState.Off);
        }

        /// <summary>Returns true if the given signalState value indicates that the annunciator is performing an action</summary>
        public static bool IsActionActive(this ANSignalState anSignalState)
        {
            return (anSignalState == ANSignalState.OnAndActionActive);
        }

        /// <summary>Returns true if any NamedValue in the given actionList has true for its value</summary>
        public static bool IsAnyActionEnabled(this INamedValueSet actionList)
        {
            return actionList.Any((nv) => (nv.GetActionDisableReason() == string.Empty));
        }

        /// <summary>
        /// Returns returns an empty string if the given actionEnableNV is valid and it either contains the empty string or a value that is castable to boolean true.
        /// Returns a disable reason, either as the non-empty string contained in the given actionEnableNV's value or a generated description that indicates why the
        /// actionEnableNV's value indicates that the action is not enabled.
        /// </summary>
        public static string GetActionDisableReason(this INamedValue actionEnableNV)
        {
            if (actionEnableNV.IsNullOrEmpty())
                return "Given NV object:'{0}' is not a valid action enable".CheckedFormat(actionEnableNV);

            ValueContainer actionEnableVC = actionEnableNV.VC;

            switch (actionEnableVC.cvt)
            {
                case ContainerStorageType.String: return actionEnableVC.GetValue<string>(false) ?? "ActionDisableReasonIsNull";
                case ContainerStorageType.IListOfString: return ((actionEnableVC.GetValue<string []>(false).SafeCount() > 0) ? actionEnableVC.ToStringSML() : string.Empty);
                default: return actionEnableVC.GetValue<bool>(false) ? String.Empty : "Action is not currently enabled {0}".CheckedFormat(actionEnableVC);
            }
        }
    }

    #endregion

    #region IANSpec, ANSpec

    /// <summary>Interface to an Annunciator Spec object (this interface is read only)</summary>
    public interface IANSpec : IEquatable<IANSpec>
    {
        /// <summary>Gives the Name of the Annunciator</summary>
        string ANName { get; }

        /// <summary>Gives a brief, optional, comment about the Annunciator as seen by its source.  This is not generally intended to be used as a formal definition or description.</summary>
        string Comment { get; }

        /// <summary>Gives the ANType of this Annunciator</summary>
        ANType ANType { get; }

        /// <summary>This is now a proxy for the ANManagerTableRegistrationNum property.</summary>
        [Obsolete("This property is being deprectated as its prior implementation is not easily unit testable.  Please change to the use of the ANManagerTableRegistrationNum value instead (2018-11-03)")]
        Int32 SpecID { get; }

        /// <summary>When non-zero this indicates the position of the corresponding ANSource in the managers table of registered sources.</summary>
        int ANManagerTableRegistrationNum { get; }

        /// <summary>Carries information about this annunciator that was provided by the client when the registering the corresponding ANSource.</summary>
        INamedValueSet MetaData { get; }

        /// <summary>
        /// Defaults to ALID_None.  When explicitly set to a possitive integer value this defines the ALID that the client would like to use with this annunciator.  
        /// When set to ALID_Lookup or ALID_OptLookup this requests that the IANManager attempt to find the ALID from the (possibly later) provided IE30ALIDHandlerFacet.  
        /// For ALID_Lookup the IANManager will emit an error message if no ALID was found for this ANName after the IE30ALIDHandlerFacet has been provided.
        /// </summary>
        ANAlarmID ALID { get; }

        /// <summary>Returns true if this object's ALID is any value other than ALID_None, ALID_Lookup, or ALID_OptLookup</summary>
        bool HasALID { get; }
    }

    /// <summary>This is the storage implementation and serialization object for the IANSpec interface.</summary>
    [DataContract(Namespace = MosaicLib.Constants.SemiNameSpace), Serializable]
	public class ANSpec : IANSpec
	{
        /// <summary>Gives the Name of the Annunciator</summary>
        [DataMember(Order = 10)]
        public string ANName { get; set; }

        /// <summary>Gives a brief description of the Annunciator</summary>
        [DataMember(Order = 20, IsRequired=false, EmitDefaultValue=false)]
        public string Comment { get; set; }

        /// <summary>Gives the ANType of this Annunciator</summary>
        [DataMember(Order = 40)]
        public ANType ANType { get; set; }

        /// <summary>This is now a proxy for the ANManagerTableRegistrationNum property.</summary>
        public Int32 SpecID { get { return ANManagerTableRegistrationNum; } }

        /// <summary>When non-zero this indicates the position of the corresponding ANSource in the managers table of registered sources.</summary>
        [DataMember(Order = 60, Name="RegNum", IsRequired = false, EmitDefaultValue = false)]
        public int ANManagerTableRegistrationNum { get; internal set; }

        /// <summary>
        /// Defaults to ALID_None.  When explicitly set to a possitive integer value this defines the ALID that the client would like to use with this annunciator.  
        /// When set to ALID_Lookup or ALID_OptLookup this requests that the IANManager attempt to find the ALID from the (possibly later) provided IE30ALIDHandlerFacet.  
        /// For ALID_Lookup the IANManager will emit an error message if no ALID was found for this ANName after the IE30ALIDHandlerFacet has been provided.
        /// </summary>
        [DataMember(Order = 70, IsRequired = false, EmitDefaultValue = false)]
        public ANAlarmID ALID { get; set; }

        /// <summary>Returns true if this object's ALID was explicitly defined by the client (i.e. it is neither None, Lookup, nor OptLookup</summary>
        public bool HasALID { get { return ALID.IsDefined(); } }

        /// <summary>Carries information about this annunciator that was provided by the client when the registering the corresponding ANSource.</summary>
        public INamedValueSet MetaData { get { return _metaData.MapNullToEmpty(); } set { _metaData = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        [DataMember(Order = 80, Name = "MetaData", IsRequired = false, EmitDefaultValue = false)]
        private NamedValueSet _metaData;

        /// <summary>Default constructor</summary>
        public ANSpec() { }

        /// <summary>Copy constructor</summary>
        public ANSpec(IANSpec other)
            : this()
        {
            ANName = other.ANName;
            Comment = other.Comment;
            ANType = other.ANType;
            ANManagerTableRegistrationNum = other.ANManagerTableRegistrationNum;
            ALID = other.ALID;
            MetaData = other.MetaData;
        }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            string mdStr = (_metaData != null ? " MetaData:{0}".CheckedFormat(_metaData.SafeToStringSML()) : "");

            if (ANManagerTableRegistrationNum != 0)
                return "ANSpec: {0} {1} RegNum:{2} ALID:{3}{4}".CheckedFormat(ANName, ANType, ANManagerTableRegistrationNum, ALID, mdStr);
            else
                return "ANSpec: {0} {1} ALID:{2}{3}".CheckedFormat(ANName, ANType, ALID, mdStr);
        }

        /// <summary>
        /// Returns true if the given other is non-null and its contents match the contents of this object.
        /// </summary>
        public bool Equals(IANSpec other)
        {
            return (other != null
                    && ANName == other.ANName
                    && Comment == other.Comment
                    && ANType == other.ANType
                    && ANManagerTableRegistrationNum == other.ANManagerTableRegistrationNum
                    && ALID == other.ALID
                    && MetaData.IsEqualTo(other.MetaData, compareReadOnly: false)
                    );
        }

        /// <summary>
        /// overrides object.Equals (for unit tests, et. al.).  Maps to use corresponding IEquatable interface method.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is IANSpec)
                return Equals(obj as IANSpec);
            else
                return false;
        }

        /// <summary>
        /// provided to avoid build warning
        /// </summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    #endregion

    #region IANState, ANState

    /// <summary>The interface to object(s) that can represent the current state of an Annunciator.</summary>
    public interface IANState : IEquatable<IANState>
    {
        /// <summary>Gives the ANSpec for the annunciator that produced this state.</summary>
        IANSpec ANSpec { get; }

        /// <summary>Gives the annunciator name (from ANSpec, or as explicitly assigned) of the annunciator that produced this state.</summary>
        string ANName { get; }

        /// <summary>This gives he ANSignalState value that the annunciator currently has.</summary>
        ANSignalState ANSignalState { get; }

        /// <summary>
        /// Returns true if the curent ANSignalState value IsSignaling
        /// <para/>(aka the signal state is not Off)
        /// </summary>
        bool IsSignaling { get; }

        /// <summary>Gives the most recently given reason for the current state or condition</summary>
        string Reason { get; }

        /// <summary>Gives the ANSeqAndTimeInfo for the last change to this state</summary>
        ANSeqAndTimeInfo SeqAndTimeInfo { get; }

        /// <summary>Gives the ANSeqAndTimeInfo for the last change to this state from non-signaling to signaling (from Off to any of the On states)s</summary>
        ANSeqAndTimeInfo LastTransitionToOnSeqAndTimeInfo { get; }

        /// <summary>This gives the QpcTimeStamp recorded at the time that this state object was generated (or last updated) - as found in SeqAndTimeInfo</summary>
        QpcTimeStamp TimeStamp { get; }

        /// <summary>This gives the DateTime recorded at the time that this state object was generated (or last updated) - as found in SeqAndTimeInfo</summary>
        DateTime DateTime { get; }

        /// <summary>This ReadOnly INamedValueSet contains a set of action names that the annunciator currently supports.  Each corresponding value shall be set to True to indicate that the corresponding name is currently available.</summary>
        INamedValueSet ActionList { get; }

        /// <summary>When non-empty this gives the name of the action that has been selected by an external decision authority.</summary>
        string SelectedActionName { get; }

        /// <summary>When non-empty this gives the name of the action that has been accepted by the annunciator source and which it is currently processing.</summary>
        string ActiveActionName { get; }

        /// <summary>When true this indicates that the external decision authority has requested that the SelectedActionName be aborted.</summary>
        bool ActionAbortRequested { get; }

        /// <summary>Gives the ALID associated with this Annunciator (for looked up ANAlarmID values, this value may differ from the value in the ANSpec)</summary>
        ANAlarmID ALID { get; }

        /// <summary>Gives the state of any Lookup operation that is required or is in progress for this AN source.</summary>
        ALIDLookupState ALIDLookupState { get; }

        /// <summary>This property allows the ANSource to record and propagate arbitrary values to consumers of ANState data such as the GUI, the decision authority, and the host interface, so as to support client specific customizations of how Annunciators are handled and connected to other entities.</summary>
        INamedValueSet NamedValues { get; }

        /// <summary>Returns true if this IANState has the same contents as the given <paramref name="other"/></summary>
        bool IsEqualTo(IANState other, bool compareTimeStamp = true, bool compareDateTime = true);
    }

    /// <summary>
    /// This struct is used to contain and serialize a triplet of a sequence number, a QpcTimeStamp and a DateTime.  It is used to record and propagate timeing and sequence information for ANState objects.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.SemiNameSpace), Serializable]
    public struct ANSeqAndTimeInfo : IEquatable<ANSeqAndTimeInfo>
    {
        /// <summary>This gives the sequence number as recorded by the ANManager's at the time that this state object was generated (or last updated)</summary>
        [DataMember(Order = 100)]
        public ulong SeqNum { get; set; }

        /// <summary>This gives the QpcTimeStamp recorded at the time that this state object was generated (or last updated)</summary>
        public QpcTimeStamp TimeStamp { get; set; }

        /// <summary>This gives the Time from the QpcTimeStamp recorded at the time that this state object was generated (or last updated) - used to support serialization</summary>
        [DataMember(Order = 200, Name = "TimeStamp")]
        private double TimeStampDouble { get { return TimeStamp.Time; } set { TimeStamp = new QpcTimeStamp() { Time = value }; } }

        /// <summary>This gives the DateTime recorded at the time that this state object was generated (or last updated)</summary>
        [DataMember(Order = 300)]
        public DateTime DateTime { get; set; }

        /// <summary>Returns true if this ANSeqAndTimeInfo has the same contents as the given <paramref name="other"/> ANSeqAndTimeInfo</summary>
        public bool Equals(ANSeqAndTimeInfo other)
        {
            return IsEqualTo(other);
        }

        /// <summary>
        /// Returns true if this ANSeqAndTimeInfo has the same contents as the given <paramref name="other"/> ANSeqAndTimeInfo.  
        /// Set <paramref name="compareTimeStamp"/> to false to prevent comparing the actual TimeStamp values.  
        /// Set <paramref name="compareDateTime"/> to false to prevent comparing the actual DateTime values.
        /// </summary>
        public bool IsEqualTo(ANSeqAndTimeInfo other, bool compareTimeStamp = true, bool compareDateTime = true)
        {
            return (SeqNum == other.SeqNum
                    && (TimeStamp == other.TimeStamp || !compareTimeStamp)
                    && (DateTime == other.DateTime || !compareDateTime));
        }

        /// <summary>
        /// Copy constructor used to assign sequence numbers
        /// </summary>
        internal ANSeqAndTimeInfo(ANSeqAndTimeInfo other, ref ulong seqNumSourceRef)
            : this()
        {
            SeqNum = ++seqNumSourceRef;
            TimeStamp = other.TimeStamp;
            DateTime = other.DateTime;
        }
    }

    /// <summary>This is the storage implementation and serialization object for the IANState interface.</summary>
    [DataContract(Namespace = MosaicLib.Constants.SemiNameSpace), Serializable]
    [KnownType(typeof(ANSpec))]
    public class ANState : IANState
	{
        /// <summary>Default constructor</summary>
        public ANState() {}

        /// <summary>Copy constructor</summary>
        public ANState(IANState other)
        {
            ANState rhsAsANState = other as ANState;
            bool rhsIsANState = (rhsAsANState != null);

            ANSpec = other.ANSpec;
            ANSignalState = other.ANSignalState;
            Reason = other.Reason;
            SeqAndTimeInfo = other.SeqAndTimeInfo;
            LastTransitionToOnSeqAndTimeInfo = other.LastTransitionToOnSeqAndTimeInfo;
            _actionList = (rhsIsANState ? rhsAsANState._actionList : other.ActionList);
            selectedActionName = (rhsIsANState ? rhsAsANState.selectedActionName : other.SelectedActionName); ;
            activeActionName = (rhsIsANState ? rhsAsANState.activeActionName : other.ActiveActionName); ;
            ActionAbortRequested = other.ActionAbortRequested;
            ALID = other.ALID;
            ALIDLookupState = other.ALIDLookupState;
            NamedValues = other.NamedValues;
        }

        /// <summary>Gives the ANSpec for the annunciator that produced this state.</summary>
        [DataMember(Order = 100)]
        public IANSpec ANSpec { get; set; }

        /// <summary>Gives the annunciator name (from ANSpec) of the annunciator that produced this state.</summary>
        public string ANName { get { return ANSpec.ANName; } set { } }

        /// <summary>This gives he ANSignalState value that the annunciator currently has.</summary>
        [DataMember(Order = 300)]
        public ANSignalState ANSignalState { get { return anSignalState; } set { anSignalState = value; } }
        private volatile ANSignalState anSignalState;

        /// <summary>
        /// Returns true if the curent ANSignalState value IsSignaling
        /// <para/>(aka the signal state is not Off)
        /// </summary>
        public bool IsSignaling { get { return anSignalState.IsSignaling(); } }

        /// <summary>Gives the most recently given reason for the current state or condition</summary>
        public string Reason { get { return _reason.MapNullToEmpty(); } set { _reason = value.MapEmptyToNull(); } }

        [DataMember(Order = 400, Name = "Reason", EmitDefaultValue=false, IsRequired=false)]
        private string _reason = null;

        /// <summary>Gives the ANSeqAndTimeInfo for the last change to this state</summary>
        [DataMember(Order = 500)]
        public ANSeqAndTimeInfo SeqAndTimeInfo { get; set; }

        /// <summary>Gives the ANSeqAndTimeInfo for the last change to this state from non-signaling to signaling (from Off to any of the On states)s</summary>
        [DataMember(Order = 600)]
        public ANSeqAndTimeInfo LastTransitionToOnSeqAndTimeInfo { get; set; }

        /// <summary>This gives the QpcTimeStamp recorded at the time that this state object was generated (or last updated)</summary>
        public QpcTimeStamp TimeStamp { get { return SeqAndTimeInfo.TimeStamp; } }

        /// <summary>This gives the DateTime recorded at the time that this state object was generated (or last updated)</summary>
        public DateTime DateTime { get { return SeqAndTimeInfo.DateTime; } }

        /// <summary>This ReadOnly INamedValueSet contains a set of action names that the annunciator currently supports.  Each corresponding value shall be set to True (or null or empty string) to indicate that the corresponding action name is currently available.</summary>
        public INamedValueSet ActionList { get { return _actionList.MapNullToEmpty(); } set { _actionList = value.ConvertToReadOnly(); } }
        private INamedValueSet _actionList = null;

        /// <summary>Private DC serialization helper.</summary>
        [DataMember(Order = 700, Name = "ActionList", IsRequired = false, EmitDefaultValue = false)]
        private NamedValueSet ActionListDC { get { return _actionList.ConvertToReadOnly(mapNullToEmpty: false).MapEmptyToNull(); } set { _actionList = value.ConvertToReadOnly(mapNullToEmpty: false); } }

        /// <summary>When non-empty this gives the name of the action that has been selected by an external decision authority.  This may only be set when the action is OnAndWaiting, and has no currently SelectedActionName.</summary>
        [DataMember(Order = 800)]
        public string SelectedActionName { get { return selectedActionName ?? String.Empty; } set { selectedActionName = value; } }
        private volatile string selectedActionName = null;

        /// <summary>When non-empty this gives the name of the action that has been accepted by the annunciator source and which it is currently processing.</summary>
        [DataMember(Order = 900)]
        public string ActiveActionName { get { return activeActionName ?? String.Empty; } set { activeActionName = value; } }
        private string activeActionName = null;

        /// <summary>When true this indicates that the external decision authority has requested that the SelectedActionName be aborted.</summary>
        [DataMember(Order = 1000)]
        public bool ActionAbortRequested { get { return actionAbortRequested; } set { actionAbortRequested = value; } }
        private volatile bool actionAbortRequested = false;

        /// <summary>Gives the ALID associated with this Annunciator (for looked up ANAlarmID values, this value may differ from the value in the ANSpec)</summary>
        [DataMember(Order = 1100)]
        public ANAlarmID ALID { get; set; }

        /// <summary>Gives the state of any Lookup operation that is required or is in progress for this AN source.</summary>
        [DataMember(Order = 1200)]
        public ALIDLookupState ALIDLookupState { get; set; }

        /// <summary>This property allows the ANSource to record and propagate arbitrary values to consumers of ANState data such as the GUI, the decision authority, and the host interface, so as to support client specific customizations of how Annunciators are handled and connected to other entities.</summary>
        public INamedValueSet NamedValues { get { return _namedValues.MapNullToEmpty(); } set { _namedValues = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        [DataMember(Order = 1300, Name = "NamedValues", EmitDefaultValue = false, IsRequired = false)]
        private NamedValueSet _namedValues;

        /// <summary>Returns true if this ANState has the same contents as the given <paramref name="other"/> IANState</summary>
        public bool Equals(IANState other)
        {
            return IsEqualTo(other);
        }

        /// <summary>
        /// Returns true if this ANState has the same contents as the given <paramref name="other"/> IANState.
        /// Set <paramref name="compareTimeStamp"/> to false to prevent comparing the actual SeqAndTimeInfo.TimeStamp values.  
        /// Set <paramref name="compareDateTime"/> to false to prevent comparing the actual SeqAndTimeInfo.DateTime values.
        /// </summary>
        public bool IsEqualTo(IANState other, bool compareTimeStamp = true, bool compareDateTime = true)
        {
            return (other != null
                    && ANSpec.Equals(other.ANSpec)
                    && ANName == other.ANName
                    && ANSignalState == other.ANSignalState
                    && Reason == other.Reason
                    && SeqAndTimeInfo.IsEqualTo(other.SeqAndTimeInfo, compareTimeStamp: compareTimeStamp, compareDateTime: compareDateTime)
                    && LastTransitionToOnSeqAndTimeInfo.IsEqualTo(other.LastTransitionToOnSeqAndTimeInfo, compareTimeStamp: compareTimeStamp, compareDateTime: compareDateTime)
                    && ActionList.IsEqualTo(other.ActionList, compareReadOnly: false)
                    && SelectedActionName == other.SelectedActionName
                    && ActiveActionName == other.ActiveActionName
                    && ActionAbortRequested == other.ActionAbortRequested
                    && ALID == other.ALID
                    && ALIDLookupState == other.ALIDLookupState
                    && NamedValues.IsEqualTo(other.NamedValues, compareReadOnly: false)
                    );
        }

        /// <summary>Debugging and logging helper method</summary>
        public override string ToString()
        {
            string anSpecStr = (ANSpec != null ? ANSpec.ToString() : "NoANSpec");
            string nvStr = (_namedValues != null ? " NamedValues:{0}".CheckedFormat(_namedValues.SafeToStringSML()) : "");

            return "ANState {0} {1} {2} sel:{3}{4} alid:{5} reason:'{6}'{7}".CheckedFormat(anSpecStr, ANSignalState, ActionList.ToString(false, true), SelectedActionName.MapNullOrEmptyTo("[None]"), ActionAbortRequested ? " AbortReq" : string.Empty, ALID, Reason, nvStr);
        }

        /// <summary>
        /// overrides object.Equals (for unit tests, et. al.).  Maps to use corresponding IEquatable interface method.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is IANState)
                return IsEqualTo(obj as IANState);
            else
                return false;
        }

        /// <summary>
        /// provided to avoid build warning
        /// </summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    #endregion

    #region IANSource and realted types: IANSourceBase, IANAcknowledgeableSourceBase, IANOccurrence, IANCondition

    /// <summary>
    /// This gives the public interface that defines the basic API that is common to all IANSource types.
    /// </summary>
    public interface IANSourceBase
    {
        /// <summary>Gets a copy of the originally registered ANSpec</summary>
        IANSpec ANSpec { get; }

        /// <summary>Gets access to the most recently generated ANState for this annunciator.  Normally only the source causes this to change but selection of an action and requesting an abort are performed externally and asynchronously.</summary>
        IANState ANState { get; }

        /// <summary>Gives the client access to the state publisher object that the manager creates in order to publish anState chagnes after it has processed them.</summary>
        ISequencedObjectSource<IANState, Int32> ANStatePublisher { get; }

        /// <summary>This method is used to wait until the ANManagerPart has processed and delivered all side effects that relate to this ANSource</summary>
        void Sync(SyncFlags flags = default(SyncFlags));

        /// <summary>Allows the caller to replace (or merge) the Anunciator's last published NamedValues with the given <paramref name="nvs"/> contents.</summary>
        void SetNamedValues(INamedValueSet nvs, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.Replace);
    }

    /// <summary>
    /// Derived from IANSourceBase.  Adds the Acknowledge action factory method.  This interface is the base interface for the IANOccurrence and IANCondition interface types.
    /// </summary>
    public interface IANAcknowledgeableSourceBase : IANSourceBase
    {
        /// <summary>
        /// Action factory method.  When the returned action is run it will attempt to select and apply the Acknowledge recovery action to this annunciator.  
        /// If the ANSignalState is OnAndWaiting then it will be set to Off by running this action as if the decision authority had directly selected this recovery action.
        /// </summary>
        /// <param name="onlyIfOnAndWaiting">
        /// Pass this parameter as true to select that the action shall only attempt to acknowledge the annunciator if it is already in an OnAndWaiting state (so as to avoid producing error messages).  
        /// If this parameter is not selected then the action will fail normally when the annunciator is not already in the appropriate state.  Defaults to false.
        /// </param>
        /// <remarks>
        /// Use of this action allows the annunciator source to directly clear the visibility state of these types of annunciators.  
        /// This pattern is typically done when an existing behavior in the consolidated cleanup code in the annunciator source part is tasked with clearing the visibility state of all annunciators for which it is responsible.
        /// </remarks>
        IClientFacet Acknowledge(bool onlyIfOnAndWaiting = false);
    }

    /// <summary>
    /// One of the current Annunciator use model interfaces.  
    /// This one gives the public interface that a client uses to make use of an Occurrence type annunctiator which only permits
    /// the client to signal the occurrence of some condition that the annunciator announces.  
    /// The client is not involved in determining when the annunciator's signaling state is cleared (this is done using Acknowledge action handled by ANManagerPart).
    /// This use model interface is suitable for use with all ANTypes except the Alarm type.
    /// </summary>
    public interface IANOccurrence : IANAcknowledgeableSourceBase
    {
        /// <summary>Signals the Occurance of this annunciator.  Posts and immediately clears it.  Acknowledgement is handled internally by the Annunciator Manager.</summary>
        void SignalOccurrence(string reason);
    }

    /// <summary>
    /// One of the current Annunciator use model interfaces.  
    /// This one gives the public interface that a client typically uses to make use of an Condition type annunciator which only permits
    /// the client to Set and Clear the annunciator condition.  The Service method is simply a proxy for calling Set and Clear based on the given conditionState and the current signaling state.
    /// As with the IANOccurrence use model interface, the client is not directly involved in determining when the annunciator's signaling state is cleared (this is done using Acknowledge action handled by ANManagerPart).
    /// This use model interface is only suitable for use with any ANType except Error.
    /// </summary>
    public interface IANCondition : IANAcknowledgeableSourceBase
    {
        /// <summary>
        /// Used by the client to indicate that this ANAlarm is asserting its condition with the given <paramref name="reason"/> explaning why it is being asserted.
        /// The client may call this method occasionally to update the reason if the underlying reason for the condition may change over time before it has been resolved.
        /// It is not recomended to call this method repeatedly with different reasons as high rate use of this method may cause some reported reasons to be discarded/filtereted out.
        /// NOTE: The AcceptAlarmReasonChangeAfterTimeSpan configuration value can be used to control the the throttleing of any such frequence reason changes.
        /// </summary>
        void Set(string reason);

        /// <summary>
        /// Used by the client to indicate that this ANAlarm is no longer asserting its condition along with the <paramref name="reason"/> that the source believes explains how the condition was resolved.
        /// Use of this method will only produce a single state change for this source and repeated use of this method will be ignored once the ConditionState is already false.
        /// This method will cause the annunciator to enable its Acknoweldge action and thus allow the signaling state to be cleared using that action.  
        /// Acknowledge action requests are processed by the ANManagerPart without direct use of this interface.
        /// </summary>
        void Clear(string reason);

        /// <summary>
        /// This method dispatches between calling Set(<paramref name="reason"/>) and Clear(<paramref name="reason"/>) based on the given <paramref name="conditionState"/> value.
        /// It is not recomended to call this method repeatedly with the <paramref name="conditionState"/> true and with different <paramref name="reason"/> values as high rate use of this method may cause some reported reasons to be discarded/filtereted out.
        /// NOTE: The AcceptAlarmReasonChangeAfterTimeSpan configuration value can be used to control the the throttleing of any such frequence reason changes while the <paramref name="conditionState"/> is true.
        /// </summary>
        void Service(bool conditionState, string reason);

        /// <summary>Returns true if the Set method was used last and false if the Clear method was used last.  Equivalent to (ANState.ANSignalState == ANSignalState.On)</summary>
        bool ConditionState { get; }

        /// <summary>
        /// Get/Set property gives client access get and change this IANCondition's selected behavior.
        /// </summary>
        IANConditionBehavior Behavior { get; set; }
    }

    /// <summary>
    /// This flag enumeration is used to adjust the behavior of a given IANCondition annunciator source instance.
    /// <para/>None (0x00, UseReasonForActionDenyReason (0x01)
    /// </summary>
    [Flags]
    public enum IANConditionBehavior : int
    {
        /// <summary>Default placeholder value.  [0x00]</summary>
        None = 0x00,

        /// <summary>
        /// When this behavior is selected the condition annunciator's Set and Service(true) methods will set the Acknowledge deny reason to the reason given to the Set or Service call.
        /// This allows the client to specify the Acknowledge action's disabled state tooltip to be the same as the string reason that is given when indicating that the annunciator (fault) condition is active.
        /// </summary>
        UseActionDenyReason = 0x01,
    }

    /// <summary>
    /// The most general of the Annunciator use model interfaces.  
    /// This gives the public interface that a client uses to make direct use of the full set of API methods for an annunciator.
    /// This use model interface is suitable for use with all ANTypes except for the Alarm type.
    /// </summary>
    public interface IANSource : IANSourceBase
    {
        /// <summary>IBasicNotificationList which is notified when an action is selected on an annunciator or when an action abort is requested.</summary>
        IBasicNotificationList NotifyOnActionSelectedOrAbortedList { get; }

        /// <summary>Sets the signal state to be non-signaling.</summary>
        void Clear(string reason);

        /// <summary>Sets the signal state to be signaling and possibly waiting (if any of the actionList items are enabled)</summary>
        void Post(INamedValueSet actionList, string reason);

        /// <summary>Notes that the given action has been started</summary>
        void NoteActionStarted(string action, string reason);

        /// <summary>Notes that the current active action succeeded.  Normally the client Clears the annunciator immediately after this.</summary>
        void NoteActionCompleted(string reason);

        /// <summary>Notes that the current active action was aborted.  Normally the client Posts the annunciator immediately after this to update the set of available Actions.</summary>
        void NoteActionAborted(string reason);

        /// <summary>Notes that the current active action failed and indicates why.  Normally the client Posts the annunciator immediately after this to update the set of available Actions.</summary>
        void NoteActionFailed(string reason);
    }

    #endregion

    #region ANManagerPart

    /// <summary>
    /// This interface defines the publicly available methods that are provided by the ANManagerPart.  
    /// This includes registration and creation of AN Source objects (IANSource, IANOccurrence and IANCondition) that may be used by clients.
    /// It also includes means to obtain a sequenced IANState publisher for given ANNames and includes means to create SetSelectedActionName and RequestAbortAction service methods.
    /// </summary>
    public interface IANManagerPart : IActivePartBase
    {
        /// <summary>
        /// Requests that the manager create a new IANSource annunciator source for the given anSpec.  
        /// The given anSpec.Name must be unique (IE it cannot have already been registered) and the anSpec.ANType must not be the Alarm type.
        /// Returns the the requested IANSource on success or null if the registration failed.
        /// <para/>Note that the <paramref name="sourceObjectID"/> is only provided (and retained by the manager) to improve the quality of error log messages in cases where two clients attempt to register the same ANName.  This parameter is not otherwise used.
        /// </summary>
        /// <exception cref="ANRegistrationException">thrown with appropriate message if given anSpec is not valid or if the given ANName has already been registered</exception>
        /// <remarks>As with CreateGoOnlineAction(bool andInitialize), this method also starts the target part if it has not already been started</remarks>
        IANSource RegisterANSource(string sourceObjectID, IANSpec anSpec);

        /// <summary>
        /// Requests that the manager create a new IANOccurrence type annunciator source for the given anSpec.  
        /// The given anSpec.Name must be unique (IE it cannot have already been registered) and the given anSpec.ANType must not be the Alarm type.
        /// Returns the requested IANOccurrence for the annunciator on success or null if the registration failed.
        /// <para/>Note that the <paramref name="sourceObjectID"/> is only provided (and retained by the manager) to improve the quality of error log messages in cases where two clients attempt to register the same ANName.  This parameter is not otherwise used.
        /// </summary>
        /// <exception cref="ANRegistrationException">thrown with appropriate message if given anSpec is not valid for this type of source or if the given ANName has already been registered</exception>
        /// <remarks>As with CreateGoOnlineAction(bool andInitialize), this method also starts the target part if it has not already been started</remarks>
        IANOccurrence RegisterANOccurrenceSource(string sourceObjectID, IANSpec anSpec);

        /// <summary>
        /// Requests that the manager create a new IANCondition type annunciator source for the given anSpec.  
        /// The given anSpec.Name must be unique (IE it cannot have already been registered) and the given anSpec.ANType must not be the Error type.
        /// Returns the requested IANCondition for the annunciator on success or null if the registration failed.
        /// <para/>Note that the <paramref name="sourceObjectID"/> is only provided (and retained by the manager) to improve the quality of error log messages in cases where two clients attempt to register the same ANName.  This parameter is not otherwise used.
        /// </summary>
        /// <exception cref="ANRegistrationException">thrown with appropriate message if given anSpec is not valid for this type of source or if the given ANName has already been registered</exception>
        /// <remarks>As with CreateGoOnlineAction(bool andInitialize), this method also starts the target part if it has not already been started</remarks>
        IANCondition RegisterANConditionSource(string sourceObjectID, IANSpec anSpec, IANConditionBehavior initialBehavior = default(IANConditionBehavior));

        /// <summary>
        /// Allows the caller to obtain an object source publisher that publishes IANState updates for the given annunciator name (anName).  
        /// May be used to obtain the publisher for a name before the name has actually been registered.
        /// When obtaining the publisher before the corresonding source has been registered, the returned publisher will remain in its initial, has no value, state 
        /// until the source has been registered and the manager has published its initial state.
        /// </summary>
        ISequencedObjectSource<IANState, Int32> GetANStatePublisher(string anName);

        /// <summary>
        /// Creates and returns an action that, when run, will indicate that the decision authority has requested that the given selectedActionName be performed for the indicated annunciator name.
        /// <para/>This is functionally equivalent to creating a ServiceAction called "SetSelectedActionName" with named string parameters "ANName" and "SelectedActionName"
        /// <para/>When <paramref name="onlyIfOnAndWaiting"/> is selected (defaults to false) then the action will be a no-op if the selected annunciator is not already in an OnAndWaiting signal state.  
        /// Selecting this option is equivalent to adding the "OnlyIfOnAndWaiting" keyword to the actions named param values.
        /// </summary>
        IStringParamAction CreateSetSelectedActionNameAction(string anName, string selectedActionName, bool onlyIfOnAndWaiting = false);

        /// <summary>
        /// Creates and returns an action that, when run, will indicate that the decision authority has requested that the annunciator source abort whatever action it is currently performing.
        /// <para/>This is functionally equivalent to creating a ServiceAction called "RequestActionAbort" with named string parameter "ANName"
        /// </summary>
        IStringParamAction CreateRequestActionAbortAction(string anName);

        /// <summary>Creates and returns an action that, when run, will clear the ANStateRecentlyClearedSet.  Equivalent to running a "ClearResentSet" service action.</summary>
        IStringParamAction CreateClearRecentSet();

        /// <summary>Creates and returns an action that, when run, will clear the ANStateHistorySet.  Equivalent to running a "ClearHistorySet" service action.</summary>
        IStringParamAction CreateClearHistorySet();

        /// <summary>
        /// Creates and returns an action that, when run, will attempt to select the given action name on all signaling annunciators that have such an action name enabled.
        /// <para/>This is functionally equivalent to creating a ServiceAction called "SelectActionNameForAll" with a named string parameter "SelectedActionName"
        /// </summary>
        IStringParamAction CreateSelectActionNameForAll(string selectedActionName);

        /// <summary>
        /// Action factory method.  When the returned action is run, it waits until the IANManagerPart has completed the requested synchronization steps and then the action completes.
        /// This action may be run concurrently with other actions and part behaviors.
        /// <para/>Use the <paramref name="flags"/> parameter to select the synchronization options that you would like it to wait to be synchronized with.
        /// </summary>
        IClientFacet Sync(SyncFlags flags = default(SyncFlags));
    }

    /// <summary>
    /// This enumeration is used with the Sync action to select which behaviors in the ANManagerPart should be performed.
    /// <para/>None (0x00), ANEventDelivery (0x01)
    /// </summary>
    [Flags]
    public enum SyncFlags : int
    {
        /// <summary>Placeholder default:  Waits for the IANManagerPart to complete the next internal update cycle.  Used for IANSource.Sync operations.  [0x00]</summary>
        None = 0x00,

        /// <summary>Waits for the IANManagerPart to complete the next ANEvent Delivery pass.  [0x01]</summary>
        ANEventDelivery = 0x01,
    }

    /// <summary>
    /// This interface defines how an AnnunciatorManagerPart can lookup and indicate change in state of an e30 ALID.
    /// The ANManagerPart instance that is in use must be explicitly constructed and assigned with a non-null IE30ALIDHandlerFacet in order for this functionality to be used.
    /// </summary>
    /// <remarks>The use of this interface will be marked Obsolete in the future and support for it will eventually be removed (2018-12=02).</remarks>
    public interface IE30ALIDHandlerFacet
    {
        /// <summary>
        /// Asks this E30ALIDHandler to get/create the ALID for the given ANSpec.
        /// <para/>Method returns non-zero integer value when given anSpec was found or zero when given anSpec was not found.
        /// </summary>
        int GetALIDFromANSpec(IANSpec anSpec);

        /// <summary>
        /// Tells the E30ALIDHandler that the state of a given ALID has been changed, indicates if the ALID is now active and gives the reason (message) that was associated with the change.
        /// </summary>
        void NoteALIDValueChanged(int ALID, bool isActive, string reason);
    }

    /// <summary>
    /// Instance configuration object for the ANManagerPart.
    /// </summary>
    public class ANManagerPartConfig
    {
        /// <summary>Constructor - requires a <paramref name="partID"/></summary>
        public ANManagerPartConfig(string partID)
        {
            PartID = partID;
        }

        /// <summary>
        /// Copy constructor.  This constructor is used to make copies of an <paramref name="other"/> instance, optionally replacing the partID with the given <paramref name="alternatePartID"/> if it is non-null.
        /// In addition if the <paramref name="other"/> instance's IVI, IConfig and/or ISI properties are null, then they are replaced with the corresponding default singleon values in the copied instance.
        /// </summary>
        public ANManagerPartConfig(ANManagerPartConfig other, string alternatePartID = null)
        {
            PartID = alternatePartID ?? other.PartID;

            IVI = other.IVI ?? Values.Instance;
            IConfig = other.IConfig ?? Modular.Config.Config.Instance;
            ISI = other.ISI ?? Modular.Interconnect.Sets.Sets.Instance;

            E30ALIDHandlerFacet = other.E30ALIDHandlerFacet;
            ALIDDictionary = other.ALIDDictionary;
            ANEventInfoListHandlerPart = other.ANEventInfoListHandlerPart;

            ActionLoggingConfig = other.ActionLoggingConfig ?? ActionLoggingConfig.Debug_Debug_Trace_Trace;
            ANEventHelperActionLoggingConfig = other.ANEventHelperActionLoggingConfig ?? other.ActionLoggingConfig ?? ActionLoggingConfig.Trace_Trace_Trace_Trace;
        }

        /// <summary>Gives the PartID that the constructed ANManagerPart will use</summary>
        public string PartID { get; private set; }

        /// <summary>Gives the IValuesInterconnection instance that the constructed part will use.  If null then the part will use the default singleton one.</summary>
        public IValuesInterconnection IVI { get; set; }

        /// <summary>Gives the IConfig instance that the constructed part will use.  If null then the part will use the default singleton one.</summary>
        public IConfig IConfig { get; set; }

        /// <summary>Gives the ISetsInterconnection instance that the constructed part will use.  If null then the part will use the default singleton one.</summary>
        public ISetsInterconnection ISI { get; set; }

        /// <summary>Gives the optional IE30ALIDHandlerFacet instance that the part will use.  Use of this property cannot be combined with use of the following ALIDDictionary property</summary>
        public IE30ALIDHandlerFacet E30ALIDHandlerFacet { get; set; }

        /// <summary>When non-null, this gives the string to int32 dictionary that the part can use to lookup ALID values for given annuciator names.</summary>
        public ReadOnlyIDictionary<string, int> ALIDDictionary { get; set; }

        /// <summary>
        /// When non-null, this gives the IANManager part another part it can use to create and run actions to process each set of ANEventInfo items as the ANManagerPart generates them.
        /// If a client needs know when they have been delivered then the client should run a Sync(SyncFlags.ANEventDelivery) action, on completion of which all pending updates from the point of the action being started will have been delivered.
        /// </summary>
        public IANEventListHandlerPart ANEventInfoListHandlerPart { get; set; }

        /// <summary>Defines the ANManagerPart's ActionLoggingConfig that is used for part actions</summary>
        public ActionLoggingConfig ActionLoggingConfig { get; set; }

        /// <summary>Defines the ANManagerPart's ActionLoggingConfig that is used for the internal ANEvent helper's actions</summary>
        public ActionLoggingConfig ANEventHelperActionLoggingConfig { get; set; }
    }

    /// <summary>
    /// This is the interface that must be supported by any external entity that is delegated to handle and process sets (lists) of ANEventInfo objects.
    /// </summary>
    public interface IANEventListHandlerPart : IActivePartBase
    {
        /// <summary>
        /// Action factory method.  When run the resulting action will process the given list of ANEventInfo items.
        /// The action returned by this method must be reusable (re-runnable) as the ANManagerPart generally creates only one such action and then runs it over and over again.
        /// </summary>
        IClientFacet ProcessANEventList(List<ANEventInfo> anEventInfoList);
    }

    /// <summary>This struct is used to pass the ANEventType and ANState object to an ANEventHandler when reporting annunciator related events to the handler.</summary>
    public struct ANEventInfo
    {
        /// <summary>Carries the ANEventType for this event</summary>
        public ANEventType ANEventType { get; set; }

        /// <summary>Carries the ANState (and the ANSpec within it) for this event</summary>
        public IANState ANState { get; set; }
    }

    /// <summary>
    /// This enumeration is used to indicate what type of event is being carried in a given ANEventInfo object.
    /// <para/>None, Registration, State
    /// </summary>
    public enum ANEventType
    {
        /// <summary>Placeholder default value (0)</summary>
        None = 0,

        /// <summary>Value used to report registration of a new IANSource</summary>
        Registration,

        /// <summary>Value used to report publication of a new IANState</summary>
        State,
    }

    /// <summary>
    /// This is the implementation class for the IANManagerPart.
    /// </summary>
    public class ANManagerPart : SimpleActivePartBase, IANManagerPart
    {
        #region Singleton helper - AutoContructIfNeeded.

        /// <summary>
        /// IANManagerPart singleton Instance property.  
        /// When this property getter is called while it is in its reset/initial unassigned state, it will construct an ANMangerPart using the default settings (PartID: ANManager, no IE30ALIDHandlerFacet)
        /// and will save that instance as the retained property
        /// May be assigned to null to clear the current instance (if one has already been created).  
        /// When this property is in its reset/initial unassigned state, it may be given an explicitly constructed IANMangerPart instance.
        /// </summary>
        public static IANManagerPart Instance { get { return singletonHelper.Instance; } set { singletonHelper.Instance = value; } }
 
        private static SingletonHelperBase<IANManagerPart> singletonHelper = new SingletonHelperBase<IANManagerPart>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new ANManagerPart("ANManager", null, Values.Instance));

        #endregion

        #region construction

        /// <summary>
        /// Constructor.  <paramref name="partID"/> is required.  <paramref name="e30ALIDHandlerFacet"/>, <paramref name="ivi"/>, <paramref name="iConfig"/>, and <paramref name="isi"/> are optional
        /// </summary>
        public ANManagerPart(string partID, IE30ALIDHandlerFacet e30ALIDHandlerFacet = null, IValuesInterconnection ivi = null, IConfig iConfig = null, ISetsInterconnection isi = null) 
            : this(new ANManagerPartConfig(partID) { E30ALIDHandlerFacet = e30ALIDHandlerFacet, IVI = ivi, IConfig = iConfig, ISI = isi })
        { }

        /// <summary>
        /// ANManagerPartConfig based Constructor.  Given <paramref name="anManagerPartConfig"/> object contents defines the configuration for this part
        /// </summary>
        public ANManagerPart(ANManagerPartConfig anManagerPartConfig)
            : base(anManagerPartConfig.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true))
        {
            ANManagerPartConfig = new ANManagerPartConfig(anManagerPartConfig);

            ActionLoggingConfig = ANManagerPartConfig.ActionLoggingConfig;

            SetupModularConfig();

            e30ALIDHandlerFacet = ANManagerPartConfig.E30ALIDHandlerFacet;
            IVI = ANManagerPartConfig.IVI;
            IConfig = ANManagerPartConfig.IConfig;
            ISI = ANManagerPartConfig.ISI;

            SetupSetRegistrationAndUnregistration();

            SetupANEventDelivery();
        }

        private ANManagerPartConfig ANManagerPartConfig { get; set; }

        private IValuesInterconnection IVI { get; set; }
        private IConfig IConfig { get; set; }
        private ISetsInterconnection ISI { get; set; }

        private IReferenceSet<ANState> anStateCurrentActiveSet;
        private IReferenceSet<ANState> anStateRecentlyClearedSet;
        private IReferenceSet<ANState> anStateHistorySet;

        #endregion

        #region E30ALIDHandlerFacet

        /// <summary>
        /// Normally the part is passed the desired e30ALIDHandlerFacet to use.  
        /// However after the part has been started, the client may use this setter to provide the desired e30ALIDHandlerFacet to be used hereafter.
        /// <para/>Note: this method will throw an InvalidOperationExecption if the client attempts to change a previously provided handler facet instance.
        /// <para/>Note also: The use of this method will start the part if the part has not already been started.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">This exception will be thrown if the client attempts to change a previously provided E30ALIDHandlerFacet instance</exception>
        public IE30ALIDHandlerFacet E30ALIDHandlerFacet
        {
            private get { return e30ALIDHandlerFacet; }
            set
            {
                ActionMethodDelegateStrResult setE30ALIDHandlerFacetDelegate = () =>
                {
                    if (e30ALIDHandlerFacet == null)
                    {
                        e30ALIDHandlerFacet = value;
                        serviceE30ALIDHandlerFacetLookupNow |= (value != null);
                        return string.Empty;
                    }
                    else if (Object.ReferenceEquals(e30ALIDHandlerFacet, value))
                        return string.Empty;
                    else
                        return "E30ALIDHandlerFacet has already been set to a non-null value.  It cannot be changed again";
                };

                StartPartIfNeeded();
                IClientFacet action = new BasicActionImpl(actionQ, setE30ALIDHandlerFacetDelegate, "E30ALIDHandlerFacet setter", ActionLoggingReference).RunInline();

                if (!action.ActionState.Succeeded)
                    throw new System.InvalidOperationException(action.ActionState.ResultCode);
            }
        }
        private volatile IE30ALIDHandlerFacet e30ALIDHandlerFacet = null;
        private bool serviceE30ALIDHandlerFacetLookupNow = false;
        
        #endregion

        #region Configuration Values - as obtained from Modular.Config

        private ConfigValuesSet ConfigValues { get; set; }
        private ConfigValueSetAdapter<ConfigValuesSet> configValuesAdapter;

        /// <summary>
        /// Creates and Sets up the configValueAdapter, and thus populates the Config property with initial values.
        /// </summary>
        private void SetupModularConfig()
        {
            configValuesAdapter = new ConfigValueSetAdapter<ConfigValuesSet>()
            {
                ValueSet = new ConfigValuesSet(),
                SetupIssueEmitter = Log.Debug,
                UpdateIssueEmitter = Log.Debug,
                ValueNoteEmitter = Log.Debug,
            }.Setup(IConfig, "{0}.".CheckedFormat(PartID));

            ConfigValues = configValuesAdapter.ValueSet;
        }

        /// <summary>
        /// Checks if the configValuesAdapter IsUpdateNeeded.  If so updates it and refreshes all dependent data.  
        /// Actually the Config property refers to the same instance that the configValuesAdapter updates so there is not actual work required to refresh the dependent data here.
        /// </summary>
        private void ServiceConfig()
        {
            if (configValuesAdapter.IsUpdateNeeded)
            {
                configValuesAdapter.Update();
            }
        }

        /// <summary>
        /// Configuration values for ANManagerPart.  Supports use of ModularConfig to provide non-default values for Set Capacity values.
        /// </summary>
        public class ConfigValuesSet
        {
            /// <summary>
            /// Constructor.  
            /// Requires caller provided partID which will be used as the name for the corresponding ANManagerPart.
            /// Sets ANStateRecentlyActiveSetMaxCount to 100 and ANStateHistorySetMaxCount to 10000,
            /// </summary>
            public ConfigValuesSet()
            {
                ANStateRecentlyClearedSetMaxCount = 100;
                ANStateRecentlyClearedSetMaxRetentionTimeSpan = TimeSpan.FromHours(1.0);

                ANStateHistorySetMaxCount = 1000;
                ANStateCurrentlyActiveSetMaxCount = 1000;

                AutoAcknowledgeInformationAfterTimeSpan = TimeSpan.Zero;
                AutoAcknowledgeWarningAfterTimeSpan = TimeSpan.Zero;
                AutoAcknowledgeAlarmAfterTimeSpan = TimeSpan.Zero;
                AutoAcknowledgeErrorAfterTimeSpan = TimeSpan.Zero;
            }

            /// <summary>Defines the Capacity (maximum count) of the Recently Active set of ANState objects.  Defaults to 100.  Supports use with the correspondingly named ConfigKey</summary>
            [ConfigItem(IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
            public int ANStateRecentlyClearedSetMaxCount { get; set; }

            /// <summary>Defines the TimeSpan during which recently cleared items will remain in the recently cleared set.  Defaults to 1 hour.</summary>
            [ConfigItem(IsOptional = true, SilenceIssues = true)]
            public TimeSpan ANStateRecentlyClearedSetMaxRetentionTimeSpan { get; set; }

            /// <summary>Defines the Capacity (maximum count) of the History set of ANState objects.  Defaults to 10000.  Supports use with the correspondingly named ConfigKey</summary>
            [ConfigItem(IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
            public int ANStateHistorySetMaxCount { get; set; }

            /// <summary>Defines the Capacity (maximum count) of the Currently Active set of ANState objects.  Defaults to 1000.  Supports use with the correspondingly named ConfigKey</summary>
            [ConfigItem(IsOptional = true, ReadOnlyOnce = true, SilenceIssues = true)]
            public int ANStateCurrentlyActiveSetMaxCount { get; set; }

            /// <summary>When non-zero, this defines the TimeSpan after which an enabled Acknowledge only Information E041 annunciator will have its "Acknowledge" action selectected automatically.  Defaults to 0.0</summary>
            [ConfigItem(IsOptional = true, SilenceIssues = true)]
            public TimeSpan AutoAcknowledgeInformationAfterTimeSpan { get; set; }

            /// <summary>When non-zero, this defines the TimeSpan after which an enabled Acknowledge only Warning E041 annunciator will have its "Acknowledge" action selectected automatically.  Defaults to 0.0</summary>
            [ConfigItem(IsOptional = true, SilenceIssues = true)]
            public TimeSpan AutoAcknowledgeWarningAfterTimeSpan { get; set; }

            /// <summary>When non-zero, this defines the TimeSpan after which an enabled Acknowledge only Alarm E041 annunciator will have its "Acknowledge" action selectected automatically.  Defaults to 0.0</summary>
            [ConfigItem(IsOptional = true, SilenceIssues = true)]
            public TimeSpan AutoAcknowledgeAlarmAfterTimeSpan { get; set; }

            /// <summary>When non-zero, this defines the TimeSpan after which an enabled Acknowledge only Error E041 annunciator will have its "Acknowledge" action selectected automatically.  Defaults to 0.0</summary>
            [ConfigItem(IsOptional = true, SilenceIssues = true)]
            public TimeSpan AutoAcknowledgeErrorAfterTimeSpan { get; set; }

            /// <summary>This defines the TimeSpan after which a reason change will cause publication of its ANState.  Defaults to 0.0</summary>
            [ConfigItem(IsOptional = true, SilenceIssues = true)]
            public TimeSpan AcceptAlarmReasonChangeAfterTimeSpan { get; set; }
        }

        #endregion

        #region IANManagerPart interface

        /// <summary>
        /// Requests that the manager create a new IANSource annunciator source for the given anSpec.  
        /// The given anSpec.Name must be unique (IE it cannot have already been registered).
        /// Returns the the requested IANSource on success or null if the registration failed.
        /// <para/>Note that the <paramref name="sourceObjectID"/> is only provided (and retained by the manager) to improve the quality of error log messages in cases where two clients attempt to register the same ANName.  This parameter is not otherwise used.
        /// </summary>
        /// <exception cref="ANRegistrationException">thrown with appropriate message if given anSpec is not vALID or if the given ANName has already been registered</exception>
        /// <remarks>As with CreateGoOnlineAction(bool andInitialize), this method also starts the target part if it has not already been started</remarks>
        public IANSource RegisterANSource(string sourceObjectID, IANSpec anSpec)
        {
            string methodName = CurrentMethodName;
            string methodNameEx = "{0}({1}, {2})".CheckedFormat(methodName, sourceObjectID, anSpec);

            if (anSpec.ANType == ANType.Alarm)
                throw new ANRegistrationException("{0} is not valid: ANType {1} is not compatible with this usage interface type".CheckedFormat(methodNameEx, anSpec.ANType));

            StartPartIfNeeded();
            
            ANSpec anSpecCopy = new ANSpec(anSpec);
            ANSourceImpl sourceImpl = null;
            System.Exception ex = null;

            IBasicAction action = new BasicActionImpl(actionQ, () => PerformRegisterAnnunciatorSource(sourceObjectID, anSpecCopy, false, ref sourceImpl, ref ex, methodName), methodNameEx, ActionLoggingReference);
            string ec = action.Run();

            if (ex != null)
                throw ex;
            if (!ec.IsNullOrEmpty())
                throw new ANRegistrationException("Internal: {0} failed with error: {1}".CheckedFormat(methodNameEx, ec));

            return sourceImpl;
        }

        /// <summary>
        /// Requests that the manager create a new IANOccurrence type annunciator source for the given anSpec.  
        /// The given anSpec.Name must be unique (IE it cannot have already been registered) and the given anSpec.ANType must not be the Alarm type.
        /// Returns the requested IANOccurrence for the annunciator on success or null if the registration failed.
        /// <para/>Note that the <paramref name="sourceObjectID"/> is only provided (and retained by the manager) to improve the quality of error log messages in cases where two clients attempt to register the same ANName.  This parameter is not otherwise used.
        /// </summary>
        /// <exception cref="ANRegistrationException">thrown with appropriate message if given anSpec is not valid for this type of source or if the given ANName has already been registered</exception>
        /// <remarks>As with CreateGoOnlineAction(bool andInitialize), this method also starts the target part if it has not already been started</remarks>
        public IANOccurrence RegisterANOccurrenceSource(string sourceObjectID, IANSpec anSpec)
        {
            string methodName = CurrentMethodName;
            string methodNameEx = "{0}({1}, {2})".CheckedFormat(methodName, sourceObjectID, anSpec);

            if (anSpec.ANType == ANType.Alarm)
                throw new ANRegistrationException("{0} is not valid: ANType {1} is not compatible with this usage interface type".CheckedFormat(methodNameEx, anSpec.ANType));

            StartPartIfNeeded();

            ANSpec anSpecCopy = new ANSpec(anSpec);
            ANSourceImpl sourceImpl = null;
            System.Exception ex = null;

            IBasicAction action = new BasicActionImpl(actionQ, () => PerformRegisterAnnunciatorSource(sourceObjectID, anSpecCopy, true, ref sourceImpl, ref ex, methodName), methodNameEx, ActionLoggingReference);
            string ec = action.Run();

            if (ex != null)
                throw ex;
            if (!ec.IsNullOrEmpty())
                throw new ANRegistrationException("Internal: {0} failed with error: {1}".CheckedFormat(methodNameEx, ec));

            return sourceImpl;
        }

        /// <summary>
        /// Requests that the manager create a new IANCondition type annunciator source for the given anSpec.  
        /// The given anSpec.Name must be unique (IE it cannot have already been registered) and the given anSpec.ANType must be the Alarm type.
        /// Returns the requested IANCondition for the annunciator on success or null if the registration failed.
        /// <para/>Note that the <paramref name="sourceObjectID"/> is only provided (and retained by the manager) to improve the quality of error log messages in cases where two clients attempt to register the same ANName.  This parameter is not otherwise used.
        /// </summary>
        /// <exception cref="ANRegistrationException">thrown with appropriate message if given anSpec is not valid for this type of source or if the given ANName has already been registered</exception>
        /// <remarks>As with CreateGoOnlineAction(bool andInitialize), this method also starts the target part if it has not already been started</remarks>
        public IANCondition RegisterANConditionSource(string sourceObjectID, IANSpec anSpec, IANConditionBehavior initialANConditionBehavior = default(IANConditionBehavior))
        {
            string methodName = CurrentMethodName;
            string behaviorStr = ((initialANConditionBehavior != IANConditionBehavior.None) ? " {0}".CheckedFormat(initialANConditionBehavior) : "");
            string methodNameEx = "{0}({1}, {2}{3})".CheckedFormat(methodName, sourceObjectID, anSpec, behaviorStr);

            if (anSpec.ANType == ANType.Error)
                throw new ANRegistrationException("{0} is not valid: ANType {1} is not compatible with this usage interface type".CheckedFormat(methodNameEx, anSpec.ANType));

            StartPartIfNeeded();

            ANSpec anSpecCopy = new ANSpec(anSpec);
            ANSourceImpl sourceImpl = null;
            System.Exception ex = null;

            IBasicAction action = new BasicActionImpl(actionQ, () => PerformRegisterAnnunciatorSource(sourceObjectID, anSpecCopy, true, ref sourceImpl, ref ex, methodName), methodNameEx, ActionLoggingReference);
            string ec = action.Run();

            if (ex != null)
                throw ex;

            if (!ec.IsNullOrEmpty())
                throw new ANRegistrationException("Internal: {0} failed with error: {1}".CheckedFormat(methodNameEx, ec));

            sourceImpl.IANConditionBehavior = initialANConditionBehavior;

            return sourceImpl;
        }

        /// <summary>
        /// Allows the caller to obtain an object source publisher that publishes IANState updates for the given annunciator name (anName).  
        /// May be used to obtain the publisher for a name before the name has actually been registered.
        /// When obtaining the publisher before the corresonding source has been registered, the returned publisher will remain in its initial, has no value, state 
        /// until the source has been registered and the manager has published its initial state.
        /// </summary>
        public ISequencedObjectSource<IANState, Int32> GetANStatePublisher(string anName)
        {
            string methodName = "{0}({1})".CheckedFormat(Fcns.CurrentMethodName, anName);

            ISequencedObjectSource<IANState, Int32> anStatePublisher = null;

            IBasicAction action = new BasicActionImpl(actionQ, () => PerformGetANStatePublisher(anName, ref anStatePublisher), methodName, ActionLoggingReference);
            string ec = action.Run();

            return anStatePublisher;
        }

        /// <summary>
        /// Creates and returns an action that, when run, will indicate that the decision authority has requested that the given selectedActionName be performed for the indicated annunciator name.
        /// <para/>This is functionally equivalent to creating a ServiceAction called "SetSelectedActionName" with named string parameters "ANName" and "SelectedActionName"
        /// <para/>When <paramref name="onlyIfOnAndWaiting"/> is selected (defaults to false) then the action will be a no-op if the selected annunciator is not already in an OnAndWaiting signal state.  
        /// Selecting this option is equivalent to adding the "OnlyIfOnAndWaiting" keyword to the actions named param values.
        /// </summary>
        public IStringParamAction CreateSetSelectedActionNameAction(string anName, string selectedActionName, bool onlyIfOnAndWaiting = false)
        {
            return CreateServiceAction("SetSelectedActionName", new NamedValueSet() { { "ANName", anName }, { "SelectedActionName", selectedActionName } }.ConditionalSetKeyword("OnlyIfOnAndWaiting", onlyIfOnAndWaiting));
        }

        /// <summary>
        /// Creates and returns an action that, when run, will indicate that the decision authority has requested that the annunciator source abort whatever action it is currently performing.
        /// <para/>This is functionally equivilant to creating a ServiceAction called "RequestActionAbort" with named string parameter "ANName"
        /// </summary>
        public IStringParamAction CreateRequestActionAbortAction(string anName)
        {
            return CreateServiceAction("RequestActionAbort", new NamedValueSet() { { "ANName", anName } });
        }

        /// <summary>Creates and returns an action that, when run, will clear the ANStateRecentlyClearedSet.  Equivalent to running a "ClearResentSet" service action.</summary>
        public IStringParamAction CreateClearRecentSet()
        {
            return CreateServiceAction("ClearRecentSet");
        }

        /// <summary>Creates and returns an action that, when run, will clear the ANStateHistorySet.  Equivalent to running a "ClearHistorySet" service action.</summary>
        public IStringParamAction CreateClearHistorySet()
        {
            return CreateServiceAction("ClearHistorySet");
        }

        /// <summary>
        /// Creates and returns an action that, when run, will attempt to select the given action name on all signaling annunciators that have such an action name enabled.
        /// <para/>This is functionally equivalent to creating a ServiceAction called "SelectActionNameForAll" with a named string parameter "SelectedActionName"
        /// </summary>
        public IStringParamAction CreateSelectActionNameForAll(string selectedActionName)
        {
            return CreateServiceAction("SelectActionNameForAll", new NamedValueSet() { { "SelectedActionName", selectedActionName } });
        }


        /// <summary>
        /// Action factory method.  When the returned action is run, it waits until the IANManagerPart has completed the requested synchronization steps and then the action completes.
        /// This action may be run concurrently with other actions and part behaviors.
        /// <para/>Use the <paramref name="flags"/> parameter to select the synchronization options that you would like it to wait to be synchronized with.
        /// </summary>
        public IClientFacet Sync(SyncFlags flags = default(SyncFlags))
        {
            return new BasicActionImpl(actionQ, ipf => PerformSync(ipf, flags), "{0}({1})".CheckedFormat(CurrentMethodName, flags), ActionLoggingReference);
        }

        #endregion

        #region internal sets of ANSourceTracking objects and support for name to ANSourceTracking lookup (FindANSourceTrackingByName)

        private IListWithCachedArray<ANSourceTracking> anSourceTrackingList = new IListWithCachedArray<ANSourceTracking>();
        private Dictionary<string, ANSourceTracking> anNameToSourceTrackingDictionary = new Dictionary<string, ANSourceTracking>();
        private IListWithCachedArray<ANSourceImpl> anSourceServiceList = new IListWithCachedArray<ANSourceImpl>();

        private List<ANSourceTracking> pendingLookupANSourceTrackingList = new List<ANSourceTracking>();

        private ANSourceTracking FindANSourceTrackingByName(string anName, bool createIfNeeded)
        {
            {
                ANSourceTracking foundSourceTracking = anNameToSourceTrackingDictionary.SafeTryGetValue(anName);

                if (foundSourceTracking != null || !createIfNeeded)
                    return foundSourceTracking;
            }

            {
                var createdSourceTracking = new ANSourceTracking() { anName = anName, listIndex = anSourceTrackingList.Count };

                anSourceTrackingList.Add(createdSourceTracking);
                anNameToSourceTrackingDictionary[anName] = createdSourceTracking;

                return createdSourceTracking;
            }
        }

        #endregion

        #region internal action implementations (PerformYYY methods and directly related methods).

        private string PerformRegisterAnnunciatorSource(string sourceObjectID, ANSpec anSpecCopy, bool requiresService, ref ANSourceImpl sourceImpl, ref System.Exception ex, string reason)
        {
            try
            {
                string anName = anSpecCopy.ANName;

                ANSourceTracking anSourceTracking = FindANSourceTrackingByName(anName, true);

                if (anSourceTracking.anSourceImpl != null)
                {
                    throw new ANRegistrationException("Registration failed: {0} has already been registered by {1}".CheckedFormat(anName, anSourceTracking.anSourceImpl.SourceObjectID));
                }

                // setup the new source, add a new tracking object for it, and return it.

                anSpecCopy.ANManagerTableRegistrationNum = anSourceTracking.listIndex + 1;

                ANAlarmID foundALID = ANAlarmID.None;
                ALIDLookupState foundALIDLookupState = ALIDLookupState.None;

                if (anSpecCopy.ALID.IsLookup())
                {
                    if (ANManagerPartConfig.ALIDDictionary != null)
                    {
                        // when dictionary ALID lookup is in use, the only resulting lookup states are Found and NotFound.  This logic never generates the Pending state.
                        foundALID = unchecked((ANAlarmID)ANManagerPartConfig.ALIDDictionary.SafeTryGetValue(anSpecCopy.ANName, fallbackValue: unchecked((int)ANAlarmID.None)));
                        foundALIDLookupState = (foundALID != ANAlarmID.None) ? ALIDLookupState.Found : ALIDLookupState.NotFound;
                    }
                    else if (E30ALIDHandlerFacet != null)
                    {
                        InnerLookupALIDAndLogIfNotFound(anSpecCopy, out foundALID, out foundALIDLookupState);
                    }
                    else
                    {
                        foundALIDLookupState = ALIDLookupState.Pending;
                    }
                }

                ANSourceImpl anSourceImpl = new ANSourceImpl() 
                    { 
                        ParentPart = this,
                        ManagersImmutableANStatePublicationDelegate = (immutableANStatePublicationDelegate ?? (immutableANStatePublicationDelegate = AsyncAcceptANStateUpdate)),
                        ANSpec = anSpecCopy, 
                        SourceObjectID = sourceObjectID, 
                        AcceptAlarmReasonChangeAfterTimeSpan = ConfigValues.AcceptAlarmReasonChangeAfterTimeSpan,
                    }.Setup(foundALID, foundALIDLookupState, reason);

                anSourceTracking.anSpec = anSpecCopy;
                anSourceTracking.anSourceImpl = anSourceImpl;
                if (IVI != null)
                    anSourceTracking.anStateIVA = IVI.GetValueAccessor<ANState>("{0}.ANStates.{1}".CheckedFormat(PartID, anName));

                anSourceImpl.ManagersANStatePublisher = anSourceTracking.anStatePublisher;

                if (requiresService)
                    anSourceServiceList.Add(anSourceImpl);

                sourceImpl = anSourceImpl;

                // set and publish the initialANState
                anSourceTracking.Publish(anSourceTracking.initialANState = new ANState(anSourceImpl.InitialANState));

                anSourceTracking.sendRegistrationEvent = true;
                lastStateSeqNum = anSourceTracking.lastEventSeqNum = GenerateNextEventSeqNum();

                if (foundALIDLookupState == ALIDLookupState.Pending)
                    pendingLookupANSourceTrackingList.Add(anSourceTracking);

                return string.Empty;
            }
            catch (System.Exception caughtEx)
            {
                ex = caughtEx;
                return "Caught {0}".CheckedFormat(ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        private string PerformGetANStatePublisher(string anName, ref ISequencedObjectSource<IANState, Int32> anStatePublisher)
        {
            ANSourceTracking anSourceTracking = FindANSourceTrackingByName(anName, true);

            anStatePublisher = anSourceTracking.anStatePublisher;
            return String.Empty;
        }

        /// <summary>
        /// Optionally forces a ServiceALIDLookups (when <paramref name="andInitialize"/> is true) and then runs a service iteration (PerformMainLoopService)
        /// Returns success.
        /// </summary>
        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            if (andInitialize)
                ServiceALIDLookups(true);

            PerformMainLoopService();

            return "";
        }

        /// <summary>
        /// Provides the part type specific implementation for performing all part type specific service actions.
        /// These include: SetSelectedActionName, RequestActionAbort, ClearRecentState, ClearHistorySet, and SelectActionNameForAll.
        /// </summary>
        /// <remarks>
        /// SetSelectedActionName: requires ANName and SelectedActionName named values.  
        ///     Attempts to find the given source by its ANName and then attempts to request that it select the given action name.
        /// RequestActionAbort: requires ANname.
        ///     Attempts to find the given source by its ANName and then attempts to request that it abort any action that it has selected or is in the process of performing.
        /// ClearRecentSet and ClearHistorySet:
        ///     Clears the indicated set.
        /// SelectActionNameForAll: requires SelectedActionName (typically set to Acknowledge).
        ///     Iterates through the set of active sources that are in the OnAndWaiting state and for which the SelectedActionName is enabled and attempts to select this 
        ///     action for each such source.  In some configurations, this may be used to Acknowledging all suitable sources quickly and thus remove them from the active set.
        /// </remarks>
        protected override string PerformServiceActionEx(Modular.Action.IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            ANSourceTracking anSourceTracking = null;
            ANSourceImpl anSource = null;

            if (npv.Contains("ANName"))
            {
                anSourceTracking = FindANSourceTrackingByName(npv["ANName"].VC.GetValue<string>(false), false);
                anSource = ((anSourceTracking != null) ? anSourceTracking.anSourceImpl : null);
            }

            bool onlyIfOnAndWaiting = npv.Contains("OnlyIfOnAndWaiting");

            string resultStr = null;

            switch (serviceName)
            {
                case "SetSelectedActionName":
                    {
                        if (anSource == null)
                            return "ANName param is missing or invalid";

                        string selectedActionName = npv["SelectedActionName"].VC.GetValue<string>(false);
                        if (selectedActionName.IsNullOrEmpty())
                            return "SelectedActionName is missing or is invalid";

                        ProcessQueuedANStateUpdates();

                        IANState anState = anSourceTracking.lastServicedANState ?? anSourceTracking.initialANState;
                        if (anState.ANSignalState != ANSignalState.OnAndWaiting)
                            return onlyIfOnAndWaiting ? "" : "Annunciator's current state {0} is not waiting for action selection".CheckedFormat(anState.ANSignalState);

                        resultStr = anSource.ProcessSetSelectActionNameRequest(selectedActionName);

                        PerformMainLoopService();
                    }
                    break;

                case "RequestActionAbort":
                    {
                        if (anSource == null)
                            return "ANName param is missing or invalid";

                        resultStr = anSource.ProcessRequestActionAbort();

                        PerformMainLoopService();
                    }
                    break;

                case "ClearRecentSet":
                    anStateRecentlyClearedSet.Clear();
                    resultStr = String.Empty;
                    break;

                case "ClearHistorySet":
                    anStateHistorySet.Clear();
                    resultStr = String.Empty;
                    break;

                case "SelectActionNameForAll":
                    {
                        string selectedActionName = npv["SelectedActionName"].VC.GetValue<string>(false);
                        if (selectedActionName.IsNullOrEmpty())
                            return "SelectedActionName is missing or is invalid";

                        ProcessQueuedANStateUpdates();

                        foreach (ANState anState in anStateCurrentActiveSet)
                        {
                            // this request can only be applied to items that are already OnAndWaiting and which do not already have a selected action.
                            if (anState.ANSignalState != ANSignalState.OnAndWaiting || !anState.SelectedActionName.IsNullOrEmpty())
                                continue;

                            INamedValueSet actionList = anState.ActionList;
                            if (actionList.Contains(selectedActionName))
                            {
                                string actionDisabledResaon = actionList[selectedActionName].GetActionDisableReason();
                                if (!actionDisabledResaon.IsNullOrEmpty())
                                    continue;
                            }

                            anSourceTracking = FindAnSourceTrackingFromANState(anState);
                            if (anSourceTracking == null || anSourceTracking.anSourceImpl == null)
                                continue;

                            string ec = anSourceTracking.anSourceImpl.ProcessSetSelectActionNameRequest(selectedActionName);
                            if (!ec.IsNullOrEmpty())
                                resultStr = resultStr ?? ec;
                        }

                        resultStr = resultStr ?? String.Empty;
                    }
                    break;

                default:
                    resultStr = base.PerformServiceActionEx(ipf, serviceName, npv);
                    break;
            }

            return resultStr;
        }

        #endregion

        #region ServiceALIDLookups, ServiceALIDLookup, InnerLookupALIDAndLogIfNotFound methods.

        private void ServiceALIDLookups(bool forceLookupNow = false)
        {
            var e30ALIDHandlerFacet = E30ALIDHandlerFacet;

            if (e30ALIDHandlerFacet != null && (serviceE30ALIDHandlerFacetLookupNow || forceLookupNow))
            {
                serviceE30ALIDHandlerFacetLookupNow = false;

                foreach (ANSourceTracking anSourceTracking in pendingLookupANSourceTrackingList)
                    ServiceALIDLookup(anSourceTracking);

                pendingLookupANSourceTrackingList.Clear();
            }
        }

        private void ServiceALIDLookup(ANSourceTracking anSourceTracking)
        {
            ANAlarmID foundALID;
            ALIDLookupState foundALIDLookupState;

            InnerLookupALIDAndLogIfNotFound(anSourceTracking.anSpec, out foundALID, out foundALIDLookupState);

            anSourceTracking.anSourceImpl.HandleALIDLookupResult(foundALID, foundALIDLookupState);
        }

        private void InnerLookupALIDAndLogIfNotFound(IANSpec anSpec, out ANAlarmID foundALID, out ALIDLookupState foundALIDLookupState)
        {
            int foundALIDValue = E30ALIDHandlerFacet.GetALIDFromANSpec(anSpec);

            if (foundALIDValue != 0)
            {
                foundALID = unchecked((ANAlarmID)foundALIDValue);
                foundALIDLookupState = ALIDLookupState.Found;
                Log.Trace.Emit("ALID {0} found for ANSpec '{1}'", foundALID, anSpec);
            }
            else
            {
                foundALID = ANAlarmID.None;
                foundALIDLookupState = ALIDLookupState.NotFound;

                if (anSpec.ALID == ANAlarmID.Lookup)
                    Log.Error.Emit("Expected ALID was not found for ANSpec '{0}'", anSpec);
                else
                    Log.Debug.Emit("Optional ALID was not found for ANSpec '{0}'", anSpec);
            }
        }


        #endregion

        #region ANSourceTracking and event seq num generation

        ulong eventSeqNumSource = 0;

        ulong GenerateNextEventSeqNum() { return ++eventSeqNumSource; }

        /// <summary>
        /// This is the manager level object that is created and retained for each anName registered, or for which a publisher has been requested.
        /// It contains the original anName and listIndex from when it was added to the overall anSourceTrackingList and related dictionary.
        /// It contains the anSpec, and anSourceImpl instance from when the name was registered as a source.
        /// It also contains all of the information that the manager needs to keep in order to service the source, 
        /// in order to implement automatic recovery action selection and handling when such optional features are enabled.
        /// </summary>
        private class ANSourceTracking
        {
            public string anName;
            public int listIndex;

            public ulong lastEventSeqNum;

            public bool sendRegistrationEvent;
            public bool sendStateEvent;

            public InterlockedSequencedRefObject<IANState> anStatePublisher = new InterlockedSequencedRefObject<IANState>();
            public IValueAccessor<ANState> anStateIVA;

            public ANState initialANState;
            public ANState lastServicedANState;

            /// <summary>publishes the given IANState to both the anStatePublisher and the optional anStateIVA</summary>
            public void Publish(IANState anState)
            {
                ANState anStateCopy = new ANState(anState);
                anStatePublisher.Object = anStateCopy;
                if (anStateIVA != null)
                    anStateIVA.Set(anStateCopy);
            }

            public IANSpec anSpec;

            public ANSourceImpl anSourceImpl;
        }

        #endregion

        #region IANSource implementation object (ANSouceImpl) and private helper methods

        /// <summary>
        /// This is the internal implementation class for all IANSourceBase derived types: IANSource, IANOccurrence, and IANCondition.
        /// It contains the implementation methods both the client side and manager side usable members.
        /// </summary>
        private class ANSourceImpl : IANSource, IANOccurrence, IANCondition, IANAcknowledgeableSourceBase, IANSourceBase
        {
            private readonly object mutex = new object();

            #region fields and properties that are filled in/setup by the parent part

            public ANManagerPart ParentPart { get; set; }
            public ImmutableANStatePublicationDelegate ManagersImmutableANStatePublicationDelegate { get; set; }
            public ANSpec ANSpec { get; set; }
            public string SourceObjectID { get; set; }

            public TimeSpan AcceptAlarmReasonChangeAfterTimeSpan { get; set; }

            public Logging.ILogger Logger { get; private set; }
            public Logging.IMesgEmitter SetSignalStateEmitter { get; private set; }

            public ISequencedObjectSource<IANState, Int32> ManagersANStatePublisher { get; set; }

            public IANState InitialANState { get; private set; }

            public ANSourceImpl Setup(ANAlarmID initialFoundALID, ALIDLookupState initialALIDLookupState, string reason)
            {
                lock (mutex)
                {
                    ANState = new ANState() { ANSpec = ANSpec, Reason = reason };

                    Logger = new Logging.Logger("AN." + ANSpec.ANName);
                    SetSignalStateEmitter = Logger.Debug;

                    if (ANSpec.ALID.IsNone())
                    {
                        ANState.ALID = ANSpec.ALID;
                        ANState.ALIDLookupState = ALIDLookupState.None;
                    }
                    else if (ANSpec.ALID.IsLookup())
                    {
                        ANState.ALID = initialFoundALID;
                        ANState.ALIDLookupState = initialALIDLookupState;
                    }
                    else
                    {
                        // it has been explicitly defined by the client
                        ANState.ALID = ANSpec.ALID;
                        ANState.ALIDLookupState = ALIDLookupState.Defined;
                    }

                    SetSignalState(ANSignalState.Off, reason, true);

                    InitialANState = LastSourceGeneratedState;

                    return this;
                }
            }

            #endregion

            #region fields and properties that are used by the parent part to track and service this anSource

            public void Service()
            {
                lock (mutex)
                {
                    InnerServiceAcknowledgeActionSelected();
                }
            }


            public void HandleALIDLookupResult(ANAlarmID finalALID, ALIDLookupState finalALIDLookupState)
            {
                lock (mutex)
                {
                    ANState.ALID = finalALID;
                    ANState.ALIDLookupState = finalALIDLookupState;

                    CloneAndPublishANStateToManager();
                }
            }

            public string ProcessSetSelectActionNameRequest(string selectActionName)
            {
                lock (mutex)
                {
                    IANSpec currentANSpec = ANSpec;
                    ANSignalState currentANSignalState = ANState.ANSignalState;
                    string currentSelectedActionName = ANState.SelectedActionName;
                    INamedValueSet currentActionList = ANState.ActionList;

                    if (!currentSelectedActionName.IsNullOrEmpty())
                        return "Action '{0}' has already been selected".CheckedFormat(currentSelectedActionName);

                    INamedValue selectedActionEnableNV = currentActionList[selectActionName];
                    if (selectedActionEnableNV.IsNullOrEmpty())
                        return "Action '{0}' is not a valid selection.  {1}".CheckedFormat(selectActionName, currentActionList.ToString(false, true));

                    string disableReason = selectedActionEnableNV.GetActionDisableReason();

                    if (!disableReason.IsNullOrEmpty())
                        return "Action '{0}' is not a currently enabled: '{1}'".CheckedFormat(selectActionName, disableReason, currentActionList.ToString(false, true));

                    ANState.SelectedActionName = selectActionName;

                    Logger.Debug.Emit("Action '{0}' has been selected [current state {1}]", selectActionName, currentANSignalState);

                    notifyOnActionSelectedOrAbortedBNL.Notify();

                    return String.Empty;
                }
            }

            public string ProcessRequestActionAbort()
            {
                lock (mutex)
                {
                    // this method never complains about unexpected abort requests in any ANSignalState.  

                    ANSignalState currentANSignalState = ANState.ANSignalState;

                    if (!ANState.ActionAbortRequested)
                    {
                        ANState.ActionAbortRequested = true;
                        Logger.Debug.Emit("Action Abort has been requested [current state {0}]", currentANSignalState);
                    }
                    else
                    {
                        Logger.Debug.Emit("Action Abort Request has been repeated [current state {0}]", currentANSignalState);
                    }

                    notifyOnActionSelectedOrAbortedBNL.Notify();

                    return String.Empty;
                }
            }

            #endregion

            #region IANSourceBase, IANAcknowledgeableSourceBase, IANSource, IANCondition, IANOccurrence

            /// <summary>IBasicNotificationList which is notified when an action is selected on an annunciator or when an action abort is requested.</summary>
            IBasicNotificationList IANSource.NotifyOnActionSelectedOrAbortedList { get { return notifyOnActionSelectedOrAbortedBNL; } }

            private BasicNotificationList notifyOnActionSelectedOrAbortedBNL = new BasicNotificationList();

            /// <summary>Gets a copy of the originally registered ANSpec</summary>
            IANSpec IANSourceBase.ANSpec { get { return this.ANSpec; } }

            /// <summary>Gets access to the most recently generated ANState for this annunciator.  Normally only the source causes this to change but selection of an action and requesting an abort are performed externally.</summary>
            IANState IANSourceBase.ANState { get { return this.ANState; } }

            ANState ANState { get; set; }
            IANState LastSourceGeneratedState { get; set; }

            /// <summary>Gives the client access to the state publisher object that the manager creates in order to publish anState changes after it has processed them.</summary>
            ISequencedObjectSource<IANState, Int32> IANSourceBase.ANStatePublisher { get { return ManagersANStatePublisher; } }

            static INamedValueSet ClearEnabledActionsIfNeeded(INamedValueSet fromNVS)
            {
                if (fromNVS.IsAnyActionEnabled())
                {
                    NamedValueSet toNVS = new NamedValueSet(fromNVS);

                    foreach (NamedValue nv in toNVS)
                    {
                        nv.VC = new ValueContainer(false);
                    }

                    return toNVS.MakeReadOnly();
                }
                else
                {
                    return fromNVS;
                }
            }

            /// <summary>Sets the signal state to be non-signaling.</summary>
            void IANSource.Clear(string reason)
            {
                lock (mutex)
                {
                    if (ANState.ANSignalState.IsSignaling())
                    {
                        if (ANState.ANSignalState.IsActionActive())
                        {
                            SetSignalState(ANSignalState.OnAndActionAborted, "Clear used before action completed", true);
                            ANState.ActiveActionName = null;
                        }

                        if (!ANState.SelectedActionName.IsNullOrEmpty())
                        {
                            Logger.Debug.Emit("cleared unexpected residual SelectedActionName:'{0}'", ANState.SelectedActionName);
                            ANState.SelectedActionName = null;
                        }

                        // clear any/all enabled actions as this point without removing the names of the available actions
                        ANState.ActionList = ClearEnabledActionsIfNeeded(ANState.ActionList);
                        
                        SetSignalState(ANSignalState.Off, reason, true);
                    }
                }
            }

            /// <summary>Sets the signal state to be signaling and possibly waiting (if any of the actionList items are enabled)</summary>
            void IANSource.Post(INamedValueSet actionList, string reason)
            {
                lock (mutex)
                {
                    INamedValueSet roActionList = actionList.ConvertToReadOnly();

                    bool isAnyActionEnabled = roActionList.IsAnyActionEnabled();
                    ANSignalState anSignalState = (isAnyActionEnabled ? ANSignalState.OnAndWaiting : ANSignalState.On);

                    if (ANState.ANSignalState.IsActionActive())
                    {
                        anSignalState = ANSignalState.OnAndActionActive;     // OnAndActionActive state is sticky until an is Cleared or Action is marked as complete
                        roActionList = ClearEnabledActionsIfNeeded(roActionList);
                    }

                    if (!ANState.ActionList.IsEqualTo(roActionList))
                        ANState.ActionList = roActionList;

                    SetSignalState(anSignalState, reason, true);
                }
            }

            /// <summary>Notes that the given action has been started</summary>
            void IANSource.NoteActionStarted(string actionName, string reason)
            {
                lock (mutex)
                {
                    string methodName = "{0}({1}, {2})".CheckedFormat(Fcns.CurrentMethodName, actionName, reason);

                    if (actionName.IsNullOrEmpty())
                    {
                        Logger.Debug.Emit("{0} failed: given actionName is null or empty", methodName);
                        return;
                    }

                    if (ANState.ANSignalState.IsActionActive())
                    {
                        Logger.Debug.Emit("Unexpected use of {0}: current state {1}, action:{2} already indicates that an action is active", methodName, ANState.ANSignalState, ANState.ActiveActionName);
                    }
                    else if (ANState.ANSignalState != ANSignalState.OnAndWaiting)
                    {
                        Logger.Debug.Emit("Unexpected use of {0}: current state {1} is not already OnAndWaiting", methodName, ANState.ANSignalState);
                    }

                    ANState.ActiveActionName = actionName;

                    // clear any/all enabled actions as this point without removing the names of the available actions
                    ANState.ActionList = ClearEnabledActionsIfNeeded(ANState.ActionList);

                    SetSignalState(ANSignalState.OnAndActionActive, reason, true);
                }
            }

            /// <summary>Notes that the current active action succeeded.  Normally the client Clears the annunciator immediately after this.</summary>
            void IANSource.NoteActionCompleted(string reason)
            {
                lock (mutex)
                {
                    string methodName = "{0}({1})".CheckedFormat(Fcns.CurrentMethodName, reason);

                    if (!ANState.ANSignalState.IsActionActive())
                        Logger.Debug.Emit("Unexpected use of {0}: current state {1} does not indicate that an action is active", methodName, ANState.ANSignalState);

                    ANState.SelectedActionName = null;

                    if (ANState.ActionAbortRequested)
                    {
                        ANState.ActionAbortRequested = false;
                        Logger.Debug.Emit("{0}: prior ActionAbortRequest was not handled", methodName);
                    }

                    SetSignalState(ANSignalState.OnAndActionCompleted, reason, true);
                    ANState.ActiveActionName = null;
                }
            }

            /// <summary>Notes that the current active action was aborted.  Normally the client Posts the annunciator immediately after this to update the set of available Actions.</summary>
            void IANSource.NoteActionAborted(string reason)
            {
                lock (mutex)
                {
                    string methodName = "{0}({1})".CheckedFormat(Fcns.CurrentMethodName, reason);

                    if (!ANState.ANSignalState.IsActionActive())
                        Logger.Debug.Emit("Unexpected use of {0}: current state {1} does not indicate that an action is active", methodName, ANState.ANSignalState);

                    ANState.SelectedActionName = null;
                    ANState.ActionAbortRequested = false;

                    SetSignalState(ANSignalState.OnAndActionAborted, reason, true);
                    ANState.ActiveActionName = null;
                }
            }

            /// <summary>Notes that the current active action failed and indicates why.  Normally the client Posts the annunciator immediately after this to update the set of available Actions.</summary>
            void IANSource.NoteActionFailed(string reason)
            {
                lock (mutex)
                {
                    string methodName = "{0}({1})".CheckedFormat(Fcns.CurrentMethodName, reason);

                    if (!ANState.ANSignalState.IsActionActive())
                        Logger.Debug.Emit("Unexpected use of {0}: current state {1} does not indicate that an action is active", methodName, ANState.ANSignalState);

                    ANState.SelectedActionName = null;
                    ANState.ActionAbortRequested = false;

                    SetSignalState(ANSignalState.OnAndActionFailed, reason, true);
                    ANState.ActiveActionName = null;
                }
            }

            /// <summary>This method is used to wait until the ANManagerPart has processed and delivered all side effects that relate to this ANSource</summary>
            void IANSourceBase.Sync(SyncFlags flags)
            {
                // do not acquire the lock in order to run this (since that might cause a deadlock).
                ParentPart.CreateSyncActionForSource(flags).Run();
            }


            /// <summary>Allows the caller to replace (or merge) the Anunciator's last published NamedValues with the given <paramref name="nvs"/> contents.</summary>
            void IANSourceBase.SetNamedValues(INamedValueSet nvs, NamedValueMergeBehavior mergeBehavior)
            {
                Logger.Trace.Emit("SetNamedValues({0}, {1}) called", nvs.SafeToStringSML(), mergeBehavior);

                INamedValueSet entryNVS = ANState.NamedValues;

                if (mergeBehavior == NamedValueMergeBehavior.Replace)
                    ANState.NamedValues = nvs;
                else
                    ANState.NamedValues = ANState.NamedValues.MergeWith(nvs, mergeBehavior: mergeBehavior);

                CloneAndPublishANStateToManager();

                if (!entryNVS.Equals(ANState.NamedValues))
                    Logger.Debug.Emit("NamedValues changed to {0}", ANState.NamedValues.SafeToStringSML());
            }

            /// <summary>
            /// Action factory method.  When the returned action is run it will attempt to select and apply the Acknowledge recovery action to this annunciator.  
            /// If the ANSignalState is OnAndWaiting then it will be set to Off by running this action as if the decision authority had directly selected this recovery action.
            /// </summary>
            /// <param name="onlyIfOnAndWaiting">
            /// Pass this parameter as true to select that the action shall only attempt to acknowledge the annunciator if it is already in an OnAndWaiting state (so as to avoid producing error messages).  
            /// If this parameter is not selected then the action will fail normally when the annunciator is not already in the appropriate state.  Defaults to true.
            /// </param>
            /// <remarks>
            /// Use of this action allows the annunciator source to directly clear the visibility state of these types of annunciators.  
            /// This pattern is typically done when an existing behavior in the consolidated cleanup code in the annunciator source part is tasked with clearing the visibility state of all annunciators for which it is responsible.
            /// </remarks>
            IClientFacet IANAcknowledgeableSourceBase.Acknowledge(bool onlyIfOnAndWaiting)
            {
                return ParentPart.CreateSetSelectedActionNameAction(ANSpec.ANName, Constants.Acknowledge, onlyIfOnAndWaiting: onlyIfOnAndWaiting);
            }

            /// <summary>Posts and immediately clears this annunciator</summary>
            void IANOccurrence.SignalOccurrence(string reason)
            {
                ANState.ActionList = roAckReadyNVS;
                SetSignalState(ANSignalState.OnAndWaiting, reason, true);
            }

            private static readonly INamedValueSet roAckReadyNVS = new NamedValueSet() { { Constants.Acknowledge, true } }.ConvertToReadOnly();
            private static readonly INamedValueSet roAckNotReadyNVS = new NamedValueSet() { { Constants.Acknowledge, false } }.ConvertToReadOnly();

            /// <summary>
            /// Used by the client to indicate that this ANAlarm is asserting its condition with the given reason explaning why it is being asserted.
            /// The client may call this method occasionally to update the reason if the underlying reason for the condition may change over time before it has been resolved.
            /// It is not recomended to call this method repeatedly and high rate use of this method may cause some reported reasons to be discarded/filtereted out.
            /// </summary>
            void IANCondition.Set(string reason)
            {
                bool useActionDenyReason = IANConditionBehavior.IsSet(E041.IANConditionBehavior.UseActionDenyReason) && reason.IsNeitherNullNorEmpty();

                lock (mutex)
                {
                    bool changeAnSignalState = (ANState.ANSignalState != ANSignalState.On);

                    bool actionListSetNeeded = false;
                    INamedValueSet actionList = roAckNotReadyNVS;

                    if (useActionDenyReason)
                    {
                        actionList = ANState.ActionList;
                        var nvAcknowledge = actionList[Constants.Acknowledge];

                        actionListSetNeeded = (changeAnSignalState
                                                  || nvAcknowledge.VC.cvt != ContainerStorageType.A
                                                  || actionList.Count != 1
                                                  || (nvAcknowledge.VC.GetValue<string>(rethrow: false) != reason && (AcceptAlarmReasonChangeAfterTimeSpan == TimeSpan.Zero || ANState.TimeStamp.Age >= AcceptAlarmReasonChangeAfterTimeSpan))
                                                 );

                        if (actionListSetNeeded)
                            actionList = new NamedValueSet() { { Constants.Acknowledge, reason } }.ConvertToReadOnly();
                    }
                    else
                    {
                        actionListSetNeeded = (changeAnSignalState || ANState.ActionList != actionList);
                    }

                    bool reasonChangeNeeded = (!changeAnSignalState && !actionListSetNeeded && ANState.Reason != reason && (AcceptAlarmReasonChangeAfterTimeSpan == TimeSpan.Zero || ANState.TimeStamp.Age >= AcceptAlarmReasonChangeAfterTimeSpan));

                    if (changeAnSignalState || actionListSetNeeded || reasonChangeNeeded)
                    {
                        ANState.ActionList = actionList;
                        SetSignalState(ANSignalState.On, reason, true);
                    }
                }
            }

            /// <summary>
            /// Used by the client to indicate that this ANAlarm is no longer asserting its condition along with the reason that the source believes explains how the condition was resolved.
            /// Use of this method will only produce a single state change for this source and repeated use of this method will be ignored once the ConditionState is already false.
            /// This method will cause the annunciator to enable its Acknoweldge action and thus allow the signaling state to be cleared using that action.  
            /// Acknowledge action requests are processed by the ANManagerPart without direct use of this interface.
            /// </summary>
            void IANCondition.Clear(string reason)
            {
                lock (mutex)
                {
                    if (ANState.ANSignalState == ANSignalState.On)
                    {
                        ANState.ActionList = roAckReadyNVS;
                        SetSignalState(ANSignalState.OnAndWaiting, reason, true);
                    }

                    InnerServiceAcknowledgeActionSelected();
                }
            }

            /// <summary>
            /// This method dispatches between calling SetAlarm(reason) and ClearAlarm(reason) based on the given alarmState value.
            /// </summary>
            void IANCondition.Service(bool alarmState, string reason)
            {
                if (alarmState)
                    ((IANCondition)this).Set(reason);
                else
                    ((IANCondition)this).Clear(reason);
            }

            /// <summary>Returns true if the Set method was used last and false if the Clear method was used last.  Equivalent to (ANState.ANSignalState == ANSignalState.On)</summary>
            bool IANCondition.ConditionState
            {
                get
                {
                    return (ANState.ANSignalState == ANSignalState.On);
                }
            }

            IANConditionBehavior IANCondition.Behavior { get { return IANConditionBehavior; } set { IANConditionBehavior = value; } }

            public IANConditionBehavior IANConditionBehavior { get; set; }

            #endregion

            #region internals

            /// <summary>
            /// For ANSources that make use of the internally defined roAckReadyNVS INamedValueSet, this method will automatically process any selected Action as an "Acknowledge"
            /// and will directly clear the signaling state of the annunciator (set it to the Off state).
            /// </summary>
            private void InnerServiceAcknowledgeActionSelected()
            {
                if (ANState.ANSignalState == ANSignalState.OnAndWaiting && ANState.ActionList == roAckReadyNVS && !ANState.SelectedActionName.IsNullOrEmpty())
                {
                    string action = ANState.SelectedActionName;

                    // we treat all SelectedActionNames as Acknowledge.

                    ANState.ActionList = roAckNotReadyNVS;
                    ANState.SelectedActionName = null;

                    SetSignalState(ANSignalState.Off, "{0} action completed".CheckedFormat(action), true);
                }
            }

            private void SetSignalState(ANSignalState anSignalState, string reason, bool publish)
            {
                ANSignalState entrySignalState = ANState.ANSignalState;

                ANState.ANSignalState = anSignalState;
                ANState.Reason = reason;
                ANState.SeqAndTimeInfo = new ANSeqAndTimeInfo() { TimeStamp = QpcTimeStamp.Now, DateTime = DateTime.Now };

                if (publish)
                {
                    string actionStr = ((anSignalState.IsActionActive() || !ANState.ActiveActionName.IsNullOrEmpty()) ? " action:'{0}'".CheckedFormat(ANState.ActiveActionName) : "");

                    if (LastSourceGeneratedState != null)
                    {
                        if (LastSourceGeneratedState.ANSignalState != anSignalState)
                            SetSignalStateEmitter.Emit("State changed to {0}, '{1}' {2}{3} [from state:{4}]", anSignalState, reason, ANState.ActionList.ToString(false, true), actionStr, LastSourceGeneratedState.ANSignalState);
                        else if (LastSourceGeneratedState.Reason != reason)
                            SetSignalStateEmitter.Emit("Reason changed {0}, '{1}' {2}{3}", anSignalState, reason, ANState.ActionList.ToString(false, true), actionStr);
                    }
                    else
                    {
                        SetSignalStateEmitter.Emit("Initial state id {0}, '{1}' {2}{3}", anSignalState, reason, ANState.ActionList.ToString(false, true), actionStr);
                    }

                    CloneAndPublishANStateToManager();
                }

                string currentSelectedActionName = ANState.SelectedActionName;
                bool currentActionAbortRequest = ANState.ActionAbortRequested;

                if (!currentSelectedActionName.IsNullOrEmpty() && (anSignalState != ANSignalState.OnAndWaiting && anSignalState != ANSignalState.OnAndActionActive))
                {
                    ANState.SelectedActionName = null;
                    Logger.Debug.Emit("State change to '{0}' has discarded an unexpected, or un-handled, SelectedActionName:'{1}'", anSignalState, currentSelectedActionName);
                }

                if (currentActionAbortRequest && (anSignalState != ANSignalState.OnAndActionActive))
                {
                    ANState.ActionAbortRequested = false;
                    Logger.Debug.Emit("State change to '{0}' has discarded an un-handled ActionAbortRequest", anSignalState);
                }
            }

            private void CloneAndPublishANStateToManager()
            {
                ANState immutableANStateCopy = new ANState(ANState);
                ManagersImmutableANStatePublicationDelegate(immutableANStateCopy);
                LastSourceGeneratedState = immutableANStateCopy;
            }

            #endregion
        }

        ActionLogging syncActionLogging = null;

        /// <summary>
        /// Method used by ANSourceImpl Sync methods to create (and then run) Sync actions.
        /// </summary>
        internal IBasicAction CreateSyncActionForSource(SyncFlags flags)
        {
            syncActionLogging = syncActionLogging ?? new ActionLogging(ActionLoggingReference) { Config = ActionLoggingConfig.Trace_Trace_Trace_Trace };

            return new BasicActionImpl(actionQ, ipf => PerformSync(ipf, flags), "Sync for AN source".CheckedFormat(flags), syncActionLogging, flags.ToString());
        }

        #endregion

        #region SetupSetRegistrationAndUnregistration

        private void SetupSetRegistrationAndUnregistration()
        {
            AddMainThreadStartingAction(() => 
            {
                anStateCurrentActiveSet = new ReferenceSet<ANState>(new SetID("{0}.ANStateCurrentlyActiveSet".CheckedFormat(PartID)), ConfigValues.ANStateCurrentlyActiveSetMaxCount, registerSelfWithSetsInstance: ISI);
                anStateRecentlyClearedSet = new ReferenceSet<ANState>(new SetID("{0}.ANStateRecentlyClearedSet".CheckedFormat(PartID)), ConfigValues.ANStateRecentlyClearedSetMaxCount, registerSelfWithSetsInstance: ISI);
                anStateHistorySet = new ReferenceSet<ANState>(new SetID("{0}.ANStateHistorySet".CheckedFormat(PartID)), ConfigValues.ANStateHistorySetMaxCount, registerSelfWithSetsInstance: ISI);
            });

            AddMainThreadStoppingAction(() =>
            {
                anStateCurrentActiveSet.UnregisterSelf();
                anStateRecentlyClearedSet.UnregisterSelf();
                anStateHistorySet.UnregisterSelf();
            });
        }

        #endregion

        #region PerformMainLoopService method

        /// <summary>Timer used to trigger re-evaluation of any autoAction triggering logic.  Triggers every 1 second.</summary>
        QpcTimer autoActionServiceTimer = new QpcTimer() { TriggerIntervalInSec = 1.0, AutoReset = true, Started = true };
        /// <summary>Timer used to trigger age based pruning of the recently cleared set.  Triggers every 10 seconds.</summary>
        QpcTimer setPruningServiceTimer = new QpcTimer() { TriggerIntervalInSec = 10.0, AutoReset = true, Started = true };

        /// <summary>
        /// Main service method.  
        /// Services Config (fielding updates to changable keys).
        /// Services the anSource objects that need to be serviced by this part (IANOccurrence and IANCondition types).
        /// Services the ANState queue of state changes from sources that need to be published.
        /// Services the configurable auto acknowledge logic.
        /// Services the time based removal of items from the RecentlyCleared set.
        /// </summary>
        protected override void PerformMainLoopService()
        {
            ServiceConfig();

            ServiceALIDLookups();

            // service all of the sources in the anSourceServiceArray (derived from the anSourceServiceList).
            foreach (ANSourceImpl anSource in anSourceServiceList.Array)
            {
                anSource.Service();
            }

            ProcessQueuedANStateUpdates();

            if (autoActionServiceTimer.IsTriggered)
            {
                ProcessAutoAcknowledgeSelections();
            }

            if (setPruningServiceTimer.IsTriggered)
            {
                if (ConfigValues.ANStateRecentlyClearedSetMaxRetentionTimeSpan != TimeSpan.Zero)
                {
                    QpcTimeStamp now = QpcTimeStamp.Now;

                    anStateRecentlyClearedSet.RemoveAll((anState) => ((now - anState.TimeStamp) >= ConfigValues.ANStateRecentlyClearedSetMaxRetentionTimeSpan));
                }
            }

            ServiceEventDelivery();
            ServicePendingSyncActions();
        }

        #endregion

        #region incomming pathway for ANState updates and related ProcessQueuedANStateUpdates service method

        /// <summary>
        /// Delegate type used to inject means for ANSourceImpl objects to enqueue the immutable copy of there ANState object back to the manager for processing.
        /// </summary>
        internal delegate void ImmutableANStatePublicationDelegate(ANState immutableANState);

        /// <summary>
        /// Retained copy of the AsyncAcceptANStateUpdate method as a delegate that is then shared by all ANSourceImpl objects.
        /// </summary>
        private ImmutableANStatePublicationDelegate immutableANStatePublicationDelegate;

        /// <summary>
        /// This method is used to accept asynchronous calls to pass newly updated anState values back into this AnnunciatorManagerPart.
        /// The given IANState values must have already been copied and must thus effectively be immutable.
        /// </summary>
        private void AsyncAcceptANStateUpdate(ANState immutableANState)
        {
            lock (anStateQueueMutex)
            {
                anStateQueue.Enqueue(immutableANState);
                anStateQueueCount = anStateQueue.Count;
            }

            // wakeup this part.
            this.Notify();
        }

        private Queue<ANState> anStateQueue = new Queue<ANState>();
        private volatile int anStateQueueCount;
        private readonly object anStateQueueMutex = new object();

        List<ANState> anStateWorkingList = new List<ANState>();

        private ulong anSeqNumSource = 0;

        /// <summary>
        /// Pulls and processes all of the recently queued ANState update records.  
        /// Implements publication, use of E30ALIDHandlerFacet, and maintenance of currently active, recently cleared, and history ANState Sets.
        /// </summary>
        private void ProcessQueuedANStateUpdates()
        {
            if (anStateQueueCount != 0)
            {
                lock (anStateQueueMutex)
                {
                    while (anStateQueue.Count > 0)
                    {
                        anStateWorkingList.Add(anStateQueue.Dequeue());
                    }

                    anStateQueueCount = anStateQueue.Count;
                }

                foreach (ANState anState in anStateWorkingList)
                {
                    if (anState == null)
                        continue;

                    if (anState.SeqAndTimeInfo.SeqNum == 0)
                        anState.SeqAndTimeInfo = new ANSeqAndTimeInfo(anState.SeqAndTimeInfo, ref anSeqNumSource);

                    ANSourceTracking anSourceTracking = FindAnSourceTrackingFromANState(anState);

                    if (anSourceTracking != null && anSourceTracking.anSourceImpl != null)
                    {
                        /// process the items from the ANState queue...

                        ANState lastServicedANState = anSourceTracking.lastServicedANState ?? anSourceTracking.initialANState;

                        bool isEqual = (anState.IsEqualTo(lastServicedANState));
                        bool isInitialState = (anSourceTracking.lastServicedANState == null);
                        bool isOffToOnTransition = (isInitialState || anSourceTracking.lastServicedANState.ANSignalState == ANSignalState.Off && anState.ANSignalState != ANSignalState.Off);

                        if (isOffToOnTransition)
                            anState.LastTransitionToOnSeqAndTimeInfo = anState.SeqAndTimeInfo;
                        else
                            anState.LastTransitionToOnSeqAndTimeInfo = lastServicedANState.LastTransitionToOnSeqAndTimeInfo;

                        if (!isEqual || isInitialState)
                        {
                            bool signalingStateHasJustBeenCleared = lastServicedANState.IsSignaling && !anState.IsSignaling;
                            bool signalingStateHasJustBeenSet = !lastServicedANState.IsSignaling && anState.IsSignaling;
                            bool signalingStateIsSetAndLookupJustCompleted = anState.IsSignaling && lastServicedANState.ALIDLookupState == ALIDLookupState.Pending && anState.ALIDLookupState == ALIDLookupState.Found; 

                            // set and publish the lastServicedANState
                            anSourceTracking.Publish(anSourceTracking.lastServicedANState = anState);

                            if (!isInitialState)
                            {
                                anStateHistorySet.Add(anState);
                            }

                            if (anState.ANSignalState.IsSignaling())
                            {
                                anStateCurrentActiveSet.RemoveAndAdd((an) => (an.ANName == anState.ANName), anState);
                                anStateRecentlyClearedSet.RemoveAll((an) => (an.ANName == anState.ANName));
                            }
                            else if (signalingStateHasJustBeenCleared)
                            {
                                anStateRecentlyClearedSet.RemoveAndAdd((an) => (an.ANName == anState.ANName), anState);
                                anStateCurrentActiveSet.RemoveAll((an) => (an.ANName == anState.ANName));
                            }

                            if ((anState.ALIDLookupState == ALIDLookupState.Defined || anState.ALIDLookupState == ALIDLookupState.Found) && (E30ALIDHandlerFacet != null))
                            {
                                if (signalingStateHasJustBeenSet || signalingStateIsSetAndLookupJustCompleted)
                                    E30ALIDHandlerFacet.NoteALIDValueChanged((int)anState.ALID, true, anState.Reason);
                                else if (signalingStateHasJustBeenCleared)
                                    E30ALIDHandlerFacet.NoteALIDValueChanged((int)anState.ALID, false, anState.Reason);
                            }

                            anSourceTracking.sendStateEvent = true;
                            lastStateSeqNum = anSourceTracking.lastEventSeqNum = GenerateNextEventSeqNum();
                        }
                        else
                        {
                            Log.Debug.Emit("Received unexpected duplicate state {0} (ignored).", anState);
                        }
                    }
                    else
                    {
                        Log.Debug.Emit("Received unexpected queued state {0}.  No corresponding registered source found.  Ignored", anState);
                    }
                }

                anStateWorkingList.Clear();
            }
        }

        /// <summary>
        /// Attempts to make use of the given anState's anSpec to obtain the table index of the corresponding source.  
        /// Otherwise attempts to lookup the source using the given anState.Name property.  
        /// Returns the corresponding ANSourceTracking object (if one was found), or null (if neither index nor name could be used to find the source)
        /// </summary>
        private ANSourceTracking FindAnSourceTrackingFromANState(ANState anState)
        {
            return anSourceTrackingList.Array.SafeAccess(anState.ANSpec.ANManagerTableRegistrationNum - 1) ?? FindANSourceTrackingByName(anState.ANName, false);
        }

        #endregion

        #region other PerformMainLoopServce support methods: ProcessAutoAcknowledgeSelections, PredicateIsANStateAckable

        long anStateCurrentActiveSetSeqNum = 0;
        int numAckableWaitingItems = 0;

        /// <summary>
        /// Keeps track of the ANStates in the anStateCurrentActiveSet, tracking the group of them that are IsANStateAckable.
        /// For each such ackable ANState, determines if the corresponding autoAcknowledge has been enabled and if the required time has ellapsed in the OnAndWaiting state
        /// in which case this method will attempt to select the matching "Acknowledge" action.
        /// </summary>
        private void ProcessAutoAcknowledgeSelections()
        {
            if (anStateCurrentActiveSetSeqNum != anStateCurrentActiveSet.SeqNum || numAckableWaitingItems != 0)
            {
                bool autoAcknowledgeInformationIsEnabled = (ConfigValues.AutoAcknowledgeInformationAfterTimeSpan != TimeSpan.Zero);
                bool autoAcknowledgeWarningIsEnabled = (ConfigValues.AutoAcknowledgeWarningAfterTimeSpan != TimeSpan.Zero);
                bool autoAcknowledgeAlarmIsEnabled = (ConfigValues.AutoAcknowledgeAlarmAfterTimeSpan != TimeSpan.Zero);
                bool autoAcknowledgeErrorIsEnabled = (ConfigValues.AutoAcknowledgeErrorAfterTimeSpan != TimeSpan.Zero);

                anStateCurrentActiveSetSeqNum = anStateCurrentActiveSet.SeqNum;

                IEnumerable<ANState> ackableItemEnumeration = anStateCurrentActiveSet.Where((anState) => PredicateIsANStateAckable(anState));

                numAckableWaitingItems = 0;
                foreach (ANState anState in ackableItemEnumeration)
                {
                    string ackActionName = anState.ActionList[0].Name;
                    ANSourceTracking anSourceTracking = FindAnSourceTrackingFromANState(anState);

                    if (!ackActionName.IsNullOrEmpty() && anSourceTracking != null)
                    {
                        TimeSpan anStateAge = anState.TimeStamp.Age;

                        switch (anSourceTracking.anSpec.ANType)
                        {
                            case ANType.Attention:
                                    if (!autoAcknowledgeInformationIsEnabled)
                                    { }
                                    else if (anStateAge < ConfigValues.AutoAcknowledgeInformationAfterTimeSpan)
                                        numAckableWaitingItems++;
                                    else
                                        anSourceTracking.anSourceImpl.ProcessSetSelectActionNameRequest(ackActionName);
                                    break;
                                    
                            case ANType.Warning:
                                    if (!autoAcknowledgeWarningIsEnabled)
                                    { }
                                    else if (anStateAge < ConfigValues.AutoAcknowledgeWarningAfterTimeSpan)
                                        numAckableWaitingItems++;
                                    else
                                        anSourceTracking.anSourceImpl.ProcessSetSelectActionNameRequest(ackActionName);
                                    break;

                            case ANType.Alarm:
                                    if (!autoAcknowledgeAlarmIsEnabled)
                                    { }
                                    else if (anStateAge < ConfigValues.AutoAcknowledgeAlarmAfterTimeSpan)
                                        numAckableWaitingItems++;
                                    else
                                        anSourceTracking.anSourceImpl.ProcessSetSelectActionNameRequest(ackActionName);
                                    break;

                            case ANType.Error:
                                    if (!autoAcknowledgeErrorIsEnabled)
                                    { }
                                    else if (anStateAge < ConfigValues.AutoAcknowledgeErrorAfterTimeSpan)
                                        numAckableWaitingItems++;
                                    else
                                        anSourceTracking.anSourceImpl.ProcessSetSelectActionNameRequest(ackActionName);
                                    break;

                            default:
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if the given anState signaling state is OnAndWaiting, SelectedActionName is null or empty, ActionList is one element long and its "Acknowledge" or "Ack" named value is true.
        /// </summary>
        private bool PredicateIsANStateAckable(ANState anState)
        {
            return (anState.ANSignalState == ANSignalState.OnAndWaiting
                    && anState.SelectedActionName.IsNullOrEmpty()
                    && anState.ActionList.Count == 1
                    && (anState.ActionList.GetValue(Constants.Acknowledge).GetValue<bool>(false) 
                        || anState.ActionList.GetValue(Constants.Ack).GetValue<bool>(false))
                    );
        }

        #endregion

        #region Sync Support (PerformSync, ServicePendingSyncActions)

        private string PerformSync(IProviderFacet ipf, SyncFlags flags)
        {
            PerformMainLoopService();

            if (flags == SyncFlags.None)
                return string.Empty;

            var syncTracker = new SyncTracker() { syncEventSeqNum = GenerateNextEventSeqNum(), ipf = ipf, syncFlags = flags, waitingForEventDelivery = flags.IsSet(SyncFlags.ANEventDelivery) };

            pendingSyncTrackerList.Add(syncTracker);

            if (syncTracker.waitingForEventDelivery)
            {
                syncItemsArWaitingForEventDelivery = true;

                ServiceEventDelivery();

                this.Notify();  // request that the part runs the next loop quickly after returning.
            }

            return null;
        }

        public void ServicePendingSyncActions()
        {
            if (pendingSyncTrackerList.Count == 0)
            {
                if (syncItemsArWaitingForEventDelivery)
                    syncItemsArWaitingForEventDelivery = false;

                return;
            }

            bool nextSyncItemsArWaitingForEventDelivery = false;

            foreach (var pendingSyncItem in pendingSyncTrackerList.Array)
            {
                if (pendingSyncItem.waitingForEventDelivery)
                {
                    if (pendingSyncItem.syncEventSeqNum < lastDeliveredEventListSeqNum)
                    {
                        pendingSyncItem.waitingForEventDelivery = false;
                    }
                    else if (anEventListHandlerPart == null)
                    {
                        pendingSyncItem.waitingForEventDelivery = false;
                        pendingSyncItem.resultCode = pendingSyncItem.resultCode.MapNullOrEmptyTo("This part has not been configured with an ANEventInfoHandlerPart");
                    }
                    else if (!anEventListHandlerPartBaseState.IsOnline)
                    {
                        pendingSyncItem.waitingForEventDelivery = false;
                        pendingSyncItem.resultCode = pendingSyncItem.resultCode.MapNullOrEmptyTo("The configured ANEventInfoHandlerPart is not online [{0}]".CheckedFormat(anEventListHandlerPartBaseState.ToString(Modular.Part.BaseState.ToStringSelect.PartID | Modular.Part.BaseState.ToStringSelect.UseState | Modular.Part.BaseState.ToStringSelect.Reason)));
                    }
                }

                nextSyncItemsArWaitingForEventDelivery |= pendingSyncItem.waitingForEventDelivery;

                if (!pendingSyncItem.IsStillWaiting)
                {
                    pendingSyncItem.ipf.CompleteRequest(pendingSyncItem.resultCode.MapNullToEmpty());
                    pendingSyncTrackerList.Remove(pendingSyncItem);
                }
            }

            syncItemsArWaitingForEventDelivery = nextSyncItemsArWaitingForEventDelivery;
        }

        private class SyncTracker
        {
            public ulong syncEventSeqNum;
            public IProviderFacet ipf;
            public SyncFlags syncFlags;
            public bool waitingForEventDelivery;
            public string resultCode;

            public bool IsStillWaiting { get { return waitingForEventDelivery; } }
        }

        IListWithCachedArray<SyncTracker> pendingSyncTrackerList = new IListWithCachedArray<SyncTracker>();
        bool syncItemsArWaitingForEventDelivery = false;

        void SyncTracking_NoteFailedEventDelivery(string resultCode)
        {
            foreach (var pendingSyncTracker in pendingSyncTrackerList.Array)
            {
                if (pendingSyncTracker.waitingForEventDelivery && pendingSyncTracker.resultCode.IsNullOrEmpty())
                    pendingSyncTracker.resultCode = resultCode;
            }
        }

        #endregion

        #region ANEventInfo scanning and delivery (ServiceEventDelivery, SetupANEventDelivery)

        /// <summary>Gives the last sequence number that was assigned to an ANSourceTracker's lastEventSeqNum value.</summary>
        ulong lastStateSeqNum = 0;

        private void ServiceEventDelivery()
        {
            if (anEventHandlerICF != null && weAreWaiting)
            {
                var actionState = anEventHandlerICF.ActionState;
                if (!actionState.IsComplete)
                    return;

                if (actionState.Failed && syncItemsArWaitingForEventDelivery)
                    SyncTracking_NoteFailedEventDelivery(actionState.ResultCode);

                weAreWaiting = false;
                pendingDeliverEventList.Clear();
                lastDeliveredEventListSeqNum = pendingDeliverEventListSeqNum;
            }

            if (!syncItemsArWaitingForEventDelivery && lastStateSeqNum <= lastDeliveredEventListSeqNum)
                return;

            anEventListHandlerPartBaseState = (anEventListHandlerPart != null) ? anEventListHandlerPart.BaseState : MosaicLib.Modular.Part.BaseState.None;

            if (!anEventListHandlerPartBaseState.IsOnline)
                return;

            pendingDeliverEventListSeqNum = GenerateNextEventSeqNum();

            foreach (var anSourceTracker in anSourceTrackingList.Array)
            {
                if (anSourceTracker.sendRegistrationEvent)
                {
                    pendingDeliverEventList.Add(new ANEventInfo() { ANEventType = ANEventType.Registration, ANState = anSourceTracker.lastServicedANState ?? anSourceTracker.initialANState });
                    anSourceTracker.sendRegistrationEvent = false;
                    anSourceTracker.sendStateEvent = false;
                }
                else if (anSourceTracker.sendStateEvent || anSourceTracker.lastEventSeqNum >= lastDeliveredEventListSeqNum)
                {
                    pendingDeliverEventList.Add(new ANEventInfo() { ANEventType = ANEventType.State, ANState = anSourceTracker.lastServicedANState ?? anSourceTracker.initialANState });
                    anSourceTracker.sendStateEvent = false;
                }
            }

            if (pendingDeliverEventList.Count > 0)
            {
                anEventHandlerICF.Start();
                weAreWaiting = true;
            }
            else
            {
                lastDeliveredEventListSeqNum = pendingDeliverEventListSeqNum;
            }
        }

        private void SetupANEventDelivery()
        {
            anEventListHandlerPart = ANManagerPartConfig.ANEventInfoListHandlerPart;
            anEventListHandlerPartBaseState = (anEventListHandlerPart != null) ? anEventListHandlerPart.BaseState : MosaicLib.Modular.Part.BaseState.None;
            anEventHandlerICF = (anEventListHandlerPart != null) ? anEventListHandlerPart.ProcessANEventList(pendingDeliverEventList) : null;
        }

        IANEventListHandlerPart anEventListHandlerPart;
        IBaseState anEventListHandlerPartBaseState = MosaicLib.Modular.Part.BaseState.None;
        IClientFacet anEventHandlerICF;
        bool weAreWaiting = false;
        ulong pendingDeliverEventListSeqNum = 0;
        List<ANEventInfo> pendingDeliverEventList = new List<ANEventInfo>();
        ulong lastDeliveredEventListSeqNum = 0;

        #endregion
    }

    /// <summary>
    /// This exception class may be thrown by the various Register ANSource method in casses where they finds that the anSpec they have been given is not valid or produces a redundant registration attempt.
    /// </summary>
    public class ANRegistrationException : System.Exception
    {
        /// <summary>Constructor.  requires a message.</summary>
        public ANRegistrationException(string message)
            : base(message, null)
        { }
    }

    #endregion

    #region Helper classes (ThresholdTool, ANConditionDebounceTool)

    namespace Tools
    {
        /// <summary>
        /// This class is used to implement a form of generalized threshold test with configurable hysteresis.
        /// The client configures this object's Threshold and Hysteresis (or PossitiveHysteresis and NegativeHysteresis seperately)
        /// and then uses the Service(value) method allow this object to track the incomming value and to perform
        /// the thresholding logic with optional hysteresis on it to service and update the CurrentState as true or false
        /// based on whether the serviced value was last seen to be at or above Threshold + Hysteresis/PossitiveHysteresis or
        /// if it was last seen to be below Threshold - Hysteresis/NegativeHysteresis.
        /// </summary>
        public class ThresholdTool
        {
            /// <summary>Defines the nominal input value level at or above which this object's state becomes true and below which this object's state becomes false (subject to additional hysteresis)</summary>
            public double Threshold { get; set; }

            /// <summary>Set only property can be used to assign both the PossitiveHysteresis and NegativeHysteresis values at the same time.</summary>
            public double Hysteresis { set { PossitiveHysteresis = NegativeHysteresis = value; } }

            /// <summary>When set to a possitive number, this value defines a possitive offset to the nominal Threshold value that is used then determining if the inputValue is sufficient to change the object's state from false to true.</summary>
            public double PossitiveHysteresis { get { return possitiveHysteresis; } set { possitiveHysteresis = Math.Max(0.0, value); } }
            private double possitiveHysteresis = 0.0;

            /// <summary>When set to a possitive number, this value defines a negative offset to the nominal Threshold value that is used then determining if the inputValue is sufficient to change the object's state from true to false.</summary>
            public double NegativeHysteresis { get { return negativeHysteresis; } set { negativeHysteresis = Math.Max(0.0, value); } }
            private double negativeHysteresis = 0.0;

            /// <summary>
            /// Gives the accumulated resulting state resulting from all prior Service calls, especially the last one.  
            /// This defaults to false.  
            /// When false it is set to true by the Service method when the inputValue is at or above the Threshold + PossitiveHysteresis.
            /// When true it is set to false by the Service method when the inputValue is below the Threshold - NegativeHysteresis.
            /// </summary>
            public bool CurrentState { get; set; }

            /// <summary>Implicit cast to bool.  Returns the CurrentState of the given ThresholdTool o.</summary>
            public static implicit operator bool(ThresholdTool thesholdTool)
            {
                return thesholdTool.CurrentState;
            }

            /// <summary>
            /// Service method.  Accepts the next inputValue and updates the CurrentState based on the prior CurrentState.
            /// When the CurrentState is false, this method sets it true if the inputValue is at or above the Threshold + PossitiveHysteresis.
            /// When the CurrentState is true, this method sets it to false if the inputValue is below the Threshold - NegativeHysteresis.
            /// <para/>Supports call chaining.
            /// </summary>
            public ThresholdTool Service(double inputValue)
            {
                if (CurrentState)
                {
                    if (inputValue < (Threshold - NegativeHysteresis))
                        CurrentState = false;
                }
                else
                {
                    if (inputValue >= (Threshold + PossitiveHysteresis))
                        CurrentState = true;
                }

                return this;
            }
        }

        /// <summary>
        /// IANCondition helper tool for use in debouncing an external input using time and/or count based confirmation.
        /// This tool is generally configured with a confirmation period and/or count and then its Service method is given a
        /// series of inputValues which this tool tracks.  Each time the inputValue is stable for the required period and for the
        /// required number of confirmation iterations (additional Service calls where the next inputValue matched the prior one)
        /// then the Service method will change the IANCondition state to the newly debounced value if it is not already at that value.
        /// </summary>
        public class ANConditionDebounceTool
        {
            /// <summary>
            /// Constructor.  Caller must provide the IANCondition instance that this tool will be used with.
            /// ConfirmationPeriod defaults to 0.1 and ConfirmationServiceCount defaults to 0
            /// </summary>
            public ANConditionDebounceTool(IANCondition anCondition)
            {
                ANCondition = anCondition;
                ConditionSourceName = "{0}.Source".CheckedFormat(ANCondition.ANSpec.ANName);

                ConfirmationPeriod = TimeSpan.FromSeconds(0.1);
                ConfirmationCount = 0;
            }

            /// <summary>Gives the IANContiion instance that this tool is being used with</summary>
            public IANCondition ANCondition { get; private set; }

            /// <summary>Gives the condition source name that will be used in the reason when Setting and/or Clearing the IANCondition state.</summary>
            public string ConditionSourceName { get; set; }

            /// <summary>Set only property.  Used to assign both the SetConfirmationPeriod and the ClearConfirationPeriod at the same time.</summary>
            public TimeSpan ConfirmationPeriod { set { SetConfirmationPeriod = ClearConfirmationPeriod = value; } }
            /// <summary>When non-zero and possitive, this value defines the period of time that must be observed to elapse in sequential Service(true) calls before the IANCondition's state can/will be Set.</summary>
            public TimeSpan SetConfirmationPeriod { get; set; }
            /// <summary>When non-zero and possitive, this value defines the period of time that must be observed to elapse in sequential Service(false) calls before the IANCondition's state can/will be Cleared.</summary>
            public TimeSpan ClearConfirmationPeriod { get; set; }

            /// <summary>Set only property.  Used to assign both the SetConfirmationCount and the ClearConfirationCount at the same time.</summary>
            public int ConfirmationCount { set { SetConfirmationCount = ClearConfirmationCount = value; } }
            /// <summary>When non-zero and possitive, this value defines the number of additional Service(true) calls that must be made sequentally before the IANCondition's state can/will be Set.</summary>
            public int SetConfirmationCount { get; set; }
            /// <summary>When non-zero and possitive, this value defines the number of additional Service(false) calls that must be made sequentally before the IANCondition's state can/will be Cleared.</summary>
            public int ClearConfirmationCount { get; set; }

            /// <summary>
            /// Getter returns the current state of the ANCondition object.  
            /// Setter changes the state of the ANCondition to match the given value and internal state is reset to the state as if the input just changed to the given value.</summary>
            public bool CurrentState 
            { 
                get { return ANCondition.ConditionState; }
                set
                {
                    if (value && !ANCondition.ConditionState)
                        ANCondition.Set("Set by explicit client request");
                    else if (!value && ANCondition.ConditionState)
                        ANCondition.Clear("Cleared by explicit client request");

                    lastServicedValue = value;
                    lastServicedValueTimeStamp.SetToNow();
                    lastServicedValueConfirmCount = 0;
                }
            }

            /// <summary>Implicit cast to bool.  Returns the CurrentState of the given ConditionDebounceTool condition.</summary>
            public static implicit operator bool(ANConditionDebounceTool condition) 
            {
                return condition.CurrentState;
            }

            /// <summary>
            /// Service method.  
            /// Applies time and count based confirmation logic to series of 1 or more calls with the same inputValue and when both time and confirmation count conditions are met
            /// it will change the IANCondition's State to the inputValue if it is not already equal.
            /// <para/>Supports call chaining.
            /// </summary>
            public ANConditionDebounceTool Service(bool inputValue)
            {
                Service(inputValue, QpcTimeStamp.Now);

                return this;
            }

            /// <summary>
            /// Service method.  
            /// Applies time and count based confirmation logic to series of 1 or more calls with the same inputValue and when both time and confirmation count conditions are met
            /// it will change the IANCondition's State to the inputValue if it is not already equal.
            /// <para/>Supports call chaining.
            /// </summary>
            public ANConditionDebounceTool Service(bool inputValue, QpcTimeStamp timeStampNow)
            {
                if (lastServicedValue != inputValue || lastServicedValueTimeStamp.IsZero)
                {
                    lastServicedValue = inputValue;
                    lastServicedValueTimeStamp = timeStampNow;
                    lastServicedValueConfirmCount = 0;
                }
                else
                {
                    if (lastServicedValueConfirmCount < (lastServicedValue ? SetConfirmationCount : ClearConfirmationCount))
                        lastServicedValueConfirmCount++;
                }

                if (lastServicedValue != CurrentState)
                {
                    TimeSpan valueAge = (timeStampNow - lastServicedValueTimeStamp);
                    bool isAgeConfirmed = (valueAge >= (lastServicedValue ? SetConfirmationPeriod : ClearConfirmationPeriod));
                    bool isCountConfirmed = (lastServicedValueConfirmCount >= (lastServicedValue ? SetConfirmationCount : ClearConfirmationCount));

                    if (isAgeConfirmed && isCountConfirmed)
                    {
                        string reason;
                        if (lastServicedValueConfirmCount > 0)
                            reason = "{0} is confirmed {1} after {2:f3} seconds and {3} iterations".CheckedFormat(ConditionSourceName, lastServicedValue ? "set" : "cleared", valueAge.TotalSeconds, lastServicedValueConfirmCount);
                        else
                            reason = "{0} is confirmed {1} after {2:f3} seconds".CheckedFormat(ConditionSourceName, lastServicedValue ? "set" : "cleared", valueAge.TotalSeconds);

                        ANCondition.Service(lastServicedValue, reason);
                    }
                }

                return this;
            }

            bool lastServicedValue = false;
            QpcTimeStamp lastServicedValueTimeStamp = QpcTimeStamp.Zero;
            int lastServicedValueConfirmCount = 0;
        }
    }

    #endregion
}
