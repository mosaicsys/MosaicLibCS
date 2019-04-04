//-------------------------------------------------------------------
/*! @file E090.cs
 *  @brief 
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2017 Mosaic Systems Inc.
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
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.Semi.E090
{
    /*
     * This namespace consists of a set of defintions, Extension Methods, and classes that are used on top of the existing related E039 namespace and concepts to implement E090 centric objects,
     * state, and relationships, especially involving Substrate objects and Substrate Location (SubstLoc) objects.  All of the related object reprersentation and persistance and publication is
     * done using the corresponding existing E039 concepts and use patterns.
     * 
     * This namespace includes a large number of E090 centric helper extension methods and a set of E090 centric information extraction and observer helper objects that provide the standard mapping
     * of E090 terms to E039 behaviors.  
     * 
     * Please note that there is no element of the underlying E039 implementation that is directly intertined with this set of E090 helpers.  The client is welcome to ignore the helper methods and
     * objects that are included here and to use their own E090 centric implemenation as desired on top of the E039 concepts, or separately from them.
     * 
     * The bulk of the extension methods provided here are designed to facilitate common E090 substrate and substrate location centric use patterns.  These consist of an outer set of 
     * methods that are applied to an IE039TableUpdater instance, and a set of corresponding update item generation extenstion methods that are applied to a List{E039UpdateItem} to which the
     * generated update items are added.  Methods are available to Create and Remove substrate and substrate location objects.  By default these methods use the IfNeeded concept so that they can
     * be applied and succeed even if the related objects (usually SubstLoc objects) already exist.  Methods are also available to move substrates, and to change the states of specific substrate
     * and substrate location attributes.  For example the default behavior is that the NoteSubstMoved method will no only update the Contains linkage in the from and to substrate locations
     * but that it will also update the substrate's STS to indicate when the substrate departs from/arrives at its source or destination location(s).
     * 
     * The IE039TableUpdater methods are each built on top of the use of corresponding List{E039UpdateItem} methods.  This allows a client to uses the same underlying attribute and linkage
     * decision tree logic in compositional cases where the client would also like to have other update items included in the execution of a single Update action.
     * 
     * In addition to the EMs used to generate and apply E090 object and state change behaviors, there are a set of classes that can be used by a client to decode, process, and track
     * E090 centric objects.  The E090SubstInfo and E090SubstLocInfo objects (structs) are typically generated in relation to a given E039Object (of the correct type) and serve to extract
     * relevant information from it to simplify the clients use of E039 for these E090 centric purposes.  There are a number of direct predicates and state specific extension methods that
     * may be used by clients to help minimize the glue logic required for standard client side substrate based decision tree logic.
     * 
     * These information decoding objects are then used with (and provided by) the E090SubstLocObserver and E090SubstObserver objects that are the primary means for clients to track
     * the state of a given substrate, or the state of a substrate location, and typically of the object it contains, with minimal client side required glue code.
     * 
     * The combination of the use of IE039TableUpdater.Update(...) related patterns with the related E090 centric extension methods, atomic and temporally consistant publication, and 
     * glue free state observers means that the client code required to use and manipulate E090 centric objects is significantly minimized and simplified so that the client code can stay
     * focused on what it needs this information for, rather than how it gets and decodes it.
     * 
     * In addition to basic E090 concepts like the SubstState (STS) and the SubstProcState (SPS), this namespace introduces a set of parallel concepts:
     * 
     * PseudoSPS values and PendingSPS attribute:  The SubstProcState enumeration used here includes a set of what are known as pseduoSPS values.  These are not part of the E090 standard but
     * there presance allows the client to use the standard call tree and parameter and update flow patterns to perform addionally useful concepts.  These include supporting a more
     * generallized pattern for process steps to record intermediate SPS results as well as a means to (optinally and configurably) accumulate a more useful history of what has been done to a 
     * given substrate than the E090 history concept directly supports.  Please see the related enumeration, structure and EM related defintions for more details.
     * 
     * As with the relationship between E039 and WPF, E090 now includes a related set of WPF helper tools in the WPF centric assembly.  This includes a E090 location tracker object that
     * generates and publishes a combined info object that includes the E090SubstLocInfo for the given location, as well as the E090SubstInfo for the substrate that is currently in that 
     * location (Contains), or when empty, for the substrate that came from (SrcLoc), or goes to (DestLoc) the location.  This tracker object and combined information object generally supports
     * the use of terse location centric binding statements, used with converters, to provide efficient and customizable visualization of the infomation that is typically known about a
     * substrate location and any substrate that it contains or is related to in a source/destination sense.  Again these techniques scale well and work seamlessly in both local and
     * remote user interface situations.
     */

    #region Constants (type names) and Settings

    /// <summary>
    /// Static class used as namespace for E090 specific global constant values.  (primarily string constants such as type names)
    /// </summary>
    public static class Constants
    {
        /// <summary>SubstrateObjectType = "Substrate"</summary>
        public const string SubstrateObjectType = "Substrate";

        /// <summary>SubstrateLocationObjectType = "SubstLoc"</summary>
        public const string SubstrateLocationObjectType = "SubstLoc";

        [Obsolete("not supported yet (2018-05-31)")]
        public const string SubstrateBatchLocationObjectType = "BatchLoc";

        /// <summary>Gives the link key used for links from a substrate location to the substrate it contains, if any (Contains)</summary>
        public const string ContainsLinkKey = "Contains";

        /// <summary>Gives the link key used for links from a substrate to its source substrate location (SrcLoc)</summary>
        public const string SourceLocationLinkKey = "SrcLoc";

        /// <summary>Gives the link key used for links from a substrate to its destination substrate location (DestLoc)</summary>
        public const string DestinationLocationLinkKey = "DestLoc";

        /// <summary>E039 attribute name used for SubstrateProcessingState: "SubstProcState"</summary>
        public const string SubstrateProcessingStateAttributeName = "SubstProcState";

        /// <summary>E039 attribute name used for the optional Pending SubstrateProcessingState value: "PendingSPS"</summary>
        public const string PendingSubstrateProcessingStateAttributeName = "PendingSPS";

        /// <summary>E039 attribute name used for SubstrateTransportState: "SubstState"</summary>
        public const string SubstrateTransportStateAttributeName = "SubstState";

        /// <summary>E039 attribute name used for LotID: "LotID"</summary>
        public const string LotIDAttributeName = "LotID";

        /// <summary>E039 attribute name used for Usage: "SubstUsage"</summary>
        public const string SubstrateUsageAttributeName = "SubstUsage";

        /// <summary>E039 attribute name used for optional SubstrateJobRequestState: "SJRS"</summary>
        public const string SubstrateJobRequestStateAttributeName = "SJRS";

        /// <summary>E039 attribute name used for optional SubstrateJobState: "SJS"</summary>
        public const string SubstrateJobStateAttributeName = "SJS";

        /// <summary>E039 attribute name used for list of accumulated SubstProcState (SPS) values (when enabled) - includes both assigned SPS and assigned pending SPS values: "SPSList"</summary>
        public const string SPSListAttributeName = "SPSList";

        /// <summary>E039 attribute name used for list of accumulated SubstLocID Names at which the SPSList had an SPS value appended (when enabled): "SPSLocList"</summary>
        public const string SPSLocListAttributeName = "SPSLocList";

        /// <summary>E039 attribute name used for list of accumulated DateTime.Now.ToString("o") values at which the SPSList had an SPS value appended (when enabled): "SPSDateTimeList"</summary>
        public const string SPSDateTimeListAttributeName = "SPSDateTimeList";

        /// <summary>E039 attribute name used to optionally save the Name of the last location a substrate was in when it is being removed (used with RemovedSetsRemovedFromSubstLocNameAttribute option): "RemovedFromSubstLocName"</summary>
        public const string RemovedFromSubstLocNameAttributeName = "RemovedFromSubstLocName";

        /// <summary>E039 attribute name used to optionally record an instance number for a given SubstLoc object.</summary>
        public const string InstanceNumAttributeName = "InstanceNum";

        /// <summary>E039 attribute name used to optionally record the E87 SlotState from a mapping operation on a given location in that location object's attribute set.</summary>
        public const string MapSlotStateAttributeName = "MapSlotState";

        /// <summary>E039 attribute name used to optionally record the reason why an existing subsrate location is not currently accessible.</summary>
        public const string NotAccessibleReasonAttributeName = "NotAccessibleReason";
    }

    /// <summary>
    /// Static class that is used to hold static (global) settings that are used by the E090 specific extension methods to configure their behavior.  
    /// The choice of these setting values are client code specific and are synchronized with correct operation of client code.  
    /// As such there is not built in logic that can be used to obtain their values from modular config.
    /// </summary>
    public static class Settings
    {
        /// <summary>Static get/set property used to control the maximum length of any generated SPSList or SPSLocList.  Once either list reaches this length limit concatination will stop.  Default: 50.  Setter clips given value to be between 0 and 1000.</summary>
        public static int MaximumSPSListLength { get { return _maximumSPSListLength; } set { _maximumSPSListLength = value.Clip(0, 1000); } }
        private static int _maximumSPSListLength = DefaultMaximumSPSListLength;

        /// <summary>Defines the default value for the MaximumSPSListLength (50)</summary>
        public const int DefaultMaximumSPSListLength = 50;

        /// <summary>This value is used to globally add specific features to the updateBehavior parameter values used in NoteSubstMoved calls.  Defaults to None.</summary>
        public static E090StateUpdateBehavior NoteSubstMovedUpdateBehaviorAdditions { get { return _noteSubstMovedUpdateBehaviorAdditions; } set { _noteSubstMovedUpdateBehaviorAdditions = value; } }
        private static E090StateUpdateBehavior _noteSubstMovedUpdateBehaviorAdditions = E090StateUpdateBehavior.None;

        /// <summary>This value is used to globally add specific features to the updateBehavior parameter values used with SetSubstProcState, and SetPendingSubstProcState calls.  This property cannot be used to set the UsePendingSPS as it is removed (bitwise) from any value that is given to the setter.  Defaults to None.</summary>
        public static E090StateUpdateBehavior SetSubstProcStateUpdateBehaviorAdditions { get { return _setSubstProcStateUpdateBehaviorAdditions; } set { _setSubstProcStateUpdateBehaviorAdditions = (value & ~(E090StateUpdateBehavior.UsePendingSPS)); } }
        private static E090StateUpdateBehavior _setSubstProcStateUpdateBehaviorAdditions = E090StateUpdateBehavior.None;

        /// <summary>This value is used to globally add specific features to the updateBehavior parameter values used with RemoveE090Subst calls.  Defaults to None.</summary>
        public static E090StateUpdateBehavior RemoveSubstUpdateBehaviorAdditions { get { return _removeSubstUpdateBehaviorAdditions; } set { _removeSubstUpdateBehaviorAdditions = value; } }
        private static E090StateUpdateBehavior _removeSubstUpdateBehaviorAdditions = E090StateUpdateBehavior.None;

        /// <summary>This value is used to globally add specific features to the updateBehavior parameter values used with all direct, or indirect, GenerateE090UpdateItems calls (including NoteSubstMoved, SetSubstProcState, and SetPendingSubstProcState calls).  This property cannot be used to set the UsePendingSPS as it is removed (bitwise) from any value that is given to the setter.  Defaults to None.</summary>
        public static E090StateUpdateBehavior GenerateE090UpdateItemsBehaviorAdditions { get { return _generateE090UpdateItemsBehaviorAdditions; } set { _generateE090UpdateItemsBehaviorAdditions = (value & ~(E090StateUpdateBehavior.UsePendingSPS)); } }
        private static E090StateUpdateBehavior _generateE090UpdateItemsBehaviorAdditions = E090StateUpdateBehavior.None;

        /// <summary>
        /// Returns true if E090 has been configured to use the AddExternalSyncItem update behavior.  
        /// <para/>If <paramref name="checkNoteSubstrateMovedAdditions"/> is true then this check includes the current value of the NoteSubstMovedUpdateBehaviorAdditions selected behavior
        /// <para/>If <paramref name="checkSetSubstProcStateAdditions"/> is true then this check includes the current value of the SetSubstProcStateUpdateBehaviorAdditions selected behavior
        /// <para/>If <paramref name="checkGenerateUpdateItemAdditions"/> is true then this check includes the current value of the GenerateE090UpdateItemsBehaviorAdditions selected behavior
        /// <para/>If <paramref name="checkRemoveSubstUpdateBehaviorAdditions"/> is true then this check includes the current value of the RemoveSubstUpdateBehaviorAdditions selected behavior
        /// </summary>
        public static bool GetUseExternalSync(bool checkNoteSubstrateMovedAdditions = false, bool checkSetSubstProcStateAdditions = true, bool checkGenerateUpdateItemAdditions = true, bool checkRemoveSubstUpdateBehaviorAdditions = true)
        {
            var bitUnion = (checkNoteSubstrateMovedAdditions ? _noteSubstMovedUpdateBehaviorAdditions : E090StateUpdateBehavior.None)
                           | (checkSetSubstProcStateAdditions ? _setSubstProcStateUpdateBehaviorAdditions : E090StateUpdateBehavior.None)
                           | (checkGenerateUpdateItemAdditions ? _generateE090UpdateItemsBehaviorAdditions : E090StateUpdateBehavior.None)
                           | (checkRemoveSubstUpdateBehaviorAdditions ? _removeSubstUpdateBehaviorAdditions : E090StateUpdateBehavior.None)
                           ;

            return bitUnion.IsSet(E090StateUpdateBehavior.AddExternalSyncItem);
        }

        public static void ResetToDefaults()
        {
            MaximumSPSListLength = DefaultMaximumSPSListLength;
            NoteSubstMovedUpdateBehaviorAdditions = E090StateUpdateBehavior.None;
            SetSubstProcStateUpdateBehaviorAdditions = E090StateUpdateBehavior.None;
            RemoveSubstUpdateBehaviorAdditions = E090StateUpdateBehavior.None;
            GenerateE090UpdateItemsBehaviorAdditions = E090StateUpdateBehavior.None;
        }
    }

    #endregion

    #region E090StateUpdateBehavior flags

    /// <summary>
    /// Flags that are used to control the behavior of the various E090 Update related ExtensionMethods provided here.
    /// <para/>None (0x00), AllowReturnToNeedsProcessing (0x02), AutoUpdateSTS (0x04), UsePendingSPS (0x08), UseSPSList (0x10), UseSPSLocList (0x20), UseSPSDateTimeList (0x40), 
    /// AddSPSMoved (0x100), AddSPSCreated (0x200), AddSPSRemoved (0x400),
    /// AddExternalSyncItem (0x1000),
    /// HandleMovedToDestLocWithSJRSStopAndSPSInProcess (0x10000), HandleMovedToDestLocWithSJRSAbortAndSPSInProcess (0x20000),
    /// RemoveAttemptsToSetSPSToLost (0x100000), RemoveAttemptsToMoveLostSubstToDest (0x200000), RemoveAttemptsToMoveAllSubstToDestOrSrc (0x400000), RemovedSetsRemovedFromSubstLocNameAttribute (0x800000)
    /// </summary>
    [Flags]
    public enum E090StateUpdateBehavior : int
    {
        /// <summary>Placeholder default value [0x00]</summary>
        None = 0x00,
        
        /// <summary>Deprecated: Automatically add a transition to InProcess when the current state is NeedsProcessing and the targetSPS is a terminal state [0x01]</summary>
        [Obsolete("This function has been deprecated as the desired behavior can only be accomplished when generating the final events for external delivery.  This behavior cannot reliably by applied using E039 Update items. (2018-05-10)")]
        AutoInProcess = 0x01,
        
        /// <summary>Include this flag to support requested transitions from InProcess back to NeedsProcessing [0x02]</summary>
        AllowReturnToNeedsProcessing = 0x02,

        /// <summary>Include this flag to support adding automatic transitions for the STS based on the location and the SPS [0x04]</summary>
        AutoUpdateSTS = 0x04,

        /// <summary>
        /// The use of this flag causes the E090 SPS related logic to primarily update the PendingSPS rather than the main SPS property.  
        /// This allows the E090 SPS system to be given a sequence of values and delays performing the final SPS complete transition until the user explicitly requests it or until the substrate reaches its destination (at which point it is safe to lock down the final SPS value). [0x08]
        /// </summary>
        UsePendingSPS = 0x08,

        /// <summary>The use of this flag causes each E090 SPS state update operation to append the given non-Undefined SPS value to the SPSList attribute. [0x10]</summary>
        UseSPSList = 0x10,

        /// <summary>
        /// The use of this flag causes each E090 SPS state update operation to append the current substrate location to the SPSLocList attribute whenever a new SPS value is appendended to the SPSList.
        /// <para/>Note: if this flag is not used uniformly with the UseSPSList flag, then the SPSLocList attribute may end up being shorter than the SPSList attribute itself.
        /// [0x20]
        /// </summary>
        UseSPSLocList = 0x20,

        /// <summary>
        /// The use of this flag causes each E090 SPS update operation to append the current DateTime value to the SPSDateTimeList attribute whenever a new SPS value is appended to the SPSList.
        /// <para/>Note: if this flag is not used uniformly with the UseSPSList flag, then the SPSLocList attribute may end up being shorter than the SPSList attribute itself.
        /// [0x40]
        /// </summary>
        UseSPSDateTimeList = 0x40,

        /// <summary>The use of this flag causes each E090 substrate move to record SPS.Moved in its SPSList (et. al.). [0x100]</summary>
        AddSPSMoved = 0x100,

        /// <summary>The use of this flag causes an E090 substrate creation to record SPS.Added in its SPSList (et. al.) [0x200]</summary>
        AddSPSCreated = 0x200,

        /// <summary>The use of this flag causes an E090 substrate removal to record SPS.Removed in its SPSList (et. al.) [0x400]</summary>
        AddSPSRemoved = 0x400,

        /// <summary>The use of this flag requests that any corresponding E090 create, of update method should also include an external sync operation. [0x1000]</summary>
        AddExternalSyncItem = 0x1000,

        /// <summary>When a substrate is moved to its destination location and its SPS is InProcess and its SJRS is Stop then its SPS will be set to Stopped. [0x10000]</summary>
        /// <remarks>Note that the use of this option allows such a substrate to reach its destination without additional work on the scheduler/SRM's behalf</remarks>
        HandleMovedToDestLocWithSJRSStopAndSPSInProcess = 0x10000,

        /// <summary>When a substrate is moved to its destination location and its SPS is InProcess and its SJRS is Abort then its SPS will be set to Aborted. [0x10000]</summary>
        /// <remarks>Note that the use of this option allows such a substrate to reach its destination without additional work on the scheduler/SRM's behalf</remarks>
        HandleMovedToDestLocWithSJRSAbortAndSPSInProcess = 0x20000,

        /// <summary>When a substrate is removed its SPS will be set to Lost if it has not already reached a complete state.  [0x100000]</summary>
        RemoveAttemptsToSetSPSToLost = 0x100000,

        /// <summary>When a substrate is removed in the Lost state it will be moved to its Destination location before being marked as removed (if needed).  This is done after appling the removal changes in relation to the RemoveAttemptsToSetSPSToLost flag.  [0x200000]</summary>
        RemoveAttemptsToMoveLostSubstToDest = 0x200000,

        /// <summary>When a substrate is removed it will be moved back to its Source or Destination location before being marked as removed.  This is done after appling the removal changes in relation to the RemoveAttemptsToSetSPSToLost flag.  [0x400000]</summary>
        RemoveAttemptsToMoveAllSubstToDestOrSrc = 0x400000,

        /// <summary>When a substrate is removed this option will cause its RemovedFromSubstLocName Attribute to be created/set to the location name of the location from which it is being removed.  [0x800000]</summary>
        RemovedSetsRemovedFromSubstLocNameAttribute = 0x800000,

        /// <summary>(AllowReturnToNeedsProcessing | AutoUpdateSTS) [0x06]</summary>
        [Obsolete("Please switch to using other enumeration combination values (2018-05-30)")]
        All = (AllowReturnToNeedsProcessing | AutoUpdateSTS),

        /// <summary>(AllowReturnToNeedsProcessing | AutoUpdateSTS) [0x06]</summary>
        [Obsolete("Please switch to using other enumeration combination values (2018-05-30)")]
        AllV1 = (AllowReturnToNeedsProcessing | AutoUpdateSTS),

        /// <summary>(AllowReturnToNeedsProcessing | AutoUpdateSTS | UsePendingSPS | UseSPSList | UseSPSLocList) [0x3e]</summary>
        [Obsolete("Please switch to using other enumeration combination values (2018-05-30)")]
        AllV2 = (AllowReturnToNeedsProcessing | AutoUpdateSTS | UsePendingSPS | UseSPSList | UseSPSLocList),

        /// <summary>(AllowReturnToNeedsProcessing | AutoUpdateSTS) [0x06]</summary>
        StandardSPSUpdate = (AllowReturnToNeedsProcessing | AutoUpdateSTS),

        /// <summary>(AllowReturnToNeedsProcessing | AutoUpdateSTS | UsePendingSPS) [0x0e]</summary>
        PendingSPSUpdate = (AllowReturnToNeedsProcessing | AutoUpdateSTS | UsePendingSPS),

        /// <summary>(AutoUpdateSTS | UsePendingSPS) [0x0c]</summary>
        StandardMoveUpdate = (AutoUpdateSTS | UsePendingSPS),

        /// <summary>(UseSPSList | UseSPSLocList) [0x30]</summary>
        BasicSPSLists = (UseSPSList | UseSPSLocList),
    }

    #endregion

    #region E090 ExtensionMethods: CreateE090SubstLoc, CreateE090Subst, NoteSubstMoved, SetSubstProcState, GenerateCreateE090SubstLocItems, GenerateCreateE090SubstItems, GenerateE090UpdateItems, IsSubstrate

    /// <summary>
    /// The E090 interface presented here are implemented as a set of extension methods that are used to create and managed E090 Substrate and SubstLoc types of E039 objects.
    /// </summary>
    public static partial class ExtensionMethods
    {
        public static E039ObjectID CreateE090SubstID(this string substName, bool assignUUID = false, IE039TableObserver tableObserver = null)
        {
            return new E039ObjectID(substName, Constants.SubstrateObjectType, assignUUID: assignUUID, tableObserver: tableObserver);
        }

        public static E039ObjectID CreateE090SubstLocID(this string substLocName, IE039TableObserver tableObserver = null)
        {
            return new E039ObjectID(substLocName, Constants.SubstrateLocationObjectType, tableObserver: tableObserver);
        }

        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, Action<E039UpdateItem.AddObject> addedObjectDelegate, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.Pinned, bool addSyncExternalItem = false, int instanceNum = 0, bool addIfNeeded = true, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate, E090SubstLocInfo? initialAttributesFromInfo = null)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090SubstLoc(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem, instanceNum: instanceNum, addIfNeeded: addIfNeeded, mergeBehavior: mergeBehavior, initialAttributesFromInfo: initialAttributesFromInfo);

            if (addedObjectDelegate != null)
                addedObjectDelegate(addObjectUpdateItem);

            return ec;
        }

        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, Action<E039ObjectID> addedObjectIDDelegate, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.Pinned, bool addSyncExternalItem = false, int instanceNum = 0, bool addIfNeeded = true, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate, E090SubstLocInfo? initialAttributesFromInfo = null)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090SubstLoc(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem, instanceNum: instanceNum, addIfNeeded: addIfNeeded, mergeBehavior: mergeBehavior, initialAttributesFromInfo: initialAttributesFromInfo);

            if (addedObjectIDDelegate != null && addObjectUpdateItem.AddedObjectPublisher != null)
                addedObjectIDDelegate((addObjectUpdateItem.AddedObjectPublisher.Object ?? E039Object.Empty).ID);

            return ec;
        }

        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.Pinned, bool addSyncExternalItem = false, int instanceNum = 0, bool addIfNeeded = true, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate, E090SubstLocInfo ? initialAttributesFromInfo = null)
        {
            List<E039UpdateItem> updateItemList = new List<E039UpdateItem>().GenerateCreateE090SubstLocItems(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: false, instanceNum: instanceNum, addIfNeeded: addIfNeeded, mergeBehavior: mergeBehavior);

            if (initialAttributesFromInfo != null)
            {
                var attributesValuesToUse = initialAttributesFromInfo ?? E090SubstLocInfo.Empty;
                var pseudoCurrentInfo = new E090SubstLocInfo() { ObjID = addObjectUpdateItem.ObjID };

                updateItemList.GenerateE090UpdateItems(pseudoCurrentInfo, mapSlotStateParam: attributesValuesToUse.MapSlotState, notAccessibleReasonParam: attributesValuesToUse.NotAccessibleReason, ignoreCurrentSubstLocInfoValidity: true);
            }

            if (addSyncExternalItem)
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return tableUpdater.Update(updateItemList.ToArray()).Run();
        }

        public static string CreateE090Subst(this IE039TableUpdater tableUpdater, string substName, Action<E039UpdateItem.AddObject> addedObjectDelegate, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090Subst(substName, out addObjectUpdateItem, srcSubstLocObjID: srcSubstLocObjID, destSubstLocObjID: destSubstLocObjID, initialE090SubstrateObjState: initialE090SubstrateObjState, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem);

            if (addedObjectDelegate != null)
                addedObjectDelegate(addObjectUpdateItem);

            return ec;
        }

        public static string CreateE090Subst(this IE039TableUpdater tableUpdater, string substName, Action<E039ObjectID> addedObjectIDDelegate, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090Subst(substName, out addObjectUpdateItem, srcSubstLocObjID: srcSubstLocObjID, destSubstLocObjID: destSubstLocObjID, initialE090SubstrateObjState: initialE090SubstrateObjState, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem);

            if (addedObjectIDDelegate != null && addObjectUpdateItem.AddedObjectPublisher != null)
                addedObjectIDDelegate((addObjectUpdateItem.AddedObjectPublisher.Object ?? E039Object.Empty).ID);

            return ec;
        }

        public static string CreateE090Subst(this IE039TableUpdater tableUpdater, string substName, out E039UpdateItem.AddObject addObjectUpdateItem, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem[] updateItemArray = new List<E039UpdateItem>().GenerateCreateE090SubstItems(substName, out addObjectUpdateItem, srcSubstLocObjID: srcSubstLocObjID, destSubstLocObjID: destSubstLocObjID, initialE090SubstrateObjState: initialE090SubstrateObjState, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem).ToArray();

            return tableUpdater.Update(updateItemArray).Run();
        }

        public static string RemoveE090Subst(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false)
        {
            var ec = tableUpdater.RemoveE090Subst((E090SubstInfo) substObs, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
            substObs.UpdateInline();
            return ec;
        }

        public static string RemoveE090Subst(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem[] updateItemArray = new List<E039UpdateItem>().GenerateRemoveE090SubstItems(currentSubstInfo: currentSubstInfo, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem).ToArray();

            return tableUpdater.Update(updateItemArray).Run();
        }

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, IE039Object substObj, IE039Object toLocObj, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.StandardMoveUpdate, bool addSyncExternalItem = false)
        {
            E039ObjectID toLocObjID = (toLocObj != null) ? toLocObj.ID : E039ObjectID.Empty;

            return tableUpdater.NoteSubstMoved(substObj, toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
        }

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, IE039Object substObj, E039ObjectID toLocObjID, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.StandardMoveUpdate, bool addSyncExternalItem = false)
        {
            E090SubstInfo currentSubstInfo = new E090SubstInfo(substObj);

            return tableUpdater.NoteSubstMoved(currentSubstInfo, toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
        }

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, E039ObjectID toLocObjID, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.StandardMoveUpdate, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.NoteSubstMoved((E090SubstInfo) substObs, toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
            substObs.Update();
            return ec;
        }

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, E039ObjectID toLocObjID, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.StandardMoveUpdate, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = currentSubstInfo.ObjID;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' '{2}'".CheckedFormat(Fcns.CurrentMethodName, substObjID, toLocObjID)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                updateBehavior |= Settings.NoteSubstMovedUpdateBehaviorAdditions;

                if (updateBehavior.IsSet(E090StateUpdateBehavior.AddSPSMoved))
                    ec = updateItemList.GenerateE090UpdateItems(currentSubstInfo, toLocObjID: toLocObjID, spsParam: SubstProcState.Moved, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
                else
                    ec = updateItemList.GenerateE090UpdateItems(currentSubstInfo, toLocObjID: toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

                if (ec.IsNullOrEmpty())
                {
                    if (updateItemList.Count > 0)
                        ec = tableUpdater.Update(updateItemList.ToArray()).Run();
                    else
                        logger.Debug.Emit("{0}: nothing to do: obj '{0}' is already at '{1}'", substObjID, toLocObjID);
                }

                eeTrace.ExtraMessage = ec;
            }

            return ec;
        }

        public static string SetSubstProcState(this IE039TableUpdater tableUpdater, IE039Object substObj, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.StandardSPSUpdate, bool addSyncExternalItem = false)
        {
            E090SubstInfo currentSubstInfo = new E090SubstInfo(substObj);

            return tableUpdater.SetSubstProcState(currentSubstInfo, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
        }

        public static string SetSubstProcState(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.StandardSPSUpdate, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.SetSubstProcState((E090SubstInfo) substObs, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

            substObs.Update();

            return ec;
        }

        public static string SetSubstProcState(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.StandardSPSUpdate, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = currentSubstInfo.ObjID;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' {2} [{3}]".CheckedFormat(Fcns.CurrentMethodName, substObjID, spsParam, updateBehavior)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                updateBehavior |= Settings.SetSubstProcStateUpdateBehaviorAdditions;

                ec = updateItemList.GenerateE090UpdateItems(currentSubstInfo, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

                if (ec.IsNullOrEmpty())
                {
                    if (updateItemList.Count > 0)
                        ec = tableUpdater.Update(updateItemList.ToArray()).Run();
                    else
                        logger.Debug.Emit("{0}: nothing to do: obj '{0}' sps is already {1}", substObjID, spsParam);
                }

                eeTrace.ExtraMessage = ec;
            }

            return ec;
        }

        public static string SetPendingSubstProcState(this IE039TableUpdater tableUpdater, IE039Object substObj, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.PendingSPSUpdate, bool addSyncExternalItem = false)
        {
            return tableUpdater.SetSubstProcState(substObj, spsParam: spsParam, updateBehavior: updateBehavior | E090StateUpdateBehavior.UsePendingSPS, addSyncExternalItem: addSyncExternalItem);
        }

        public static string SetPendingSubstProcState(this IE039TableUpdater tableUpdater, E090SubstLocObserver substLocObs, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.PendingSPSUpdate, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.SetPendingSubstProcState((E090SubstInfo) substLocObs, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

            substLocObs.Update();

            return ec;
        }

        public static string SetPendingSubstProcState(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.PendingSPSUpdate, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.SetPendingSubstProcState((E090SubstInfo) substObs, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

            substObs.Update();

            return ec;
        }

        public static string SetPendingSubstProcState(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.PendingSPSUpdate, bool addSyncExternalItem = false)
        {
            return tableUpdater.SetSubstProcState(currentSubstInfo, spsParam: spsParam, updateBehavior: updateBehavior | E090StateUpdateBehavior.UsePendingSPS, addSyncExternalItem: addSyncExternalItem);
        }

        public static E039UpdateItem.AddObject GenerateCreateE090SubstLocItems(this string substLocName, List<E039UpdateItem> updateItemList, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addIfNeeded = false, bool addSyncExternalItem = false)
        {
            E039UpdateItem.AddObject addObjectItem;

            updateItemList.GenerateCreateE090SubstLocItems(substLocName, out addObjectItem, attributes: attributes, flags: flags, addIfNeeded: addIfNeeded, addSyncExternalItem: addSyncExternalItem);

            return addObjectItem;
        }

        public static string SetSubstrateJobStates(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, SubstrateJobRequestState ? sjrs = null, SubstrateJobState ? sjs = null, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.SetSubstrateJobStates((E090SubstInfo) substObs, sjrs: sjrs, sjs: sjs, addSyncExternalItem: addSyncExternalItem);

            substObs.Update();

            return ec;
        }

        public static string SetSubstrateJobStates(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, SubstrateJobRequestState? sjrs = null, SubstrateJobState? sjs = null, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = currentSubstInfo.ObjID;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}'{2}{3}".CheckedFormat(Fcns.CurrentMethodName, substObjID, sjrs != null ? " sjrs:{0}".CheckedFormat(sjrs) : "", sjs != null ? " sjs:{0}".CheckedFormat(sjs) : "")))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                ec = updateItemList.GenerateE090UpdateSubstrateJobStates(currentSubstInfo, sjrs: sjrs, sjs: sjs, addSyncExternalItem: addSyncExternalItem);

                if (ec.IsNullOrEmpty())
                {
                    if (updateItemList.Count > 0)
                        eeTrace.ExtraMessage = tableUpdater.Update(updateItemList.ToArray()).Run();
                    else
                        eeTrace.ExtraMessage = "There was nothing to do";
                }
                else
                {
                    eeTrace.ExtraMessage = ec;
                }
            }

            return ec;
        }

        public static string SetSubstLocStates(this IE039TableUpdater tableUpdater, E090SubstLocObserver substLocObs, E087.SlotState? mapSlotStateParam = null, string notAccessibleReasonParam = null, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.SetSubstLocStates(substLocObs.Info, mapSlotStateParam: mapSlotStateParam, notAccessibleReasonParam: notAccessibleReasonParam, addSyncExternalItem: addSyncExternalItem);

            substLocObs.Update();

            return ec;
        }

        public static string SetSubstLocStates(this IE039TableUpdater tableUpdater, E090SubstLocInfo currentSubstLocInfo, E087.SlotState? mapSlotStateParam = null, string notAccessibleReasonParam = null, bool addSyncExternalItem = false)
        {
            E039ObjectID substLocObjID = currentSubstLocInfo.ObjID;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}'{2}{3}".CheckedFormat(Fcns.CurrentMethodName, substLocObjID, mapSlotStateParam != null ? " mapSlotState:{0}".CheckedFormat(mapSlotStateParam) : "", notAccessibleReasonParam != null ? " notAccessibleReason:{0}".CheckedFormat(notAccessibleReasonParam) : "")))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                ec = updateItemList.GenerateE090UpdateItems(currentSubstLocInfo, mapSlotStateParam: mapSlotStateParam, notAccessibleReasonParam: notAccessibleReasonParam, addSyncExternalItem: addSyncExternalItem);

                if (ec.IsNullOrEmpty())
                {
                    if (updateItemList.Count > 0)
                        eeTrace.ExtraMessage = tableUpdater.Update(updateItemList.ToArray()).Run();
                    else
                        eeTrace.ExtraMessage = "There was nothing to do";
                }
                else
                {
                    eeTrace.ExtraMessage = ec;
                }
            }

            return ec;
        }

        public static List<E039UpdateItem> GenerateCreateE090SubstLocItems(this List<E039UpdateItem> updateItemList, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addIfNeeded = false, bool addSyncExternalItem = false, int instanceNum = 0, bool assignUUID = false, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
        {
            E039ObjectID substLocObjID = new E039ObjectID(substLocName, Constants.SubstrateLocationObjectType, assignUUID: assignUUID);

            if (instanceNum != 0)
                attributes = attributes.ConvertToWritable().SetValue("InstanceNum", instanceNum);

            updateItemList.Add(addObjectUpdateItem = new E039UpdateItem.AddObject(substLocObjID, attributes: attributes, flags: flags, ifNeeded: addIfNeeded, mergeBehavior: mergeBehavior));
            updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substLocObjID, E039ObjectID.Empty, Constants.ContainsLinkKey), ifNeeded: addIfNeeded));

            if (addSyncExternalItem)
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return updateItemList;
        }

        public static List<E039UpdateItem> GenerateCreateE090SubstItems(this List<E039UpdateItem> updateItemList, string substName, out E039UpdateItem.AddObject addObjectUpdateItem, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false, bool assignUUID = true)
        {
            E039ObjectID substObjID = new E039ObjectID(substName, Constants.SubstrateObjectType, assignUUID: assignUUID);

            E090SubstInfo useInitialState = initialE090SubstrateObjState ?? E090SubstInfo.Initial;

            srcSubstLocObjID = srcSubstLocObjID ?? E039ObjectID.Empty;
            destSubstLocObjID = destSubstLocObjID ?? srcSubstLocObjID;

            // set things up so that we can really use the InferredSTS
            useInitialState.LinkToSrc = new E039Link(substObjID, srcSubstLocObjID, Constants.SourceLocationLinkKey);
            useInitialState.LinkToDest = new E039Link(substObjID, destSubstLocObjID, Constants.DestinationLocationLinkKey);
            if (useInitialState.LocID.IsNullOrEmpty())
                useInitialState.LocID = useInitialState.SPS.IsProcessingComplete() ? destSubstLocObjID.Name : srcSubstLocObjID.Name;

            useInitialState.STS = useInitialState.InferredSTS;

            attributes = useInitialState.UpdateAttributeValues(new NamedValueSet()).MergeWith(attributes, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate).MakeReadOnly();

            updateItemList.Add(addObjectUpdateItem = new E039UpdateItem.AddObject(substObjID, attributes: attributes, flags: flags));

            if (!srcSubstLocObjID.IsNullOrEmpty())
                updateItemList.Add(new E039UpdateItem.AddLink(useInitialState.LinkToSrc));

            if (!destSubstLocObjID.IsEmpty)
                updateItemList.Add(new E039UpdateItem.AddLink(useInitialState.LinkToDest));

            var createAtLocID = E039ObjectID.Empty;

            switch (useInitialState.STS)
            {
                case SubstState.AtSource: createAtLocID = srcSubstLocObjID; break;
                case SubstState.AtDestination: createAtLocID = destSubstLocObjID; break;
                case SubstState.AtWork: createAtLocID = new E039ObjectID(useInitialState.LocID, Constants.SubstrateLocationObjectType); break;
                default: break;
            }

            if (!createAtLocID.IsEmpty)
            {
                var containsLink = new E039Link(createAtLocID, substObjID, Constants.ContainsLinkKey);
                updateItemList.Add(new E039UpdateItem.AddLink(containsLink));

                var createUpdateBehavior = Settings.NoteSubstMovedUpdateBehaviorAdditions | Settings.GenerateE090UpdateItemsBehaviorAdditions;

                if (createUpdateBehavior.IsSet(E090StateUpdateBehavior.AddSPSCreated))
                {
                    var pseudoCurrentState = useInitialState;

                    pseudoCurrentState.ObjID = substObjID;
                    pseudoCurrentState.LocsLinkedToHere = new[] { containsLink };

                    updateItemList.GenerateE090UpdateItems(pseudoCurrentState, spsParam: SubstProcState.Created);
                }
            }

            if (addSyncExternalItem)
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return updateItemList;
        }

        public static List<E039UpdateItem> GenerateRemoveE090SubstItems(this List<E039UpdateItem> updateItemList, E090SubstInfo currentSubstInfo, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false)
        {
            updateBehavior |= Settings.RemoveSubstUpdateBehaviorAdditions;

            bool removeAttemptsToSetSPSToLost = updateBehavior.IsSet(E090StateUpdateBehavior.RemoveAttemptsToSetSPSToLost);
            bool removeAttemptsToMoveAllSubstToDestOrSrc = updateBehavior.IsSet(E090StateUpdateBehavior.RemoveAttemptsToMoveAllSubstToDestOrSrc);
            bool removeAttemptsToMoveLostSubstToDest = removeAttemptsToMoveAllSubstToDestOrSrc || updateBehavior.IsSet(E090StateUpdateBehavior.RemoveAttemptsToMoveLostSubstToDest);
            if (removeAttemptsToMoveLostSubstToDest || removeAttemptsToMoveAllSubstToDestOrSrc)
                updateBehavior |= Settings.NoteSubstMovedUpdateBehaviorAdditions;

            if (!currentSubstInfo.SPS.IsProcessingComplete() && removeAttemptsToSetSPSToLost)
            {
                updateItemList.GenerateE090UpdateItems(currentSubstInfo: currentSubstInfo, spsParam: SubstProcState.Lost, updateBehavior: updateBehavior);
                currentSubstInfo.SPS = SubstProcState.Lost;
            }

            if (currentSubstInfo.STS.IsAtWork() && (currentSubstInfo.SPS == SubstProcState.Lost ? removeAttemptsToMoveLostSubstToDest : removeAttemptsToMoveAllSubstToDestOrSrc))
            {
                bool currentSubstInfoIsNeedsProcessing = currentSubstInfo.SPS.IsNeedsProcessing();

                var spsParam = updateBehavior.IsSet(E090StateUpdateBehavior.AddSPSMoved) ? SubstProcState.Moved : SubstProcState.Undefined;

                if (currentSubstInfoIsNeedsProcessing && !currentSubstInfo.LinkToSrc.ToID.IsEmpty && currentSubstInfo.LocID != currentSubstInfo.LinkToSrc.ToID.Name)
                {
                    updateItemList.GenerateE090UpdateItems(currentSubstInfo: currentSubstInfo, spsParam: spsParam, toLocObjID: currentSubstInfo.LinkToSrc.ToID, updateBehavior: updateBehavior);
                    currentSubstInfo.LocID = currentSubstInfo.LinkToSrc.ToID.Name;
                }
                else if (!currentSubstInfoIsNeedsProcessing && !currentSubstInfo.LinkToDest.ToID.IsEmpty && currentSubstInfo.LocID != currentSubstInfo.LinkToDest.ToID.Name)
                {
                    updateItemList.GenerateE090UpdateItems(currentSubstInfo: currentSubstInfo, spsParam: spsParam, toLocObjID: currentSubstInfo.LinkToDest.ToID, updateBehavior: updateBehavior);
                    currentSubstInfo.LocID = currentSubstInfo.LinkToDest.ToID.Name;
                }
            }

            updateBehavior |= Settings.GenerateE090UpdateItemsBehaviorAdditions;

            if (updateBehavior.IsSet(E090StateUpdateBehavior.AddSPSRemoved))
                updateItemList.GenerateE090UpdateItems(currentSubstInfo: currentSubstInfo, spsParam: SubstProcState.Removed, updateBehavior: updateBehavior);

            if (updateBehavior.IsSet(E090StateUpdateBehavior.RemovedSetsRemovedFromSubstLocNameAttribute))
                updateItemList.AddSetAttributesItem(objID: currentSubstInfo, attributes: new NamedValueSet() { { Constants.RemovedFromSubstLocNameAttributeName, currentSubstInfo.LocID } }, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate);

            updateItemList.Add(new E039UpdateItem.RemoveObject(currentSubstInfo.ObjID));

            if (addSyncExternalItem)
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return updateItemList;
        }

        public static string GenerateE090UpdateItems(this List<E039UpdateItem> updateItemList, IE039Object substObj, SubstProcState spsParam = SubstProcState.Undefined, E039ObjectID toLocObjID = null, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false)
        {
            E090SubstInfo currentSubstInfo = new E090SubstInfo(substObj);

            return updateItemList.GenerateE090UpdateItems(currentSubstInfo, spsParam: spsParam, toLocObjID: toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
        }

        public static string GenerateE090UpdateItems(this List<E039UpdateItem> updateItemList, E090SubstInfo currentSubstInfo, SubstProcState spsParam = SubstProcState.Undefined, E039ObjectID toLocObjID = null, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false)
        {
            updateBehavior |= Settings.GenerateE090UpdateItemsBehaviorAdditions;

            if (currentSubstInfo.ObjID == null || updateItemList == null)
                return "{0}: given invalid or null parameter".CheckedFormat(Fcns.CurrentMethodName);

            if (!currentSubstInfo.ObjID.IsSubstrate())
                return "{0}: given non-Substrate ObjID [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo.ObjID);

            toLocObjID = toLocObjID ?? E039ObjectID.Empty;

            if (!currentSubstInfo.IsValid)
                return "{0}: given unusable obj.  derived Substrate Info is not valid: [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo);

            // first generate any required location move update item and update the currentLocID accordingly
            string currentLocID = currentSubstInfo.LocID;
            bool substrateMoved = false;

            if (!toLocObjID.IsEmpty)
            {
                E039ObjectID fromLocObjID = currentSubstInfo.LocsLinkedToHere.FirstOrDefault().FromID;

                if (fromLocObjID.IsEmpty)
                    logger.Debug.Emit("{0}: Issue with attempt to move [{1}]: current location [{2}] is unknown", Fcns.CurrentMethodName, currentSubstInfo.ObjID, currentLocID);

                if (toLocObjID.Name != currentLocID)
                {
                    updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(toLocObjID, currentSubstInfo.ObjID, Constants.ContainsLinkKey), autoUnlinkFromPriorByTypeStr: true));
                    currentLocID = toLocObjID.Name;
                    substrateMoved = true;
                }
            }

            // the following logic will create the attribute update NVS if needed.  If it ends up being non-empty then an UpdateItem will be added for it.
            NamedValueSet attribUpdateNVS = null;
            NamedValueMergeBehavior attribUpdateNVSMergeBehavior = NamedValueMergeBehavior.AddAndUpdate;

            // next apply any change in the SPS and/or PendingSPS

            bool clearPendingSPS = false;
            bool spsIsPsuedoStateValueCreatedMovedOrRemoved = spsParam.IsPseudoState(includeCreatedMovedAndRemoved: true, includeProcessStepCompleted: false);
            bool addToSPSLists = spsIsPsuedoStateValueCreatedMovedOrRemoved;

            {
                var setSPS = SubstProcState.Undefined;

                if (substrateMoved && (spsParam == SubstProcState.Undefined || spsParam == SubstProcState.Moved) && currentLocID == currentSubstInfo.LinkToDest.ToID.Name)
                {
                    if (currentSubstInfo.SJRS == SubstrateJobRequestState.Stop && updateBehavior.IsSet(E090StateUpdateBehavior.HandleMovedToDestLocWithSJRSStopAndSPSInProcess))
                    {
                        attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                        setSPS = SubstProcState.Stopped;
                        spsParam = spsParam.MapUndefinedTo(setSPS);
                        addToSPSLists = true;
                    }
                    if (currentSubstInfo.SJRS == SubstrateJobRequestState.Abort && updateBehavior.IsSet(E090StateUpdateBehavior.HandleMovedToDestLocWithSJRSAbortAndSPSInProcess))
                    {
                        attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                        setSPS = SubstProcState.Aborted;
                        spsParam = spsParam.MapUndefinedTo(setSPS);
                        addToSPSLists = true;
                    }
                }

                var setPendingSPS = SubstProcState.Undefined;

                if (spsParam != SubstProcState.Undefined && !spsIsPsuedoStateValueCreatedMovedOrRemoved && setSPS == SubstProcState.Undefined)
                {
                    attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                    bool usePendingSPSBehavior = updateBehavior.IsSet(E090StateUpdateBehavior.UsePendingSPS);

                    if (!usePendingSPSBehavior)
                    {
                        var mergedSPSParam = spsParam;

                        if (currentSubstInfo.PendingSPS != SubstProcState.Undefined)
                        {
                            mergedSPSParam = spsParam.MergeWith(currentSubstInfo.PendingSPS);
                            if (mergedSPSParam != spsParam)
                                logger.Debug.Emit("{0}: for '{1}' replaced given sps:{2} with:{3} from PendingSPS", Fcns.CurrentMethodName, currentSubstInfo.ObjID, spsParam, mergedSPSParam);
                            else
                                clearPendingSPS = true;
                        }

                        string ec = GetSPSTransitionDenyReason(currentSubstInfo.SPS, mergedSPSParam, allowReturnToNeedsProcessing: updateBehavior.IsSet(E090StateUpdateBehavior.AllowReturnToNeedsProcessing));

                        if (!ec.IsNullOrEmpty())
                            logger.Debug.Emit("{0}: Issue with attempt to set SPS for [{1}, @{2}]: {3}", Fcns.CurrentMethodName, currentSubstInfo.ObjID, currentLocID, ec);

                        if (mergedSPSParam != currentSubstInfo.SPS)
                            setSPS = mergedSPSParam;
                    }
                    else if (usePendingSPSBehavior)
                    {
                        var nextPendingSPS = currentSubstInfo.InferredSPS.MergeWith(spsParam);

                        if (nextPendingSPS != currentSubstInfo.PendingSPS)
                            setPendingSPS = nextPendingSPS;

                        if (currentSubstInfo.SPS == SubstProcState.NeedsProcessing)
                        {
                            switch (setPendingSPS)
                            {
                                case SubstProcState.NeedsProcessing:
                                    break;
                                case SubstProcState.InProcess:
                                case SubstProcState.Processed:
                                case SubstProcState.Rejected:
                                case SubstProcState.Stopped:
                                case SubstProcState.Aborted:
                                case SubstProcState.ProcessStepCompleted:
                                    setSPS = SubstProcState.InProcess;
                                    break;
                                case SubstProcState.Skipped:
                                case SubstProcState.Lost:
                                default:
                                    setSPS = setPendingSPS;
                                    break;
                            }
                        }
                        else if (currentSubstInfo.SPS.IsProcessingComplete())
                        {
                            clearPendingSPS = true;
                        }
                    }

                    addToSPSLists = true;
                }

                if (setSPS != SubstProcState.Undefined)
                {
                    attribUpdateNVS.SetValue("SubstProcState", setSPS);
                    currentSubstInfo.SPS = setSPS;

                    clearPendingSPS |= setSPS.IsProcessingComplete();
                }

                if (clearPendingSPS)
                {
                    // the attribute additions required to do the clear are added later.
                    currentSubstInfo.PendingSPS = SubstProcState.Undefined;
                }
                else if (setPendingSPS != SubstProcState.Undefined)
                {
                    attribUpdateNVS.SetValue("PendingSPS", setPendingSPS);
                    currentSubstInfo.PendingSPS = setPendingSPS;
                }
            }

            // optionally add the given spsParam value to the SPSList (and optinally update the SPSLocList and SPSDateTimeList)

            if (addToSPSLists)
            {
                var maximumSPSListLength = Settings.MaximumSPSListLength;

                if (updateBehavior.IsSet(E090StateUpdateBehavior.UseSPSList)
                    && currentSubstInfo.SPSList.SafeLength() < maximumSPSListLength
                    && currentSubstInfo.SPSLocList.SafeCount() < maximumSPSListLength
                    && currentSubstInfo.SPSDateTimeList.SafeCount() < maximumSPSListLength)
                {
                    attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                    attribUpdateNVSMergeBehavior |= NamedValueMergeBehavior.AppendLists;        // turn on AppendLists mode to the current NVS merge behavior

                    attribUpdateNVS.SetValue(Constants.SPSListAttributeName, new ReadOnlyIList<string>(spsParam.ToString()));

                    if (updateBehavior.IsSet(E090StateUpdateBehavior.UseSPSLocList))
                        attribUpdateNVS.SetValue(Constants.SPSLocListAttributeName, new ReadOnlyIList<string>(currentLocID));

                    if (updateBehavior.IsSet(E090StateUpdateBehavior.UseSPSDateTimeList))
                        attribUpdateNVS.SetValue(Constants.SPSDateTimeListAttributeName, new ReadOnlyIList<string>(DateTime.Now.ToString("o")));
                }
            }

            if (updateBehavior.IsSet(E090StateUpdateBehavior.AutoUpdateSTS))
            {
                attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                SubstState inferredNextSTS = currentSubstInfo.GetInferredSTS(currentLocID, sps: currentSubstInfo.InferredSPS);

                if (inferredNextSTS != currentSubstInfo.STS)
                {
                    attribUpdateNVS.SetValue("SubstState", inferredNextSTS);

                    var inferredSPS = currentSubstInfo.InferredSPS;
                    if (inferredNextSTS == SubstState.AtDestination && currentSubstInfo.SPS != inferredSPS)
                    {
                        attribUpdateNVS.SetValue("SubstProcState", inferredSPS);
                        currentSubstInfo.SPS = inferredSPS;

                        clearPendingSPS = inferredSPS.IsProcessingComplete();
                    }
                }
            }

            if (clearPendingSPS)
            {
                attribUpdateNVSMergeBehavior |= NamedValueMergeBehavior.RemoveNull;        // turn on the RemoveNull mode in the current NVS merge behavior

                attribUpdateNVS.SetValue("PendingSPS", null);

                currentSubstInfo.PendingSPS = SubstProcState.Undefined;
            }

            if (!attribUpdateNVS.IsNullOrEmpty())
                updateItemList.AddSetAttributesItem(objID: currentSubstInfo.ObjID, attributes: attribUpdateNVS, mergeBehavior: attribUpdateNVSMergeBehavior);

            if (addSyncExternalItem || updateBehavior.IsSet(E090StateUpdateBehavior.AddExternalSyncItem))
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return string.Empty;
        }

        public static string GenerateE090UpdateSubstrateJobStates(this List<E039UpdateItem> updateItemList, E090SubstInfo currentSubstInfo, SubstrateJobRequestState? sjrs = null, SubstrateJobState? sjs = null, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false)
        {
            if (currentSubstInfo.ObjID == null || updateItemList == null)
                return "{0}: given invalid or null parameter".CheckedFormat(Fcns.CurrentMethodName);

            if (!currentSubstInfo.ObjID.IsSubstrate())
                return "{0}: given non-Substrate ObjID [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo.ObjID);

            if (!currentSubstInfo.IsValid)
                return "{0}: given unusable obj.  derived Substrate Info is not valid: [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo);

            NamedValueSet attribUpdateNVS = null;
            NamedValueMergeBehavior attribUpdateNVSMergeBehavior = NamedValueMergeBehavior.AddAndUpdate;

            if (sjrs != null && currentSubstInfo.SJRS != sjrs)
            {
                attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                var sjrsValue = sjrs ?? SubstrateJobRequestState.None;

                if (sjrsValue != SubstrateJobRequestState.None)
                {
                    attribUpdateNVS.SetValue(Constants.SubstrateJobRequestStateAttributeName, sjrsValue);
                }
                else
                {
                    attribUpdateNVSMergeBehavior |= NamedValueMergeBehavior.RemoveNull;        // turn on RemoveNull mode to the current NVS merge behavior
                    attribUpdateNVS.SetValue(Constants.SubstrateJobRequestStateAttributeName, null);
                }
            }

            if (sjs != null && currentSubstInfo.SJS != sjs)
            {
                attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                var sjsValue = sjs ?? SubstrateJobState.Initial;

                if (sjsValue != SubstrateJobState.Initial)
                {
                    attribUpdateNVS.SetValue(Constants.SubstrateJobStateAttributeName, sjsValue);
                }
                else
                {
                    attribUpdateNVSMergeBehavior |= NamedValueMergeBehavior.RemoveNull;        // turn on RemoveNull mode to the current NVS merge behavior
                    attribUpdateNVS.SetValue(Constants.SubstrateJobStateAttributeName, null);
                }
            }

            if (!attribUpdateNVS.IsNullOrEmpty())
                updateItemList.AddSetAttributesItem(objID: currentSubstInfo.ObjID, attributes: attribUpdateNVS, mergeBehavior: attribUpdateNVSMergeBehavior);

            if (addSyncExternalItem || updateBehavior.IsSet(E090StateUpdateBehavior.AddExternalSyncItem))
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return string.Empty;
        }

        public static string GenerateE090UpdateItems(this List<E039UpdateItem> updateItemList, E090SubstLocInfo currentSubstLocInfo, E087.SlotState ? mapSlotStateParam = null, string notAccessibleReasonParam = null, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false, bool ignoreCurrentSubstLocInfoValidity = false)
        {
            updateBehavior |= Settings.GenerateE090UpdateItemsBehaviorAdditions;

            if (currentSubstLocInfo.ObjID == null && updateItemList == null)
                return "{0}: given invalid or null parameter".CheckedFormat(Fcns.CurrentMethodName);

            if (!currentSubstLocInfo.ObjID.IsSubstLoc())
                return "{0}: given non-SubstLoc ObjID [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstLocInfo.ObjID);

            if (!currentSubstLocInfo.IsValid && !ignoreCurrentSubstLocInfoValidity)
                return "{0}: given unusable obj.  derived SubstLoc Info is not valid: [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstLocInfo);

            // the following logic will create the attribute update NVS if needed.  If it ends up being non-empty then an UpdateItem will be added for it.
            NamedValueSet attribUpdateNVS = null;
            NamedValueMergeBehavior attribUpdateNVSMergeBehavior = NamedValueMergeBehavior.AddAndUpdate;

            var mapSlotStateValue = mapSlotStateParam ?? E087.SlotState.Invalid;

            if (mapSlotStateParam != null && currentSubstLocInfo.MapSlotState != mapSlotStateParam)
            {
                attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                if (mapSlotStateValue != E087.SlotState.Invalid)
                {
                    attribUpdateNVS.SetValue(Constants.MapSlotStateAttributeName, ValueContainer.Create(mapSlotStateValue));

                }
                else
                {
                    attribUpdateNVS.SetValue(Constants.MapSlotStateAttributeName, ValueContainer.Empty);
                    attribUpdateNVSMergeBehavior |= NamedValueMergeBehavior.RemoveEmpty;
                }
            }

            if (notAccessibleReasonParam != null && currentSubstLocInfo.NotAccessibleReason != notAccessibleReasonParam)
            {
                attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                if (notAccessibleReasonParam.IsNeitherNullNorEmpty())
                {
                    attribUpdateNVS.SetValue(Constants.NotAccessibleReasonAttributeName, ValueContainer.Create(notAccessibleReasonParam));
                }
                else
                {
                    attribUpdateNVS.SetValue(Constants.NotAccessibleReasonAttributeName, ValueContainer.Empty);
                    attribUpdateNVSMergeBehavior |= NamedValueMergeBehavior.RemoveEmpty;
                }
            }

            if (!attribUpdateNVS.IsNullOrEmpty())
                updateItemList.AddSetAttributesItem(objID: currentSubstLocInfo.ObjID, attributes: attribUpdateNVS, mergeBehavior: attribUpdateNVSMergeBehavior);

            if (addSyncExternalItem || updateBehavior.IsSet(E090StateUpdateBehavior.AddExternalSyncItem))
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return string.Empty;
        }

        /// <summary>
        /// Returns true if the given obj's ID IsSubstrate (aka its type is "Substrate")
        /// </summary>
        public static bool IsSubstrate(this IE039Object obj)
        {
            return (obj != null && obj.ID.IsSubstrate());
        }

        /// <summary>
        /// Returns true if the given objID is of type "Substrate"
        /// </summary>
        public static bool IsSubstrate(this E039ObjectID objID)
        {
            return (objID != null && objID.Type == Constants.SubstrateObjectType);
        }

        /// <summary>
        /// Returns true if the given obj's ID IsSubstLoc (aka its type is "SubstLoc")
        /// </summary>
        public static bool IsSubstLoc(this IE039Object obj)
        {
            return (obj != null && obj.ID.IsSubstLoc());
        }

        /// <summary>
        /// Returns true if the given objID is of type "SubstLoc"
        /// </summary>
        public static bool IsSubstLoc(this E039ObjectID objID)
        {
            return (objID != null && objID.Type == Constants.SubstrateLocationObjectType);
        }

        private static Logging.ILogger logger = new Logging.Logger("E090.ExtensionMethods");

        /// <summary>
        /// Accepts a current <paramref name="startingSPS"/> and a <paramref name="mergeWithSPS"/>.  Returns the value between the two that has the higher priority.
        /// <para/>Priority (from least to greatest): NeedsProcessing, InProcess, ProcessStepCompleted, Processed, Stopped, Rejected, Skipped, Aborted, Lost
        /// </summary>
        public static SubstProcState MergeWith(this SubstProcState startingSPS, SubstProcState mergeWithSPS)
        {
            switch (startingSPS)
            {
                case SubstProcState.NeedsProcessing: if (mergeWithSPS.IsInProcess() || mergeWithSPS.IsProcessingComplete() || mergeWithSPS.IsProcessStepComplete()) return mergeWithSPS; break;
                case SubstProcState.InProcess: if (mergeWithSPS.IsProcessingComplete() || mergeWithSPS.IsProcessStepComplete()) return mergeWithSPS; break;
                case SubstProcState.ProcessStepCompleted: if (mergeWithSPS.IsProcessingComplete()) return mergeWithSPS; break;
                case SubstProcState.Processed: if (mergeWithSPS.IsProcessingComplete()) return mergeWithSPS; break;
                case SubstProcState.Stopped: if (mergeWithSPS == SubstProcState.Rejected || mergeWithSPS == SubstProcState.Skipped || mergeWithSPS == SubstProcState.Aborted || mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Rejected: if (mergeWithSPS == SubstProcState.Skipped || mergeWithSPS == SubstProcState.Aborted || mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Skipped: if (mergeWithSPS == SubstProcState.Aborted || mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Aborted: if (mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Lost: break;
                case SubstProcState.Created: return SubstProcState.Undefined;
                case SubstProcState.Moved: return SubstProcState.Undefined;
                case SubstProcState.Removed: return SubstProcState.Undefined;
                case SubstProcState.Undefined: return mergeWithSPS;
                default: return SubstProcState.Undefined;
            }

            return startingSPS;
        }

        /// <summary>
        /// If the given <paramref name="sps"/> is not SubstProcState.Undefined then its value is returned unchanged, otherwise the given <paramref name="mapToSPS"/> value is returned.
        /// </summary>
        public static SubstProcState MapUndefinedTo(this SubstProcState sps, SubstProcState mapToSPS)
        {
            return (sps != SubstProcState.Undefined) ? sps : mapToSPS;
        }

        /// <summary>
        /// Note: this is only intended to be applied to the E090 visible SPS value and captures the known permitted transitions for that value.  As such it does not contemplate transitions between pseudo states (except for an initial transition from Undefined)
        /// </summary>
        public static string GetSPSTransitionDenyReason(SubstProcState currentSPS, SubstProcState toSPS, bool requireInProcessBeforeProcessComplete = false, bool allowReturnToNeedsProcessing = false)
        {
            bool transitionIsPermitted = false;

            if (toSPS == currentSPS || currentSPS == SubstProcState.Undefined)
                transitionIsPermitted = true;
            else
            {
                switch (toSPS)
                {
                    case SubstProcState.NeedsProcessing:
                        if (currentSPS.IsInProcess() && allowReturnToNeedsProcessing)
                            transitionIsPermitted = true;
                        break;

                    case SubstProcState.InProcess:
                        if (currentSPS.IsNeedsProcessing())
                            transitionIsPermitted = true;
                        break;

                    case SubstProcState.Lost:
                        transitionIsPermitted = true;
                        break;

                    case SubstProcState.Skipped:
                        if (currentSPS.IsNeedsProcessing())
                            transitionIsPermitted = true;
                        break;

                    case SubstProcState.Processed:
                    case SubstProcState.Aborted:
                    case SubstProcState.Stopped:
                    case SubstProcState.Rejected:
                        if (currentSPS.IsInProcess())
                            transitionIsPermitted = true;
                        else if (!requireInProcessBeforeProcessComplete && currentSPS.IsNeedsProcessing())
                            transitionIsPermitted = true;
                        break;

                    default:
                        return "{0} failed: toSPS is not valid [{1}]".CheckedFormat(Fcns.CurrentMethodName, toSPS);
                }
            }

            if (transitionIsPermitted)
                return string.Empty;
            else
                return "Transition is not allowed [from:{0}, to:{1}]".CheckedFormat(currentSPS, toSPS);
        }
    }

    #endregion

    #region E090SubstLocObserver and E090SubstObserver objects

    /// <summary>
    /// Observer helper object for use with Substrate Location object publishers.  
    /// </summary>
    public class E090SubstLocObserver : E039.E039ObjectObserverWithInfoExtraction<E090SubstLocInfo>
    {
        /// <summary>Implicit cast operator to support implicit conversion of a E090SubstLocObserver object to the SubstLoc ObjID that it observes.  This implicit cast does not call UpdateOnCastIfNeeded</summary>
        public static implicit operator E039ObjectID(E090SubstLocObserver substLocObs) { return substLocObs.ID; }

        /// <summary>Implicit cast operator to support implicit conversion of a E090SubstLocObserver object to the E090SubstLocInfo for the SubstLoc object that it observes.</summary>
        public static implicit operator E090SubstLocInfo(E090SubstLocObserver substLocObs) { substLocObs.UpdateOnCastIfNeeded(); return substLocObs.Info; }

        /// <summary>Implicit cast operator to support implicit conversion of a E090SubstLocObserver object to the E090SubstInfo for the current Substrate object, if any, that the observed SubstLoc object is currently linked to Contain.</summary>
        public static implicit operator E090SubstInfo(E090SubstLocObserver substLocObs) { substLocObs.UpdateOnCastIfNeeded(); return substLocObs.ContainsSubstInfo; }

        /// <summary>Normal constructor.  Caller provides a <paramref name="substLocID"/>.  This method calls GetPublisher on it and then initializes itself using the obtained object publisher.  If the optional <paramref name="alsoObserveContents"/> is given as false then the ContainsObject and ContainsSubstInfo properties will not be updated and will remain in their initial default states.</summary>
        public E090SubstLocObserver(E039ObjectID substLocID, bool alsoObserveContents = true)
            : this(substLocID.GetPublisher(), alsoObserveContents: alsoObserveContents)
        { }

        /// <summary>Normal constructor.  Caller provides the object publisher instance to observe from.  If the optional <paramref name="alsoObserveContents"/> is given as false then the ContainsObject and ContainsSubstInfo properties will not be updated and will remain in their initial default states.</summary>
        public E090SubstLocObserver(ISequencedObjectSource<IE039Object, int> objLocPublisher, bool alsoObserveContents = true)
            : base(objLocPublisher, (obj) => new E090SubstLocInfo(obj))
        {
            AlsoObserveContents = alsoObserveContents;

            if (AlsoObserveContents)
                base.Add((obj) => UpdateContainsObject(obj));
        }

        /// <summary>
        /// "Copy" constructor.  
        /// <para/>Note: this constructor implicitly Updates the constructed observer so it may not give the same property values (Info, Object, ID, ...) if a new object has been published since the <paramref name="other"/> observer was last Updated.
        /// <para/>Note: this copy constructor does not copy the <paramref name="other"/>'s UpdateAndGetObjectUpdateActionArray.  Any desired object update actions for this new observer must be added explicitly.
        /// </summary>
        public E090SubstLocObserver(E090SubstLocObserver other)
            : this((other != null) ? other.ObjPublisher : null, (other != null) ? other.AlsoObserveContents : false)
        {
            if (AlsoObserveContents)
                base.Add((obj) => UpdateContainsObject(obj));
        }

        /// <summary>
        /// Debugging and logging helper method
        /// </summary>
        public override string ToString()
        {
            string baseStr = base.ToString();

            if (!AlsoObserveContents || ContainsObject == null)
                return baseStr;
            else
                return "{0} Contains:{1}".CheckedFormat(baseStr, ContainsSubstInfo);
        }

        /// <summary>When true this option selects that the ContainsObject and ContainsSubstInfo properties shall be set based on the object identified by the location's "Contains" link's ToID</summary>
        public bool AlsoObserveContents { get; private set; }

        protected virtual void UpdateContainsObject(IE039Object obj)
        {
            ContainsObject = Info.GetContainedE039Object();
            ContainsSubstInfo = new E090SubstInfo(ContainsObject);
        }

        /// <summary>Returns true if Info.IsOccupied and ContainsObject is neither Null nor Empty</summary>
        public bool IsOccupied
        {
            get { return Info.IsOccupied && !ContainsObject.IsNullOrEmpty(); }
        }

        /// <summary>Returns true if Info.IsUnoccupied and ContainsObject is Null or Empty</summary>
        public bool IsUnoccupied
        {
            get { return Info.IsUnoccupied && ContainsObject.IsNullOrEmpty(); }
        }

        /// <summary>If AlsoObserveContents is true then this gives the IE039Object that is the target of the Contains link, or null if the link does not exist or it's ToID is empty, otherwise this property returns null.</summary>
        public IE039Object ContainsObject { get; private set; }

        /// <summary>Gives the E090SubstInfo for the E039Object given in the ContainsObject property or default(E090SubstInfo) if AlsoObserveContents is false</summary>
        public E090SubstInfo ContainsSubstInfo { get; private set; }
    }

    /// <summary>
    /// Observer helper object for use with Substrate object publishers
    /// </summary>
    public class E090SubstObserver : E039.E039ObjectObserverWithInfoExtraction<E090SubstInfo>
    {
        /// <summary>Implicit cast operator to support implicit conversion of a E090SubstObserver object to the Substrate ObjID that it observes.  This implicit cast does not call UpdateOnCastIfNeeded</summary>
        public static implicit operator E039ObjectID(E090SubstObserver substObs) { return substObs.ID; }

        /// <summary>Implicit cast operator to support implicit conversion of a E090SubstObserver object to the E090SubstInfo for the Substrate object that it observes.</summary>
        public static implicit operator E090SubstInfo(E090SubstObserver substObs) { substObs.UpdateOnCastIfNeeded(); return substObs.Info; }

        /// <summary>Normal constructor.  Caller provides a <paramref name="substID"/>.  This method calls GetPublisher on it and then initializes itself using the obtained object publisher.</summary>
        public E090SubstObserver(E039ObjectID substID)
            : this(substID.GetPublisher())
        { }

        /// <summary>Normal constructor.  Caller provides the object publisher instance to observe from.</summary>
        public E090SubstObserver(ISequencedObjectSource<IE039Object, int> objPublisher)
            : base(objPublisher, (obj) => new E090SubstInfo(obj))
        { }

        /// <summary>
        /// "Copy" constructor.  
        /// <para/>Note: this constructor implicitly Updates the constructed observer so it may not give the same property values (Info, Object, ID) if a new object has been published since the <paramref name="other"/> observer was last Updated.
        /// <para/>Note: this copy constructor does not copy the <paramref name="other"/>'s UpdateAndGetObjectUpdateActionArray.  Any desired object update actions for this new observer must be added explicitly.
        /// </summary>
        public E090SubstObserver(E090SubstObserver other)
            : this((other != null) ? other.ObjPublisher : null)
        { }
    }
    #endregion

    #region E090SubstLocInfo, E090SubstInfo helper structs.

    /// <summary>
    /// This is a helper object that is generally used to extract Substrate Location related information from an SubstLoc IE039Object.
    /// <para/>At present all normal SubstLoc related information is derived from the "Contains", "SrcLoc" and "DestLoc" links from/to the given location.
    /// </summary>
    public struct E090SubstLocInfo : IEquatable<E090SubstLocInfo>
    {
        /// <summary>Implicit cast operator to support use implicit conversion of a E090SubstLocInfo object to the SubstLoc ObjID that is contained therein.</summary>
        public static implicit operator E039ObjectID(E090SubstLocInfo substLocInfo) { return substLocInfo.ObjID; }

        /// <summary>Normal constructor.  Caller must provide a non-empty Substrate Location type <paramref name="obj"/> [SubstLoc]</summary>
        public E090SubstLocInfo(IE039Object obj)
            : this()
        {
            Obj = obj;
            ObjID = (Obj != null) ? Obj.ID : E039ObjectID.Empty;

            INamedValueSet attributes = (Obj != null) ? Obj.Attributes : NamedValueSet.Empty;
            InstanceNum = attributes[Constants.InstanceNumAttributeName].VC.GetValue<int>(rethrow: false);
            MapSlotState = attributes[Constants.MapSlotStateAttributeName].VC.GetValue<Semi.E087.SlotState?>(rethrow: false);
            NotAccessibleReason = attributes[Constants.NotAccessibleReasonAttributeName].VC.GetValue<string>(rethrow: false).MapNullToEmpty();

            LinkToSubst = Obj.FindFirstLinkTo(Constants.ContainsLinkKey);
            SrcLinksToHere = Obj.FindLinksFrom(Constants.SourceLocationLinkKey).ToArray();
            DestLinksToHere = Obj.FindLinksFrom(Constants.DestinationLocationLinkKey).ToArray();
        }

        /// <summary>Copy constructor</summary>
        public E090SubstLocInfo(E090SubstLocInfo other)
            : this()
        {
            Obj = other.Obj;
            ObjID = other.ObjID;
            InstanceNum = other.InstanceNum;
            MapSlotState = other.MapSlotState;
            NotAccessibleReason = other.NotAccessibleReason;

            LinkToSubst = other.LinkToSubst;
            SrcLinksToHere = other.SrcLinksToHere.MakeCopyOf(mapNullToEmpty: false);
            DestLinksToHere = other.DestLinksToHere.MakeCopyOf(mapNullToEmpty: false);
        }

        /// <summary>Used to update the given <paramref name="nvs"/> to contain Attribute values for the properties in this object that represent Attribute values.</summary>
        public NamedValueSet UpdateAttributeValues(NamedValueSet nvs)
        {
            nvs.ConditionalSetValue(Constants.InstanceNumAttributeName, InstanceNum != 0, InstanceNum);
            nvs.ConditionalSetValue(Constants.MapSlotStateAttributeName, MapSlotState != null, MapSlotState);
            nvs.ConditionalSetValue(Constants.NotAccessibleReasonAttributeName, NotAccessibleReason.IsNeitherNullNorEmpty(), NotAccessibleReason);

            return nvs;
        }

        /// <summary>Gives the original object that this info object was constructed from, or null for the default constructor.</summary>
        public IE039Object Obj { get; private set; }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

        /// <summary>When non-zero, this property gives the instance number for this location.  This is typically used when the location is part of a set of locations that can be indexed.  In this case this property may be used to determine the location's index in the set.</summary>
        public int InstanceNum { get; set; }

        /// <summary>When non-null this gives the E087 SlotState that was last produced when mapping this location.</summary>
        public Semi.E087.SlotState? MapSlotState { get; set; }

        /// <summary>When non-empty this gives the reason why this substrate location should not be accessed now.</summary>
        public string NotAccessibleReason { get; set; }

        /// <summary>Gives the "Contains" link</summary>
        public E039Link LinkToSubst { get; set; }

        /// <summary>Gives the set of "SrcLoc" links from other objects to here.  Generally there should only be one.</summary>
        public E039Link[] SrcLinksToHere { get; set; }

        /// <summary>Gives the set of "DestLoc" links from other objects to here.  Generally there should only be one.</summary>
        public E039Link[] DestLinksToHere { get; set; }

        /// <summary>
        /// This property is used to indicate if the corresponding Substrate Location Object is Occupied, Unoccupied or is Undefined.
        /// If the object contains a valid LinkToSubst link (using the "Contains" key) then it returns Occupied/Unoccupied based on the link's ToID being non-empty/empty.
        /// Otherwise the property returns Undefined.  This typically happens if this object has been constructed from and E039Object that does not have a Contains link and is thus not a valid substrate location.
        /// </summary>
        public SubstLocState SLS 
        { 
            get 
            {
                if (!LinkToSubst.IsEmpty)
                    return (!LinkToSubst.IsToIDEmpty ? SubstLocState.Occupied : SubstLocState.Unoccupied);
                else
                    return SubstLocState.Undefined;
            } 
        }

        /// <summary>Returns true if the location is Occupied (it has a valid Contains link with a non-empty ToID)</summary>
        public bool IsOccupied { get { return SLS.IsOccupied(); } }

        /// <summary>Returns true if the location is Unocccupied (it has a valid Contains link with an empty ToID)</summary>
        public bool IsUnoccupied { get { return SLS.IsUnoccupied(); } }

        /// <summary>Returns true if the location is Occupied or Unocccupied (it has a valid Contains link, ToID can be empty or not)</summary>
        public bool IsValid { get { return SLS.IsValid(); } }

        /// <summary>
        /// Attempts to follow the LinkToSubst ("Contains") link and obtain the IE039Object for the current state of the linked object from its table.  
        /// Returns the resulting IE039Object, or the given <paramref name="fallbackObj"/> if the link could not be successfully followed.
        /// </summary>
        public IE039Object GetContainedE039Object(IE039Object fallbackObj = null)
        {
            return LinkToSubst.ToID.GetObject(fallbackObj);
        }

        private static readonly E039Link[] emptyLinkArray = EmptyArrayFactory<E039Link>.Instance;

        /// <summary>Gives an empty E090SubstInfo object to be used as a default value.</summary>
        public static E090SubstLocInfo Empty { get { return new E090SubstLocInfo() { ObjID = E039ObjectID.Empty, SrcLinksToHere = emptyLinkArray, DestLinksToHere = emptyLinkArray }; } }

        /// <summary>Returns true if the contents are the same as the contents of the Empty E090SubstInfo object.  This is NOT the same as indicating that the current location is Unoccupied</summary>
        public bool IsEmpty { get { return this.Equals(Empty); } }

        /// <summary>
        /// Returns true if the contents of this object match the contentents of the given <paramref name="other"/> object.
        /// <para/>NOTE: this test does not look at the contents of the Obj property.
        /// </summary>
        public bool Equals(E090SubstLocInfo other)
        {
            return (ObjID.Equals(other.ObjID)
                    && InstanceNum == other.InstanceNum
                    && LinkToSubst.Equals(other.LinkToSubst)
                    && SrcLinksToHere.IsEqualTo(other.SrcLinksToHere)
                    && DestLinksToHere.IsEqualTo(other.DestLinksToHere)
                    );
        }

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return ToString(includeSubstLocID: true);
        }

        /// <summary>Debugging and logging helper</summary>
        public string ToString(bool includeSubstLocID)
        {
            if (IsEmpty)
                return "E090SubstLocInfo Empty";

            StringBuilder sb = new StringBuilder("E090SubstLocInfo");

            if (includeSubstLocID)
                sb.CheckedAppendFormat(" {0}", ObjID.ToString(E039ToStringSelect.FullName));

            sb.CheckedAppendFormat(" {0}", SLS);

            if (InstanceNum != 0)
                sb.CheckedAppendFormat(" InstNum:{0}", InstanceNum);

            if (!LinkToSubst.IsToIDEmpty)
                sb.CheckedAppendFormat(" Contains:{0}", LinkToSubst.ToID.ToString(E039ToStringSelect.FullName));

            if (!SrcLinksToHere.IsNullOrEmpty())
                sb.CheckedAppendFormat(" SrcOf:{0}", String.Join(",", SrcLinksToHere.Select(link => link.FromID.ToString(E039ToStringSelect.FullName))));

            if (!DestLinksToHere.IsNullOrEmpty())
                sb.CheckedAppendFormat(" DestOf:{0}", String.Join(",", DestLinksToHere.Select(link => link.FromID.ToString(E039ToStringSelect.FullName))));

            return sb.ToString();
        }
    }

    /// <summary>
    /// This is a helper object that is generally used to extract Substrate related information from an Substrate IE039Object.
    /// <para/>This includes information obtained from the "SubstProcState" (SPS), "SubstState" (STS), "LotID", and "SubstUsage" attributes,
    /// information from optional "PendingSPS", "SPSList", "SPSLocList", and "SPSDateTimeList" attrubutes, 
    /// and information that is derived from the "Contains", "SrcLoc" and "DestLoc" links to/from this object.
    /// Now also includes use of optional SubstrateJobRequestState (SJRS) and SubsrateJobState (SJS) attibutes.
    /// </summary>
    public struct E090SubstInfo : IEquatable<E090SubstInfo>
    {
        /// <summary>Implicit cast operator to support use implicit conversion of a E090SubstInfo object to the Substrate ObjID that is contained therein.</summary>
        public static implicit operator E039ObjectID(E090SubstInfo substInfo) { return substInfo.ObjID; }

        /// <summary>Normal constructor.  Caller must provide a non-empty Substrate type <paramref name="obj"/> [Substrate]</summary>
        public E090SubstInfo(IE039Object obj) 
            : this()
        {
            Obj = obj;
            _objID = (Obj != null) ? Obj.ID : null;

            INamedValueSet attributes = (Obj != null) ? Obj.Attributes : NamedValueSet.Empty;
            
            SPS = attributes[Constants.SubstrateProcessingStateAttributeName].VC.GetValue(rethrow: false, defaultValue: SubstProcState.Undefined);
            STS = attributes[Constants.SubstrateTransportStateAttributeName].VC.GetValue(rethrow: false, defaultValue: SubstState.Undefined);
            PendingSPS = attributes[Constants.PendingSubstrateProcessingStateAttributeName].VC.GetValue<SubstProcState>(rethrow: false, defaultValue: SubstProcState.Undefined);
            LotID = attributes[Constants.LotIDAttributeName].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
            SubstUsageStr = attributes[Constants.SubstrateUsageAttributeName].VC.GetValue<string>(rethrow: false).MapNullToEmpty();

            LocsLinkedToHere = Obj.FindLinksFrom(Constants.ContainsLinkKey).ToArray();
            LinkToSrc = Obj.FindFirstLinkTo(Constants.SourceLocationLinkKey);
            LinkToDest = Obj.FindFirstLinkTo(Constants.DestinationLocationLinkKey);

            SJRS = attributes[Constants.SubstrateJobRequestStateAttributeName].VC.GetValue<SubstrateJobRequestState>(rethrow: false);
            SJS = attributes[Constants.SubstrateJobStateAttributeName].VC.GetValue<SubstrateJobState>(rethrow: false);

            _deriveListsFromAttributes = attributes.ConvertToReadOnly();
            // actual lists/arrays will be extracted and retained from these attributes on request.
        }

        /// <summary>Copy constructor</summary>
        public E090SubstInfo(E090SubstInfo other)
            : this()
        {
            Obj = other.Obj;
            _objID = other._objID;

            SPS = other.SPS;
            STS = other.STS;
            PendingSPS = other.PendingSPS;
            LotID = other.LotID;
            SubstUsageStr = other.SubstUsageStr;

            LocsLinkedToHere = other.LocsLinkedToHere.MakeCopyOf(mapNullToEmpty: false);
            LinkToSrc = other.LinkToSrc;
            LinkToDest = other.LinkToDest;

            SJRS = other.SJRS;
            SJS = other.SJS;

            // only make copies of the items that have been derived and/or obtained already in the other copy from object.
            _deriveListsFromAttributes = other._deriveListsFromAttributes;
            _spsList = other._spsList.MakeCopyOf(mapNullToEmpty: false);
            _spsLocList = other._spsLocList;
            _spsDateTimeList = other._spsDateTimeList.MakeCopyOf(mapNullToEmpty: false);
            _zippedItemArray = other._zippedItemArray.MakeCopyOf(mapNullToEmpty: false);
        }

        private static readonly E039Link[] emptyLinkArray = EmptyArrayFactory<E039Link>.Instance;

        /// <summary>Used to update the given <paramref name="nvs"/> to contain Attribute values for the properties in this object that represent Attribute values.</summary>
        public NamedValueSet UpdateAttributeValues(NamedValueSet nvs)
        {
            nvs.SetValue(Constants.SubstrateProcessingStateAttributeName, SPS);
            nvs.SetValue(Constants.SubstrateTransportStateAttributeName, STS);

            nvs.ConditionalSetValue(Constants.PendingSubstrateProcessingStateAttributeName, PendingSPS != SubstProcState.Undefined, PendingSPS);
            nvs.ConditionalSetValue(Constants.SPSListAttributeName, !SPSList.IsNullOrEmpty(), (SPSList ?? EmptyArrayFactory<SubstProcState>.Instance).Select(sps => sps.ToString()));
            nvs.ConditionalSetValue(Constants.SPSLocListAttributeName, !SPSLocList.IsNullOrEmpty(), SPSLocList);
            nvs.ConditionalSetValue(Constants.SPSDateTimeListAttributeName, !SPSDateTimeList.IsNullOrEmpty(), (SPSDateTimeList ?? EmptyArrayFactory<DateTime>.Instance).Select(dt => dt.ToString("o")));
            nvs.ConditionalSetValue(Constants.LotIDAttributeName, !LotID.IsNullOrEmpty() || nvs.Contains("LotID"), LotID);
            nvs.ConditionalSetValue(Constants.SubstrateUsageAttributeName, !SubstUsageStr.IsNullOrEmpty() || nvs.Contains("SubstUsage"), SubstUsageStr);
            nvs.ConditionalSetValue(Constants.SubstrateJobRequestStateAttributeName, SJRS != SubstrateJobRequestState.None, SJRS);
            nvs.ConditionalSetValue(Constants.SubstrateJobStateAttributeName, SJS != SubstrateJobState.Initial, SJS);

            return nvs;
        }

        /// <summary>Gives the original object that this info object was constructed from, or null for the default constructor.</summary>
        public IE039Object Obj { get; private set; }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

        /// <summary>Returns the E039ObjectFlags from the associated Obj or E039ObjectFlags.None if there is no such object.</summary>
        public E039ObjectFlags ObjFlags { get { return (Obj != null) ? Obj.Flags : E039ObjectFlags.None; } }

        /// <summary>Returns true when the associated Obj's Flags have been marked with the E039ObjectFlags.IsFinal flag to indicate that this is the final publication for the object (after which it has been removed from the table)</summary>
        public bool IsFinal { get { return ObjFlags.IsFinal(); } }

        /// <summary>From SubstState attribute</summary>
        public SubstState STS { get; set; }

        /// <summary>From SubstProcState attribute</summary>
        public SubstProcState SPS { get; set; }

        /// <summary>Optional, From PendingSPS attribute.  Undefined if the substrate does not have this attributes.</summary>
        public SubstProcState PendingSPS { get; set; }

        /// <summary>This property returns the SPS that is inferred by merging the SPS with the PendingSPS (if any)</summary>
        public SubstProcState InferredSPS { get { return SPS.MergeWith(PendingSPS); } }

        /// <summary>From LotID attribute</summary>
        public string LotID { get; set; }

        /// <summary>From SubstUsage attribute</summary>
        public string SubstUsageStr { get; set; }

        /// <summary>SubstUsage enum version of SubstUsageStr property (and SubstUsage attribute)</summary>
        public SubstUsage SubstUsage
        {
            get { return SubstUsageStr.IsNullOrEmpty() ? SubstUsage.Undefined : SubstUsageStr.TryParse<SubstUsage>(parseFailedResult: SubstUsage.Other); }
            set { SubstUsageStr = value.ToString(); }
        }

        /// <summary>Proxy for SubstLocID attribute - Gives the Name of the FromID for the first Contains link from that links to this object [Contains]</summary>
        public string LocID { get { return _locID.MapNullToEmpty(); } set { _locID = value; } }
        private string _locID;

        /// <summary>Gives the set of links that are linked to this object using the "Contains" key.</summary>
        public E039Link[] LocsLinkedToHere 
        { 
            get { return _locsLinkedToHere; }
            set
            {
                _locsLinkedToHere = value;
                LocID = value.SafeAccess(0).FromID.Name;
            }
        }
        private E039Link[] _locsLinkedToHere;

        /// <summary>Proxy link for SubstSource attribute - gives link to the SubstLoc object which is the Source location for this substrate. [SrcLoc]</summary>
        public E039Link LinkToSrc { get; set; }

        /// <summary>Proxy link for SubstDestination attribute- gives link to the SubstLoc object which is the Destination location for this substrate. [DestLoc]</summary>
        public E039Link LinkToDest { get; set; }

        /// <summary>Gives the SJRS SubstrateJobRequestState attribute value.  The corresponding attribute will only be included/used if this value is not None</summary>
        public SubstrateJobRequestState SJRS { get; set; }

        /// <summary>Gives the SJS SubstrateJobState attribute value.  The corresponding attribute will only be included/used if this value is not Initial</summary>
        public SubstrateJobState SJS { get; set; }

        private INamedValueSet _deriveListsFromAttributes;

        /// <summary>Optional, an accumlation of SPS attributes that have been assigned to this substrate.  Empty if the substrate does not have this attributes.</summary>
        public SubstProcState[] SPSList { get { return _spsList ?? (_spsList = GetSPSListFromSavedAttributes()); } set { _spsList = value; } }
        private SubstProcState[] _spsList;

        /// <summary>Optional, an accumlation of the locations at which SPS values have been assigned to this substrate.  Empty if the substrate does not have this attributes.</summary>
        public IList<string> SPSLocList { get { return _spsLocList ?? (_spsLocList = GetSPSLocListFromSavedAttributes()); } set { _spsLocList = value.ConvertToReadOnly(); } }
        private ReadOnlyIList<string> _spsLocList;

        /// <summary>Optional, an accumulation of the DateTime's (local time on "o" format) at which SPS values have been appended to the SPSList.  Empty if the substrate does not have this attribute.</summary>
        public DateTime[] SPSDateTimeList { get { return _spsDateTimeList ?? (_spsDateTimeList = GetSPSDateTimeListFromSavedAttributes()); } set { _spsDateTimeList = value; } }
        private DateTime[] _spsDateTimeList;

        /// <summary>
        /// This is the per item representation for the zip up of the SPSList, SPSLocList and SPSDateTimeList
        /// </summary>
        public struct Item : IEquatable<Item>
        {
            /// <summary>SubstProcState (SPS) from SPSList for this zipped item</summary>
            public SubstProcState SPS { get; set; }

            /// <summary>string Loc name from SPSLocList for this zipped item</summary>
            public string Loc { get; set; }

            /// <summary>DateTime from SPSDateTimeList for this zipped item</summary>
            public DateTime DateTime { get; set; }

            /// <summary>Debugging and logging helper method.</summary>
            public override string ToString()
            {
                string dtStr = DateTime.IsZero() ? "" : " {0}".CheckedFormat(DateTime.ToString("o"));
                
                return "sps:{0} loc:{1}{2}".CheckedFormat(SPS, Loc, dtStr);
            }

            /// <summary>Returns true if this and the given <paramref name="other"/> have the same contents.</summary>
            public bool Equals(Item other)
            {
                return (SPS == other.SPS
                        && Loc == other.Loc
                        && DateTime == other.DateTime
                        );
            }
        }

        /// <summary>
        /// This is the zipped representation list for the combination of the SPSList, the SPSLocList and the SPSDateTimeList.  It is only useful if the three lists are appending in a uniform manner.
        /// <para/>Note: this is only populated when construting an E090SubstInfo object from a corresponding Substrate IE039Object.  This is never used when generating attribute values for a new IE039Object.
        /// </summary>
        public Item[] ZippedItemArray { get { return _zippedItemArray ?? (_zippedItemArray = GetZippedItemArraySavedAttributes()); } }
        private Item[] _zippedItemArray;

        private SubstProcState[] GetSPSListFromSavedAttributes()
        {
            INamedValueSet attributes = _deriveListsFromAttributes.MapNullToEmpty();
            return (attributes[Constants.SPSListAttributeName].VC.GetValue<ReadOnlyIList<string>>(rethrow: false) ?? ReadOnlyIList<string>.Empty).Select(str => str.TryParse<SubstProcState>()).ToArray();
        }

        private ReadOnlyIList<string> GetSPSLocListFromSavedAttributes()
        {
            INamedValueSet attributes = _deriveListsFromAttributes.MapNullToEmpty();
            return (attributes[Constants.SPSLocListAttributeName].VC.GetValue<ReadOnlyIList<string>>(rethrow: false) ?? ReadOnlyIList<string>.Empty);
        }

        private DateTime[] GetSPSDateTimeListFromSavedAttributes()
        {
            INamedValueSet attributes = _deriveListsFromAttributes.MapNullToEmpty();
            return (attributes[Constants.SPSDateTimeListAttributeName].VC.GetValue<ReadOnlyIList<string>>(rethrow: false) ?? ReadOnlyIList<string>.Empty).Select(str => { try { return DateTime.ParseExact(str, "o", null); } catch { return default(DateTime); } }).ToArray();
        }

        private Item[] GetZippedItemArraySavedAttributes()
        {
            var spsList = SPSList;
            if (!spsList.IsNullOrEmpty())
            {
                var spsLocList = SPSLocList;
                var spsDateTimeList = SPSDateTimeList;

                return SPSList.Select((sps, index) => new Item() { SPS = sps, Loc = spsLocList.SafeAccess(index).MapNullToEmpty(), DateTime = spsDateTimeList.SafeAccess(index) }).ToArray();
            }
            else
            {
                return EmptyArrayFactory<Item>.Instance;
            }
        }

        /// <summary>Gives an empty E090SubstInfo object to be used as a default value.</summary>
        public static E090SubstInfo Empty { get { return new E090SubstInfo() { LotID = "", SubstUsageStr = "", STS = SubstState.Undefined, SPS = SubstProcState.Undefined, PendingSPS = SubstProcState.Undefined, LocsLinkedToHere = emptyLinkArray, SPSList = EmptyArrayFactory<SubstProcState>.Instance, SPSLocList = ReadOnlyIList<string>.Empty, SPSDateTimeList = EmptyArrayFactory<DateTime>.Instance }; } }

        /// <summary>Returns true if the contents are the same as the contents of the Empty E090SubstInfo object.</summary>
        public bool IsEmpty { get { return this.Equals(Empty); } }

        /// <summary>
        /// Gives the initial E090SubstInfo to be used with newly created E090 objects.
        /// <para/>Sets: LotID = "", SubstUsage = Product, STS = AtSource, and SPS = NeedsProcessing.
        /// </summary>
        public static E090SubstInfo Initial { get { return new E090SubstInfo() { LotID = "", SubstUsage = SubstUsage.Product, STS = SubstState.AtSource, SPS = SubstProcState.NeedsProcessing, PendingSPS = SubstProcState.Undefined }; } }

        /// <summary>
        /// Returns true if this object's contents are equal to the contents of the given <paramref name="other"/> object.
        /// <para/>NOTE: this test does not look at the contents of the Obj property.
        /// </summary>
        public bool Equals(E090SubstInfo other)
        {
            return (ObjID.Equals(other.ObjID)
                    && STS == other.STS
                    && SPS == other.SPS
                    && PendingSPS == other.PendingSPS
                    && LotID == other.LotID
                    && SubstUsageStr == other.SubstUsageStr
                    && _locID == other._locID
                    && _locsLinkedToHere.IsEqualTo(other._locsLinkedToHere)
                    && LinkToSrc.Equals(other.LinkToSrc)
                    && LinkToDest.Equals(other.LinkToDest)
                    && SJRS == other.SJRS
                    && SJS == other.SJS
                    && SPSList.IsEqualTo(other.SPSList)
                    && SPSLocList.IsEqualTo(other.SPSLocList)
                    && SPSDateTimeList.IsEqualTo(other.SPSDateTimeList)
                    // do not explicitly compare the ZippedItemArray as it is generated on demand from other values that are already being compared here.
                );
        }

        /// <summary>
        /// Returns true if this ObjID is non-empty, STS and SPS are both IsValid, LocsLinkedToHere has one link and neither LinkToSrc nor LinkToDest are IsEmpty
        /// </summary>
        public bool IsValid 
        { 
            get 
            { 
                return (!ObjID.IsEmpty
                        && STS.IsValid() 
                        && SPS.IsValid() 
                        && (LocsLinkedToHere.SafeLength() == 1)
                        && !LinkToSrc.IsEmpty 
                        && !LinkToDest.IsEmpty); 
            } 
        }

        /// <summary>
        /// genertes inferred versions of given <paramref name="locID"/> and <paramref name="sps"/>.  
        /// Uses these to obtain inferred STS as
        /// AtSource if inferred sps is NeedsProcessing and inferred locID matches SrcLocID
        /// AtDestination if inferred sps is ProcessingComplete and inferred locID matches DestLocID
        /// otherwise AtWork
        /// </summary>
        public SubstState GetInferredSTS(string locID = null, SubstProcState ? sps = null)
        {
            return GetInferredSTS(locID.MapNullOrEmptyTo(LocID), sps ?? SPS.MergeWith(PendingSPS));
        }

        /// <summary>
        /// AtSource if given <paramref name="sps"/> is NeedsProcessing and given <paramref name="locID"/> matches SrcLocID
        /// AtDestination if given <paramref name="sps"/> is ProcessingComplete and given <paramref name="locID"/> matches DestLocID
        /// AtSource if given <paramref name="sps"/> is Skipped and given <paramref name="locID"/> matches SrcLocID but does not match DestLocID
        /// otherwise AtWork
        /// </summary>
        public SubstState GetInferredSTS(string locID, SubstProcState sps)
        {
            if (sps.IsNeedsProcessing() && locID == LinkToSrc.ToID.Name)
                return SubstState.AtSource;
            else if (sps.IsProcessingComplete() && locID == LinkToDest.ToID.Name)
                return SubstState.AtDestination;            
            else if (sps == SubstProcState.Skipped && locID == LinkToSrc.ToID.Name)
                return SubstState.AtSource; // skipped at source is only used if the source and destination locations are different, otherwise the value will typcially be Skipped AtDestination
            else
                return SubstState.AtWork;
        }

        /// <summary>
        /// Returns the currently inferred STS: AtSource if NeedsProcessing and current location is Source location, AtDestination if ProcessingComplete and current location is destination location, or AtWork otherwise.
        /// </summary>
        public SubstState InferredSTS { get { return GetInferredSTS(); } }

        /// <summary>Debugging and logging helper</summary>
        public override string ToString()
        {
            return ToString(includeSubstID: true);
        }

        /// <summary>Debugging and logging helper</summary>
        public string ToString(bool includeSubstID)
        {
            if (IsEmpty || (Obj == null && ObjID.IsEmpty))
                return "E90SubstInfo Empty";

            StringBuilder sb = new StringBuilder("E90SubstInfo");

            if (includeSubstID)
                sb.CheckedAppendFormat(" {0}", ObjID.ToString(E039ToStringSelect.FullName));

            sb.CheckedAppendFormat(" {0} {1}", STS, SPS);

            if (PendingSPS != SubstProcState.Undefined)
                sb.CheckedAppendFormat(" PendingSPS:{0}", PendingSPS);

            if (!LotID.IsNullOrEmpty())
                sb.CheckedAppendFormat(" LotID:{0}", new ValueContainer(LotID));

            if (!SubstUsageStr.IsNullOrEmpty())
                sb.CheckedAppendFormat(" Usage:{0}", new ValueContainer(SubstUsageStr));

            if (!LocsLinkedToHere.IsNullOrEmpty())
            {
                if (LocsLinkedToHere.SafeLength() == 1)
                    sb.CheckedAppendFormat(" Loc:{0}", LocID);
                else
                    sb.CheckedAppendFormat(" LocsLinkedHere:{0}", String.Join(",", LocsLinkedToHere.Select(link => link.FromID.ToString(E039ToStringSelect.FullName))));
            }

            if (!LinkToSrc.IsToIDEmpty)
                sb.CheckedAppendFormat(" Src:{0}", LinkToSrc.ToID.ToString(E039ToStringSelect.FullName));

            if (!LinkToDest.IsToIDEmpty)
                sb.CheckedAppendFormat(" Dest:{0}", LinkToDest.ToID.ToString(E039ToStringSelect.FullName));

            if (SJRS != SubstrateJobRequestState.None)
                sb.CheckedAppendFormat(" SJRS:{0}", SJRS);

            if (SJS != SubstrateJobState.Initial)
                sb.CheckedAppendFormat(" SJS:{0}", SJS);

            return sb.ToString();
        }
    }

    #endregion

    #region SubstState, SubstProcState, related Event enumerations, and related extension methods

    //-------------------------------------------------------------------
    // E090-0306

    /// <summary>
    /// SubstState: Substrate (Transport) State (STS)
    /// Gives a summary of where the substrate it in the tool.
    /// <para/>AtSource = 0, AtWork  = 1, AtDestination = 2, Undefined = -1
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstState : int
    {
        /// <summary>Substrate is located at its "source" location, where it was originally received.  If the source and destination are the same location then we destinguish source from destination based on whether it has been processed. [0]</summary>
        [EnumMember]
        AtSource = 0,

        /// <summary>Substrate is out in the tool (essentially it is neither AtSource nor AtDestination). [1]</summary>
        [EnumMember]
        AtWork = 1,

        /// <summary>Substrate is located at its "destination" location.  If the source and destination are the same location then we destinguish source from destination based on whether it has been processed. [2]</summary>
        [EnumMember]
        AtDestination = 2,

        /// <summary>Local default value to use when there is no valid value. [-1]</summary>
        [EnumMember]
        Undefined = -1,
    }

    /// <summary>
    /// SubstProcState: Substrate Processing State (SPS)
    /// Gives a summary of if the substrate has been processed and, if it was not processed successfully, why.
    /// This enumeration is divided into values that follow the E090 standard and other values that are used internally (typically as a PendingSPS) but which are not generally visible outside of this namespace.
    /// <para/>NeedsProcessing = 0, InProcess = 1, Processed = 2, Aborted = 3, Stopped = 4, Rejected = 5, Lost = 6, Skipped = 7, 
    /// <para/>Undefined = -1, 
    /// <para/>PseudoSPS values: Created = -2, Moved = -3, Removed = -4, ProcessStepCompleted = -5
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstProcState : int
    {
        /// <summary>Substrate has not been fully processed yet.  This is the default state for newly created substrates. [0]</summary>
        [EnumMember]
        NeedsProcessing = 0,

        /// <summary>Substrate is being processed.  Substrate properties are being changed or measured by the equipment. [1]</summary>
        [EnumMember]
        InProcess = 1,

        /// <summary>Substrate has been processed successfully.  No more processing will take place while the Substrate remains in this equipment. [2]</summary>
        [EnumMember]
        Processed = 2,

        /// <summary>Processing of this substrate was aborted during processing.  Additional information may be required to determine this substrate's final state. [3]</summary>
        [EnumMember]
        Aborted = 3,

        /// <summary>Processing of this Substrate was stopped during processing.  Additional information may be required to determine this substrate's final state. [4]</summary>
        [EnumMember]
        Stopped = 4,

        /// <summary>The Substrate has completed all processing steps but one or more of them may have had an issue during processing.  Additional information may be required to determine this substrate's final state. [5]</summary>
        [EnumMember]
        Rejected = 5,

        /// <summary>The Substrate has been lost by the equipment, or was manually removed by an external entity/decision authority, and its loss/removal has been reported to the equipment. [6]</summary>
        [EnumMember]
        Lost = 6,

        /// <summary>As directed by a suitable decision authority, the equipment has been told to skip processing for this substrate. [7]</summary>
        [EnumMember]
        Skipped = 7,

        /// <summary>Local default value to use when there is no valid value. [-1]</summary>
        [EnumMember]
        Undefined = -1,

        /// <summary>PseudoSPS value: Optionally added to SPSList when substrate has been created. [-2]</summary>
        [EnumMember]
        Created = -2,

        /// <summary>PseudoSPS value: Optionally added to SPSList when substrate has been moved to a new location. [-3]</summary>
        [EnumMember]
        Moved = -3,

        /// <summary>PseudoSPS value: Optionally added to SPSList when substrate has been removed. [-4]</summary>
        [EnumMember]
        Removed = -4,

        /// <summary>PseudoSPS value: Used as PendingSPS value to indicate that a processing step has been completed, typically at a process module location.  The use of this PendingSPS indicates that the current process step was completed successfully.  If this was the last processing step then the PendingSPS and the final SPS may be set to Processed to indicate normal and successful completion of all processing steps. [-5]</summary>
        [EnumMember]
        ProcessStepCompleted = -5,
    }

    /// <summary>
    /// SubstIDStatus: Substrate ID verifcation state (SVS)
    /// Gives a summary of the information known about the equipements use of an substrate id reader to verify the substrate id information that was provided by the host during substrate reception.
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstIDStatus : int
    {
        /// <summary>0: Entry substrate id status for recived substrates when substrate id reader is available and is enabled for use in this equipment.</summary>
        [EnumMember]
        NotConfirmed = 0,

        /// <summary>1: Substrate id status when substrate id could not be acquired by reader, or it was not the expected value for this substrate (does, or does not match another reported substrate id)</summary>
        [EnumMember]
        WaitingForHost = 1,

        /// <summary>2: The substrate id has been accepted either through equipment or host based verification.</summary>
        [EnumMember]
        Confirmed = 2,

        /// <summary>3: The host has canceled the substrate.</summary>
        [EnumMember]
        ConfirmationFailed = 3,

        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        [EnumMember]
        Undefined = -1,
    }

    /// <summary>
    /// Valid set of state transitions for the Substrate state related states (SubstState, SubstProcState, SubstIDStatus)
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstState_Transition : int
    {
        /// <summary>1: no state -> AtSource: Substrate object has been received/constructed.</summary>
        [EnumMember]
        Transition1 = 1,

        /// <summary>2: AtSource -> AtWork: The substrate has been moved from its source location into the equipment</summary>
        [EnumMember]
        Transition2 = 2,

        /// <summary>3: AtWork -> AtSource: The substrate has been returned to its source location.</summary>
        [EnumMember]
        Transition3 = 3,

        /// <summary>4: AtWork -> AtWork: The substrate has been moved within the equipment.</summary>
        [EnumMember]
        Transition4 = 4,

        /// <summary>5: AtWork -> AtDestination: The substrate has been moved to its destination location</summary>
        [EnumMember]
        Transition5 = 5,

        /// <summary>6: AtDestination -> AtWork: The substrate has been moved from its destination location back into the equipment</summary>
        [EnumMember]
        Transition6 = 6,

        /// <summary>7: AtDestination -> extinction: The substrate object has been removed from the equipment normally</summary>
        [EnumMember]
        Transition7 = 7,

        /// <summary>8: AtDestination -> AtSource: The user, decision authority, or equipment has detected, and informed the equipment (itself) that the substrate is actually AtSource</summary>
        [EnumMember]
        Transition8 = 8,

        /// <summary>9: any state-> extinction: The user, decision authority, or equipment has detected, and informed the equipment (itself) that the substrate has been removed abnormally.</summary>
        [EnumMember]
        Transition9 = 9,

        /// <summary>10: no state -> NeedsProcessing: Substrate object has been received/constructed.</summary>
        [EnumMember]
        Transition10 = 10,

        /// <summary>11: NeedsProcessing -> InProcess: Substrate processing has started in the equipment.</summary>
        [EnumMember]
        Transition11 = 11,

        /// <summary>12: InProcess -> ProcessingComplete (Processed, Aborted, Stopped, Rejected, Lost, Skipped):  Substrate proessing was completed normally or abnormally or the substrate was unexpectedly removed from the equipment.</summary>
        [EnumMember]
        Transition12 = 12,

        /// <summary>13: InProcess -> NeedsProcessing: One phase of the intended processing for this substrate has been completed and there are more processing phases to complete within the equipment before the substrate will be fully Processed.</summary>
        [EnumMember]
        Transition13 = 13,

        /// <summary>14: NeedsProcessing -> ProcessingComplete (Skipped, or Lost): The equipment has been told to skip processing of this substrate (normal), or the substrate has been marked lost (abnormal) by the user or decision authority.</summary>
        [EnumMember]
        Transition14 = 14,

        /// <summary>16: no state -> NotConfirmed: Substrate object has been received/constructed.</summary>
        [EnumMember]
        Transition16 = 16,

        /// <summary>17: NotConfirmed -> Confirmed: The equipment successfully acquired a Substrate ID from this substrate and it matches the id that was used when the substrate object was instantiated. (SubstID, AcquiredID)</summary>
        [EnumMember]
        Transition17 = 17,

        /// <summary>18: NotConfirmed -> WaitingForHost: The equipment was unable to succesfully acquire a Substrate ID for this substrate after repeated attempts. (SubstID)</summary>
        [EnumMember]
        Transition18 = 18,

        /// <summary>19: NotConfirmed -> WaitingForHost: Substrate ID was successfully read/acquired but acquired id is different than the one the equipment used to instantiate the substrate object. (SubstID, AcquiredID)</summary>
        [EnumMember]
        Transition19 = 19,

        /// <summary>20: WaitingForHost -> Confirmed: Eqiupment was told to ProceedWithSubstrate for this substrate (SubstID, AcquiredID).</summary>
        [EnumMember]
        Transition20 = 20,

        /// <summary>21: WaitingForHost -> ConfirmationFailed: Equipment was told to CancelSubstrate for this substrate.</summary>
        [EnumMember]
        Transition21 = 21,
    }


    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstLocState : int
    {
        /// <summary>0: The location is not currently occupied by a Substrate.</summary>
        [EnumMember]
        Unoccupied = 0,

        /// <summary>1: The location is currently occupied by a Substrate.</summary>
        [EnumMember]
        Occupied = 1,

        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        [EnumMember]
        Undefined = -1,
    }

    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstLocState_Transition : int
    {
        /// <summary>1: Unoccupied -> Occupied: A substrate object was moved to this location.</summary>
        [EnumMember]
        Transition1 = 1,

        /// <summary>2: Occupied -> Unoccupied: A substrate object was removed from this location.</summary>
        [EnumMember]
        Transition2 = 2,
    }

    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum BatchLocState : int
    {
        /// <summary>0: The location is not currently occupied by a Batch.</summary>
        [EnumMember]
        Unoccupied = 0,

        /// <summary>1: The location is currently occupied by a Batch.</summary>
        [EnumMember]
        Occupied = 1,

        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        [EnumMember]
        Undefined = -1,
    }

    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum BatchLocState_Transition : int
    {
        /// <summary>1: Unoccupied -> Occupied: A group of substrate objects was moved to this batch location.</summary>
        [EnumMember]
        Transition1 = 1,

        /// <summary>2: Occupied -> Unoccupied: A group of substrate objects was removed from this batch location.</summary>
        [EnumMember]
        Transition2 = 2,
    }

    /// <summary>
    /// Substrate Type enumeration:
    /// <para/>Wafer (0), FlatPanel (1), CD (2), Mask (3), Undefined (-1)
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstType : sbyte
    {
        [EnumMember]
        Wafer = 0,

        [EnumMember]
        FlatPanel = 1,

        [EnumMember]
        CD = 2,

        [EnumMember]
        Mask = 3,

        /// <summary>Local default value to use when there is no valid value. [-1]</summary>
        [EnumMember]
        Undefined = -1,
    }

    /// <summary>
    /// Substrate Usage enumeration:
    /// <para>Product (0), Test (1), Filler (2), Undefined (-1), Other (-2)</para>
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstUsage : sbyte
    {
        [EnumMember]
        Product = 0,

        [EnumMember]
        Test = 1,

        [EnumMember]
        Filler = 2,

        /// <summary>Local default value to use when there is no valid value. [-1]</summary>
        [EnumMember]
        Undefined = -1,

        [EnumMember]
        Other = -2,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given <paramref name="sls"/> value is Occupied</summary>
        public static bool IsOccupied(this SubstLocState sls) { return (sls == SubstLocState.Occupied); }

        /// <summary>Returns true if the given <paramref name="sls"/> value is Unoccupied</summary>
        public static bool IsUnoccupied(this SubstLocState sls) { return (sls == SubstLocState.Unoccupied); }

        /// <summary>Returns true if the given <paramref name="sts"/> value is AtSource</summary>
        public static bool IsAtSource(this SubstState sts) { return (sts == SubstState.AtSource); }

        /// <summary>Returns true if the given <paramref name="sts"/> value is AtWork</summary>
        public static bool IsAtWork(this SubstState sts) { return (sts == SubstState.AtWork); }

        /// <summary>Returns true if the given <paramref name="sts"/> value is AtDestination</summary>
        public static bool IsAtDestination(this SubstState sts) { return (sts == SubstState.AtDestination); }

        /// <summary>Returns true if the given <paramref name="sps"/> value is NeedsProcessing</summary>
        public static bool IsNeedsProcessing(this SubstProcState sps) { return (sps == SubstProcState.NeedsProcessing); }

        /// <summary>Returns true if the given <paramref name="sps"/> value is InProcess</summary>
        public static bool IsInProcess(this SubstProcState sps) { return (sps == SubstProcState.InProcess); }

        /// <summary>Returns true if the given <paramref name="sps"/> value is ProcessStepCompleted</summary>
        public static bool IsProcessStepComplete(this SubstProcState sps) { return (sps == SubstProcState.ProcessStepCompleted); }

        /// <summary>
        /// Returns true if the given <paramref name="sps"/> value is any of the known processing complete values, or false otherwise.
        /// <para/>Processed, Aborted, Stopped, or Rejected.
        /// <para/>or Lost if the <paramref name="includeLost"/> parameter is true
        /// <para/>or Skipped if the <paramref name="includeSkipped"/> parameter is true
        /// </summary>
        public static bool IsProcessingComplete(this SubstProcState sps, bool includeLost = true, bool includeSkipped = true)
        {
            switch (sps)
            {
                case SubstProcState.Processed:
                case SubstProcState.Aborted:
                case SubstProcState.Stopped:
                case SubstProcState.Rejected:
                    return true;
                case SubstProcState.Lost:
                    return includeLost;
                case SubstProcState.Skipped:
                    return includeSkipped;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if the give <paramref name="sps"/> value is one of the pseudo SPS values, Added, Moved, or Removed, if <paramref name="includeCreatedMovedAndRemoved"/> is true, or ProcessStepCompleted if <paramref name="includeProcessStepCompleted"/> is true.
        /// </summary>
        public static bool IsPseudoState(this SubstProcState sps, bool includeCreatedMovedAndRemoved = true, bool includeProcessStepCompleted = true)
        {
            switch (sps)
            {
                case SubstProcState.Created: return includeCreatedMovedAndRemoved;
                case SubstProcState.Moved: return includeCreatedMovedAndRemoved;
                case SubstProcState.Removed: return includeCreatedMovedAndRemoved;
                case SubstProcState.ProcessStepCompleted: return includeProcessStepCompleted;
                default: return false;
            }
        }

        /// <summary>
        /// Returns true if the value of the given <paramref name="sls"/> value is any of the known values, or false otherwise
        /// <para/>Occupied, or Unoccupied
        /// </summary>
        public static bool IsValid(this SubstLocState sls)
        {
            switch (sls)
            {
                case SubstLocState.Occupied:
                case SubstLocState.Unoccupied:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if the value of the given <paramref name="sts"/> value is any of the known values, or false otherwise
        /// <para/>AtSource, AtWork, or AtDestination.
        /// </summary>
        public static bool IsValid(this SubstState sts)
        {
            switch (sts)
            {
                case SubstState.AtSource:
                case SubstState.AtWork:
                case SubstState.AtDestination:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if the value of the given <paramref name="sps"/> value is any of the known values, or false otherwise
        /// <para/>NeedsProcessing, InProcess, Processed, Aborted, Stopped, Rejected, Lost, or Skipped.
        /// <para/>If <paramref name="acceptProcessStepCompleted"/> then ProcessStepCompleted is also accepted (defaults to false).
        /// <para/>If <paramref name="acceptUndefined"/> then Undefined is also accepted (defaults to false).
        /// <para/>Note: this EM does not generally accept PseudoSPS values.
        /// </summary>
        public static bool IsValid(this SubstProcState sps, bool acceptProcessStepCompleted = false, bool acceptUndefined = false)
        {
            switch (sps)
            {
                case SubstProcState.NeedsProcessing:
                case SubstProcState.InProcess:
                case SubstProcState.Processed:
                case SubstProcState.Aborted:
                case SubstProcState.Stopped:
                case SubstProcState.Rejected:
                case SubstProcState.Lost:
                case SubstProcState.Skipped:
                    return true;

                case SubstProcState.ProcessStepCompleted: 
                    return acceptProcessStepCompleted;

                case SubstProcState.Undefined:
                    return acceptUndefined;

                case SubstProcState.Created:
                case SubstProcState.Moved:
                case SubstProcState.Removed:
                    //These PseudoSPS values are only ever used when building a Substrate's SPSList.  They should not ever appear in the Substrate's SPS or PendingSPS.
                    return false;

                default:
                    return false;
            }
        }
    }

    #endregion

    #region SubstrateJobRequestState and SubstrateJobState and related extension methods

    /// <summary>
    /// This is a source agnostic representation for what the job engine wants the substrate scheduler to do for a given wafer.
    /// Although the job engine generally updates this in a particular pattern, there is no explicit requirement for this.
    /// The job engine may update this at any time, even after the substrate has been given its final state.
    /// <para/>None (0), Run (1), Pause (2), Stop (3), Abort (4), Return (5)
    /// </summary>    
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstrateJobRequestState : int
    {
        /// <summary>This is the default value.  It indicates that the job engine is not currently making any request for this substrate.  By default any external entity should hold the substrate in its current state and may report Held for the SJS [0]</summary>
        [EnumMember]
        None = 0,

        /// <summary>Indicates that the job engine would like this substrate to be processed (and to resume processing) normally [1]</summary>
        [EnumMember]
        Run = 1,

        /// <summary>Indicates that the job engine would like processing for this substrate to be paused.  Detailed meaning of this are tool specific. [2]</summary>
        [EnumMember]
        Pause = 2,

        /// <summary>Indicates that the job engine would like processing for this substrate to be stopped.  Detailed meaning of this are tool specific. [3]</summary>
        [EnumMember]
        Stop = 3,

        /// <summary>Indicates that the job engine would like processing for this substrate to be aborted.  Detailed meaning of this are tool specific. [4]</summary>
        [EnumMember]
        Abort = 4,

        /// <summary>Indicates that the job engine would like this substrate to be returned to its source (or destination).  Detailed meaning of this are tool specific. [5]</summary>
        [EnumMember]
        Return = 5,
    }

    /// <summary>
    /// This is a target agnostic representation for what the substrate routing and processing engine indicates that it is doing and has done with a given substrate.
    /// Generally this state is intended to react to changes in the substrate's corresponding SubstrateJobRequestState value.  However once this state reaches a terminal
    /// value it will no longer change.
    /// <para/>Initial (0), WaitingForStart (1), Running (2), Processed (3), Rejected (4), Skipped (5), Pausing (6), Paused (7), Stopping (8), Stopped (9), Aborting (10), Aborted (11), Lost (12), Returning (13), Returned (14), Held (15), RoutingAlarm (16), Removed (17)
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstrateJobState : int
    {
        /// <summary>This is the default value.  It indicates that the substrate routing and processing engine has not yet started looking at this substrate [0]</summary>
        [EnumMember]
        Initial = 0,

        /// <summary>Indicates that the substrate routing and processing engine has not observed any request from the job engine yet. [1]</summary>
        [EnumMember]
        WaitingForStart = 1,

        /// <summary>Indicates that the substrate routing and processing engine has been asked to process (or resume processing) this substrate normally. [2]</summary>
        [EnumMember]
        Running = 2,

        /// <summary>Indicates that the substrate routing and processing engine has completed normal processing for this substrate.  This is a terminal state and requires that the substrate be Processed AtDestination [3]</summary>
        [EnumMember]
        Processed = 3,

        /// <summary>Indicates that the substrate routing and processing engine has completed abnormal processing for this substrate due to the subsrate being marked Rejected.  This is a terminal state and requires that the substrate be Rejected AtDestination [4]</summary>
        [EnumMember]
        Rejected = 4,

        /// <summary>Indicates that the substrate routing and processing engine has been asked to stop or abort normal processing for this substrate that has not started processing yet.  This state is a terminal state and requires that the substrate be Skipped AtSource or Skipped AtDestination [5]</summary>
        [EnumMember]
        Skipped = 5,

        /// <summary>Indicates that the substrate routing and processing engine has been asked to pause normal processing for this substrate but that the completion conditions for paused have not been reached yet. [6]</summary>
        [EnumMember]
        Pausing = 6,

        /// <summary>Indicates that the substrate routing and processing engine has completed the pause related activties for this substrate.  This is not a terminal state. [7]</summary>
        [EnumMember]
        Paused = 7,

        /// <summary>Indicates that the substrate routing and processing engine has been asked to stop normal processing for this substrate but that the completion conditions for stopped have not been reached yet. [8]</summary>
        [EnumMember]
        Stopping = 8,

        /// <summary>Indicates that the substrate routing and processing engine has completed the stop related activities for this substrate.  This is a terminal state.  It generally requires that the substrate be Stopped and that it may be AtDestination (or AtWork).  Such a substrate may not be AtSource. [9]</summary>
        [EnumMember]
        Stopped = 9,

        /// <summary>Indicates that the substrate routing and processing engine has been asked to abort normal processing for this substrate but that the completion conditions for aborted have not been reached yet [10]</summary>
        [EnumMember]
        Aborting = 10,

        /// <summary>Indicates that the substrate routing and processing engine has completed the abort related activities for this substrate.  This is a terminal state.  It generally requires that the substrate be Aborted and that it may be AtDestination (or AtWork).  Such a substrate may not be AtSource. [11]</summary>
        [EnumMember]
        Aborted = 11,

        /// <summary>Indicates that the substrate has been marked lost.  This is a terminal state and may be detected at any location (AtSource, AtWork, or AtDestination) [12]</summary>
        [EnumMember]
        Lost = 12,

        /// <summary>Indicates that the substrate routing and processing engine has been asked to return the substrate but that the completion conditions for returned have not been reached yet [13]</summary>
        [EnumMember]
        Returning = 13,

        /// <summary>Indicates that the substrate routing and processing engine has completed the return related activities for this substrate.  Generally a PendingSPS of Stopped will be applied to it if it has reached InProcess and the substrate will be returned to its Destination, or Source, location.  Final STS will be AtDestination, or AtSource, correspondingly. [14]</summary>
        [EnumMember]
        Returned = 14,

        /// <summary>Indicates that the substrate routing and processing engine is not being asked to make any changes to a substrate and that substrate is not in its initial source location. [15]</summary>
        [EnumMember]
        Held = 15,

        /// <summary>Indicates that the substrate routing engine has encountered an error with this substrate that requires manual intervention to confirm and/or guide what the next steps shall be. [16]</summary>
        [EnumMember]
        RoutingAlarm = 16,

        /// <summary>Indicates that the substrate has been removed from the system in an unexpected location and/or state. [17]</summary>
        [EnumMember]
        Removed = 17,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given SubstrateJobState <paramref name="sjs"/> is final (Processed, Rejected, Skipped, Stopped, Aborted, Lost, Returned (optional), Removed)
        /// </summary>
        public static bool IsFinal(this SubstrateJobState sjs, bool includeReturned = false)
        {
            switch (sjs)
            {
                case SubstrateJobState.Processed: return true;
                case SubstrateJobState.Rejected: return true;
                case SubstrateJobState.Skipped: return true;
                case SubstrateJobState.Stopped: return true;
                case SubstrateJobState.Aborted: return true;
                case SubstrateJobState.Lost: return true;
                case SubstrateJobState.Returned: return includeReturned;
                case SubstrateJobState.Removed: return true;
                default: return false;
            }
        }
    }
    
    #endregion
}
