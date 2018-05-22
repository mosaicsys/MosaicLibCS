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
    #region Constants (type names)

    public static class Constants
    {
        /// <summary>SubstrateObjectType = "Substrate"</summary>
        public const string SubstrateObjectType = "Substrate";

        /// <summary>SubstrateLocationObjectType = "SubstLoc"</summary>
        public const string SubstrateLocationObjectType = "SubstLoc";

        // public const string SubstrateBatchLocationObjectType = "BatchLoc";       // not supported yet

        /// <summary>Gives the link key used for links from a substrate location to the substrate it contains, if any (Contains)</summary>
        public const string ContainsLinkKey = "Contains";

        /// <summary>Gives the link key used for links from a substrate to its source substrate location (SrcLoc)</summary>
        public const string SourceLocationLinkKey = "SrcLoc";

        /// <summary>Gives the link key used for links from a substrate to its destination substrate location (DestLoc)</summary>
        public const string DestinationLocationLinkKey = "DestLoc";

        /// <summary>E039 attribute name used for SubstrateProcessingState: "SubstProcState"</summary>
        public const string SubstrateProcessingStateAttributeName = "SubstProcState";

        /// <summary>E039 attribute name used for SubstrateTransportState: "SubstState"</summary>
        public const string SubstrateTransportStateAttributeName = "SubstState";

        /// <summary>E039 attribute name used for LotID: "LotID"</summary>
        public const string LotIDAttributeName = "LotID";

        /// <summary>E039 attribute name used for Usage: "SubstUsage"</summary>
        public const string SubstrateUsageAttributeName = "SubstUsage";

        /// <summary>Static get/set property used to control the maximum length of any generated SPSList or SPSLocList.  Once either list reaches this length limit concatination will stop.  Default: 50.  Setter clips given value to be between 0 and 1000.</summary>
        public static int MaximumSPSListLength { get { return _maximumSPSListLength; } set { _maximumSPSListLength = value.Clip(0, 1000); } }
        private static int _maximumSPSListLength = DefaultMaximumSPSListLength;

        /// <summary>Defines the default value for the MaximumSPSListLength (50)</summary>
        public const int DefaultMaximumSPSListLength = 50;

        /// <summary>E039 attribute name used for optional SubstrateJobRequestState: "SJRS"</summary>
        public const string SubstrateJobRequestStateAttributeName = "SJRS";

        /// <summary>E039 attribute name used for optional SubstrateJobState: "SJS"</summary>
        public const string SubstrateJobStateAttributeName = "SJS";
    }

    #endregion

    #region E090StateUpdateBehavior flags

    /// <summary>
    /// Flags that are used to control the behavior of the various E090 Update related ExtensionMethods provided here.
    /// <para/>None (0x00), AutoInProcess (0x01), AllowReturnToNeedsProcessing (0x02), AutoUpdateSTS (0x04), All (0x07)
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

        /// <summary>Include this flag to support adding automatic transitions for the STS based on the location and the SPS</summary>
        AutoUpdateSTS = 0x04,

        /// <summary>The use of this flag causes the E090 SPS related logic to primarily update the PendingSPS rather than the main SPS property.  This allows the E090 SPS system to be given a sequence of values and delays performing the final SPS complete transition until the user explicitly requests it or until the substrate reaches its destination (at which point it is safe to lock down the final SPS value).</summary>
        UsePendingSPS = 0x08,

        /// <summary>The use of this flag causes each E090 SPS state update operation to append the given non-Undefined SPS value to the SPSList attribute.</summary>
        UseSPSList = 0x10,

        /// <summary>
        /// The use of this flag causes each E090 SPS state update operation to append the current substrate location to the SPSLocList attribute whenever a new SPS value is appendended to the SPSList.
        /// <para/>Note: if this flag is not used uniformly with the UseSPSList flag, then the SPSLocList attribute may end up being shorter than the SPSList attribute itself.
        /// </summary>
        UseSPSLocList = 0x20,

        /// <summary>The use of this flag requests that any corresponding E090 create, of update method should also include an external sync operation.</summary>
        AddExternalSyncItem = 0x40,

        /// <summary>Synonym for AllV1: (AllowReturnToNeedsProcessing | AutoUpdateSTS) [0x06]</summary>
        All = AllV1,

        /// <summary>(AllowReturnToNeedsProcessing | AutoUpdateSTS) [0x06]</summary>
        AllV1 = (AllowReturnToNeedsProcessing | AutoUpdateSTS),

        /// <summary>(AllowReturnToNeedsProcessing | AutoUpdateSTS | UsePendingSPS | UseSPSList | UseSPSLocList) [0x7e]</summary>
        AllV2 = (AllowReturnToNeedsProcessing | AutoUpdateSTS | UsePendingSPS | UseSPSList | UseSPSLocList),

        /// <summary>(UseSPSList | UseSPSLocList) [0x60]</summary>
        SPSLists = (UseSPSList | UseSPSLocList),
    }

    #endregion

    #region E090 ExtensionMethods: CreateE090SubstLoc, CreateE090Subst, NoteSubstMoved, SetSubstProcState, GenerateCreateE090SubstLocItems, GenerateCreateE090SubstItems, GenerateE090UpdateItems, IsSubstrate

    /// <summary>
    /// The E090 interface presented here are implemented as a set of extension methods that are used to create and managed E090 Substrate and SubstLoc types of E039 objects.
    /// </summary>
    public static partial class ExtensionMethods
    {
        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, Action<E039UpdateItem.AddObject> addedObjectDelegate, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.Pinned, bool addSyncExternalItem = false, int instanceNum = 0, bool addIfNeeded = true)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090SubstLoc(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem, instanceNum: instanceNum, addIfNeeded: addIfNeeded);

            if (addedObjectDelegate != null)
                addedObjectDelegate(addObjectUpdateItem);

            return ec;
        }

        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, Action<E039ObjectID> addedObjectIDDelegate, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.Pinned, bool addSyncExternalItem = false, int instanceNum = 0, bool addIfNeeded = true)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090SubstLoc(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem, instanceNum: instanceNum, addIfNeeded: addIfNeeded);

            if (addedObjectIDDelegate != null && addObjectUpdateItem.AddedObjectPublisher != null)
                addedObjectIDDelegate((addObjectUpdateItem.AddedObjectPublisher.Object ?? E039Object.Empty).ID);

            return ec;
        }

        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.Pinned, bool addSyncExternalItem = false, int instanceNum = 0, bool addIfNeeded = true)
        {
            E039UpdateItem[] updateItemArray = new List<E039UpdateItem>().GenerateCreateE090SubstLocItems(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem, instanceNum: instanceNum, addIfNeeded: addIfNeeded).ToArray();

            return tableUpdater.Update(updateItemArray).Run();
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

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, IE039Object substObj, IE039Object toLocObj, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV1, bool addSyncExternalItem = false)
        {
            E039ObjectID toLocObjID = (toLocObj != null) ? toLocObj.ID : E039ObjectID.Empty;

            return tableUpdater.NoteSubstMoved(substObj, toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
        }

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, IE039Object substObj, E039ObjectID toLocObjID, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV1, bool addSyncExternalItem = false)
        {
            E090SubstInfo currentSubstInfo = new E090SubstInfo(substObj);

            return tableUpdater.NoteSubstMoved(currentSubstInfo, toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
        }

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, E039ObjectID toLocObjID, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV1, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.NoteSubstMoved(substObs.UpdateInline().Info, toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
            substObs.Update();
            return ec;
        }

        public static string NoteSubstMoved(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, E039ObjectID toLocObjID, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV1, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = currentSubstInfo.ObjID;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' '{2}'".CheckedFormat(Fcns.CurrentMethodName, substObjID, toLocObjID)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

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

        public static string SetSubstProcState(this IE039TableUpdater tableUpdater, IE039Object substObj, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV1, bool addSyncExternalItem = false)
        {
            E090SubstInfo currentSubstInfo = new E090SubstInfo(substObj);

            return tableUpdater.SetSubstProcState(currentSubstInfo, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);
        }

        public static string SetSubstProcState(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV1, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.SetSubstProcState(substObs.UpdateInline().Info, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

            substObs.Update();

            return ec;
        }

        public static string SetSubstProcState(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV1, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = currentSubstInfo.ObjID;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' {2}".CheckedFormat(Fcns.CurrentMethodName, substObjID, spsParam)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

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

        public static string SetPendingSubstProcState(this IE039TableUpdater tableUpdater, IE039Object substObj, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV2, bool addSyncExternalItem = false)
        {
            return tableUpdater.SetSubstProcState(substObj, spsParam: spsParam, updateBehavior: updateBehavior | E090StateUpdateBehavior.UsePendingSPS, addSyncExternalItem: addSyncExternalItem);
        }

        public static string SetPendingSubstProcState(this IE039TableUpdater tableUpdater, E090SubstObserver substObs, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV2, bool addSyncExternalItem = false)
        {
            string ec = tableUpdater.SetPendingSubstProcState(substObs.UpdateInline().Info, spsParam: spsParam, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

            substObs.Update();

            return ec;
        }

        public static string SetPendingSubstProcState(this IE039TableUpdater tableUpdater, E090SubstInfo currentSubstInfo, SubstProcState spsParam, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.AllV2, bool addSyncExternalItem = false)
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
            string ec = tableUpdater.SetSubstrateJobStates(substObs.UpdateInline().Info, sjrs: sjrs, sjs: sjs, addSyncExternalItem: addSyncExternalItem);

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

        public static List<E039UpdateItem> GenerateCreateE090SubstLocItems(this List<E039UpdateItem> updateItemList, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addIfNeeded = false, bool addSyncExternalItem = false, int instanceNum = 0)
        {
            E039ObjectID substLocObjID = new E039ObjectID(substLocName, Constants.SubstrateLocationObjectType, assignUUID: true);

            if (instanceNum != 0)
                attributes = attributes.ConvertToWriteable().SetValue("InstanceNum", instanceNum);

            updateItemList.Add(addObjectUpdateItem = new E039UpdateItem.AddObject(substLocObjID, attributes: attributes, flags: flags, ifNeeded: addIfNeeded));
            updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substLocObjID, E039ObjectID.Empty, Constants.ContainsLinkKey), ifNeeded: addIfNeeded));
            if (addSyncExternalItem)
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return updateItemList;
        }

        public static List<E039UpdateItem> GenerateCreateE090SubstItems(this List<E039UpdateItem> updateItemList, string substName, out E039UpdateItem.AddObject addObjectUpdateItem, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = new E039ObjectID(substName, Constants.SubstrateObjectType, assignUUID: true);

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
                updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(createAtLocID, substObjID, Constants.ContainsLinkKey)));

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
            if (currentSubstInfo.Obj == null || updateItemList == null)
                return "{0}: given invalid or null parameter".CheckedFormat(Fcns.CurrentMethodName);

            if (!currentSubstInfo.Obj.IsSubstrate())
                return "{0}: given non-Substrate obj [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo.Obj);

            toLocObjID = toLocObjID ?? E039ObjectID.Empty;

            if (!currentSubstInfo.IsValid)
                return "{0}: given unusable obj.  derived Substrate Info is not valid: [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo);

            // first generate any required location move update item and update the currentLocID accordingly
            string currentLocID = currentSubstInfo.LocID;

            if (!toLocObjID.IsEmpty)
            {
                E039ObjectID fromLocObjID = currentSubstInfo.LocsLinkedToHere.FirstOrDefault().FromID;

                if (fromLocObjID.IsEmpty)
                    logger.Debug.Emit("{0}: Issue with attempt to move [{1}]: current location [{2}] is unknown", Fcns.CurrentMethodName, currentSubstInfo.ObjID, currentLocID);

                if (toLocObjID.Name != currentLocID)
                {
                    updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(toLocObjID, currentSubstInfo.ObjID, Constants.ContainsLinkKey), autoUnlinkFromPriorByTypeStr: true));
                    currentLocID = toLocObjID.Name;
                }
            }

            // the following logic will create the attribute update NVS if needed.  If it ends up being non-empty then an UpdateItem will be added for it.
            NamedValueSet attribUpdateNVS = null;
            NamedValueMergeBehavior attribUpdateNVSMergeBehavior = NamedValueMergeBehavior.AddAndUpdate;

            // next apply any change in the SPS and/or PendingSPS

            bool clearPendingSPS = false;

            if (spsParam != SubstProcState.Undefined)
            {
                attribUpdateNVS = attribUpdateNVS ?? new NamedValueSet();

                bool usePendingSPSBehavior = updateBehavior.IsSet(E090StateUpdateBehavior.UsePendingSPS);

                var setSPS = SubstProcState.Undefined;
                var setPendingSPS = SubstProcState.Undefined;

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

                    string ec = GetSPSTransitionDenyReason(currentSubstInfo.InferredSPS, mergedSPSParam, allowReturnToNeedsProcessing: updateBehavior.IsSet(E090StateUpdateBehavior.AllowReturnToNeedsProcessing));

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

                if (updateBehavior.IsSet(E090StateUpdateBehavior.UseSPSList) && currentSubstInfo.SPSList.Length < Constants.MaximumSPSListLength && currentSubstInfo.SPSLocList.Count < Constants.MaximumSPSListLength)
                {
                    attribUpdateNVSMergeBehavior |= NamedValueMergeBehavior.AppendLists;        // turn on AppendLists mode to the current NVS merge behavior

                    attribUpdateNVS.SetValue("SPSList", new[] { spsParam.ToString() });

                    if (updateBehavior.IsSet(E090StateUpdateBehavior.UseSPSLocList))
                        attribUpdateNVS.SetValue("SPSLocList", new[] { currentLocID });
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
            if (currentSubstInfo.Obj == null || updateItemList == null)
                return "{0}: given invalid or null parameter".CheckedFormat(Fcns.CurrentMethodName);

            if (!currentSubstInfo.Obj.IsSubstrate())
                return "{0}: given non-Substrate obj [{1}]".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo.Obj);

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

        private static Logging.ILogger logger = new Logging.Logger("E090.ExtensionMethods");

        /// <summary>
        /// Accepts a current <paramref name="startingSPS"/> and a <paramref name="mergeWithSPS"/>.  Returns the value between the two that has the higher priority.
        /// <para/>Priority (from least to greatest): NeedsProcessing, InProcess, Processed, Stopped, Rejected, Skipped, Aborted, Lost
        /// </summary>
        public static SubstProcState MergeWith(this SubstProcState startingSPS, SubstProcState mergeWithSPS)
        {
            switch (startingSPS)
            {
                case SubstProcState.NeedsProcessing: if (mergeWithSPS.IsInProcess() || mergeWithSPS.IsProcessingComplete()) return mergeWithSPS; break;
                case SubstProcState.InProcess: if (mergeWithSPS.IsProcessingComplete()) return mergeWithSPS; break;
                case SubstProcState.Processed: if (mergeWithSPS.IsProcessingComplete()) return mergeWithSPS; break;
                case SubstProcState.Stopped: if (mergeWithSPS == SubstProcState.Rejected || mergeWithSPS == SubstProcState.Skipped || mergeWithSPS == SubstProcState.Aborted || mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Rejected: if (mergeWithSPS == SubstProcState.Skipped || mergeWithSPS == SubstProcState.Aborted || mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Skipped: if (mergeWithSPS == SubstProcState.Aborted || mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Aborted: if (mergeWithSPS == SubstProcState.Lost) return mergeWithSPS; break;
                case SubstProcState.Lost: break;
                case SubstProcState.Undefined:
                default:
                    return mergeWithSPS;
            }

            return startingSPS;
        }

        public static string GetSPSTransitionDenyReason(SubstProcState currentSPS, SubstProcState toSPS, bool requireInProcessBeforeProcessComplete = false, bool allowReturnToNeedsProcessing = false)
        {
            string reason = null;

            if (toSPS != currentSPS && currentSPS != SubstProcState.Undefined)
            {
                switch (toSPS)
                {
                    case SubstProcState.NeedsProcessing:
                        if (!currentSPS.IsInProcess())
                            reason = "Transition is not allowed";
                        else if (!allowReturnToNeedsProcessing)
                            reason = "Transition is not enabled";
                        break;

                    case SubstProcState.InProcess:
                        if (!currentSPS.IsNeedsProcessing())
                            reason = "Transition is not allowed";
                        break;

                    case SubstProcState.Lost:
                        break;

                    case SubstProcState.Skipped:
                        if (!currentSPS.IsNeedsProcessing() && !currentSPS.IsInProcess())
                            reason = "Transition is not allowed";
                        break;

                    case SubstProcState.Processed:
                    case SubstProcState.Aborted:
                    case SubstProcState.Stopped:
                    case SubstProcState.Rejected:
                        if (!(currentSPS.IsInProcess() || (!requireInProcessBeforeProcessComplete && currentSPS.IsNeedsProcessing())))
                            reason = "Transition is not allowed";
                        break;

                    default:
                        return "Current SubstProcState is not valid";
                }
            }

            return (reason.IsNullOrEmpty() ? "" : "{0} [from:{1}, to:{2}]".CheckedFormat(reason, currentSPS, toSPS));
        }
    }

    #endregion

    #region E090SubstLocObserver and E090SubstObserver objects

    /// <summary>
    /// Observer helper object for use with Substrate Location object publishers.  
    /// </summary>
    public class E090SubstLocObserver : E039.E039ObjectObserverWithInfoExtraction<E090SubstLocInfo>
    {
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
            : base(other)
        {
            AlsoObserveContents = other.AlsoObserveContents;

            if (AlsoObserveContents)
                base.Add((obj) => UpdateContainsObject(obj));
        }

        /// <summary>When true this option selects that the ContainsObject and ContainsSubstInfo properties shall be set based on the object identified by the location's "Contains" link's ToID</summary>
        public bool AlsoObserveContents { get; private set; }

        private void UpdateContainsObject(IE039Object obj)
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
            : base(other)
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
        /// <summary>Normal constructor.  Caller must provide a non-empty Substrate Location type <paramref name="obj"/> [SubstLoc]</summary>
        public E090SubstLocInfo(IE039Object obj)
            : this()
        {
            Obj = obj;
            ObjID = (Obj != null) ? Obj.ID : E039ObjectID.Empty;

            INamedValueSet attributes = (Obj != null) ? Obj.Attributes : NamedValueSet.Empty;
            InstanceNum = attributes["InstanceNum"].VC.GetValue<int>(rethrow: false);

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

            LinkToSubst = other.LinkToSubst;
            SrcLinksToHere = other.SrcLinksToHere.MakeCopyOf(mapNullToEmpty: false);
            DestLinksToHere = other.DestLinksToHere.MakeCopyOf(mapNullToEmpty: false);
        }

        /// <summary>Used to update the given <paramref name="nvs"/> to contain Attribute values for the properties in this object that represent Attribute values.</summary>
        public NamedValueSet UpdateAttributeValues(NamedValueSet nvs)
        {
            nvs.ConditionalSetValue("InstanceNum", InstanceNum != 0, InstanceNum);

            return nvs;
        }

        /// <summary>Gives the original object that this info object was constructed from, or null for the default constructor.</summary>
        public IE039Object Obj { get; private set; }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

        /// <summary>When non-zero, this property gives the instance number for this location.  This is typically used when the location is part of a set of locations that can be indexed.  In this case this property may be used to determine the location's index in the set.</summary>
        public int InstanceNum { get; set; }

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
            if (IsEmpty)
                return "E090SubstLocInfo Empty";

            StringBuilder sb = new StringBuilder("E090SubstLocInfo {0} {1}".CheckedFormat(ObjID.ToString(E039ToStringSelect.FullName), SLS));

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
    /// information from optional "PendingSPS", "SPSList", and "SPSLocList" attrubutes, 
    /// and information that is derived from the "Contains", "SrcLoc" and "DestLoc" links to/from this object.
    /// Now also includes use of optional SubstrateJobRequestState (SJRS) and SubsrateJobState (SJS) attibutes.
    /// </summary>
    public struct E090SubstInfo : IEquatable<E090SubstInfo>
    {
        /// <summary>Normal constructor.  Caller must provide a non-empty Substrate type <paramref name="obj"/> [Substrate]</summary>
        public E090SubstInfo(IE039Object obj) 
            : this()
        {
            Obj = obj;
            _objID = (Obj != null) ? Obj.ID : null;

            INamedValueSet attributes = (Obj != null) ? Obj.Attributes : NamedValueSet.Empty;
            
            SPS = attributes["SubstProcState"].VC.GetValue(rethrow: false, defaultValue: SubstProcState.Undefined);
            STS = attributes["SubstState"].VC.GetValue(rethrow: false, defaultValue: SubstState.Undefined);
            PendingSPS = attributes["PendingSPS"].VC.GetValue<SubstProcState>(rethrow: false, defaultValue: SubstProcState.Undefined);
            SPSList = (attributes["SPSList"].VC.GetValue<ReadOnlyIList<string>>(rethrow: false) ?? ReadOnlyIList<string>.Empty).Select(str => str.TryParse<SubstProcState>()).ToArray();
            SPSLocList = (attributes["SPSLocList"].VC.GetValue<IList<string>>(rethrow: false) ?? ReadOnlyIList<string>.Empty);
            LotID = attributes["LotID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
            SubstUsageStr = attributes["SubstUsage"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();

            LocsLinkedToHere = Obj.FindLinksFrom(Constants.ContainsLinkKey).ToArray();
            LinkToSrc = Obj.FindFirstLinkTo(Constants.SourceLocationLinkKey);
            LinkToDest = Obj.FindFirstLinkTo(Constants.DestinationLocationLinkKey);

            SJRS = attributes[Constants.SubstrateJobRequestStateAttributeName].VC.GetValue<SubstrateJobRequestState>(rethrow: false);
            SJS = attributes[Constants.SubstrateJobStateAttributeName].VC.GetValue<SubstrateJobState>(rethrow: false);
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
            SPSList = other.SPSList.MakeCopyOf();
            SPSLocList = other.SPSLocList.ConvertToReadOnly();
            LotID = other.LotID;
            SubstUsageStr = other.SubstUsageStr;

            LocsLinkedToHere = other.LocsLinkedToHere.MakeCopyOf(mapNullToEmpty: false);
            LinkToSrc = other.LinkToSrc;
            LinkToDest = other.LinkToDest;

            SJRS = other.SJRS;
            SJS = other.SJS;
        }

        private static readonly E039Link[] emptyLinkArray = EmptyArrayFactory<E039Link>.Instance;

        /// <summary>Used to update the given <paramref name="nvs"/> to contain Attribute values for the properties in this object that represent Attribute values.</summary>
        public NamedValueSet UpdateAttributeValues(NamedValueSet nvs)
        {
            nvs.SetValue("SubstProcState", SPS);
            nvs.SetValue("SubstState", STS);

            nvs.ConditionalSetValue("PendingSPS", PendingSPS != SubstProcState.Undefined, PendingSPS);
            nvs.ConditionalSetValue("SPSList", !SPSList.IsNullOrEmpty(), (SPSList ?? EmptyArrayFactory<SubstProcState>.Instance).Select(sps => sps.ToString()));
            nvs.ConditionalSetValue("SPSLocList", !SPSLocList.IsNullOrEmpty(), SPSLocList);
            nvs.ConditionalSetValue("LotID", !LotID.IsNullOrEmpty() || nvs.Contains("LotID"), LotID);
            nvs.ConditionalSetValue("SubstUsage", !SubstUsageStr.IsNullOrEmpty() || nvs.Contains("SubstUsage"), SubstUsageStr);
            nvs.ConditionalSetValue(Constants.SubstrateJobRequestStateAttributeName, SJRS != SubstrateJobRequestState.None, SJRS);
            nvs.ConditionalSetValue(Constants.SubstrateJobStateAttributeName, SJS != SubstrateJobState.Initial, SJS);

            return nvs;
        }

        /// <summary>Gives the original object that this info object was constructed from, or null for the default constructor.</summary>
        public IE039Object Obj { get; private set; }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

        /// <summary>From SubstState attribute</summary>
        public SubstState STS { get; set; }

        /// <summary>From SubstProcState attribute</summary>
        public SubstProcState SPS { get; set; }

        /// <summary>Optional, From PendingSPS attribute.  Undefined if the substrate does not have this attributes.</summary>
        public SubstProcState PendingSPS { get; set; }

        /// <summary>This property returns the SPS that is inferred by merging the SPS with the PendingSPS (if any)</summary>
        public SubstProcState InferredSPS { get { return SPS.MergeWith(PendingSPS); } }

        /// <summary>Optional, an accumlation of SPS attributes that have been assigned to this substrate.  Empty if the substrate does not have this attributes.</summary>
        public SubstProcState [] SPSList { get; set; }

        /// <summary>Optional, an accumlation of the locations at which SPS values have been assigned to this substrate.  Empty if the substrate does not have this attributes.</summary>
        public IList<string> SPSLocList { get; set; }

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

        /// <summary>Gives an empty E090SubstInfo object to be used as a default value.</summary>
        public static E090SubstInfo Empty { get { return new E090SubstInfo() { LotID = "", SubstUsageStr = "", STS = SubstState.Undefined, SPS = SubstProcState.Undefined, PendingSPS = SubstProcState.Undefined, LocsLinkedToHere = emptyLinkArray, SPSList = EmptyArrayFactory<SubstProcState>.Instance, SPSLocList = ReadOnlyIList<string>.Empty }; } }

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
                    && SPSList.IsEqualTo(other.SPSList)
                    && SPSLocList.IsEqualTo(other.SPSLocList)
                    && LotID == other.LotID
                    && SubstUsageStr == other.SubstUsageStr
                    && _locID == other._locID
                    && _locsLinkedToHere.IsEqualTo(other._locsLinkedToHere)
                    && LinkToSrc.Equals(other.LinkToSrc)
                    && LinkToDest.Equals(other.LinkToDest)
                    && SJRS == other.SJRS
                    && SJS == other.SJS
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
        /// otherwise AtWork
        /// </summary>
        public SubstState GetInferredSTS(string locID, SubstProcState sps)
        {
            if (sps.IsNeedsProcessing() && locID == LinkToSrc.ToID.Name)
                return SubstState.AtSource;
            else if (sps.IsProcessingComplete() && locID == LinkToDest.ToID.Name)
                return SubstState.AtDestination;            
            else if (sps == SubstProcState.Skipped && locID == LinkToSrc.ToID.Name)
                return SubstState.AtSource;
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
            if (IsEmpty)
                return "E90SubstInfo Empty";

            StringBuilder sb = new StringBuilder("E90SubstInfo {0} {1} {2}".CheckedFormat(ObjID.ToString(E039ToStringSelect.FullName), STS, SPS));

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
                sb.CheckedAppendFormat(" JSRS:{0}", SJRS);

            if (SJS != SubstrateJobState.Initial)
                sb.CheckedAppendFormat(" JSS:{0}", SJS);

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
        /// <summary>0: Substrate is located at its "source" location, where it was originally received.  If the source and destination are the same location then we destinguish source from destination based on whether it has been processed.</summary>
        [EnumMember]
        AtSource = 0,

        /// <summary>1: Substrate is out in the tool (essentially it is neither AtSource nor AtDestination).</summary>
        [EnumMember]
        AtWork = 1,

        /// <summary>2: Substrate is located at its "destination" location.  If the source and destination are the same location then we destinguish source from destination based on whether it has been processed.</summary>
        [EnumMember]
        AtDestination = 2,

        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        [EnumMember]
        Undefined = -1,
    }

    /// <summary>
    /// SubstProcState: Substrate Processing State (SPS)
    /// Gives a summary of if the substrate has been processed and, if it was not processed successfully, why.
    /// <para/>NeedsProcessing = 0, InProcess = 1, Processed = 2, Aborted = 3, Stopped = 4, Rejected = 5, Lost = 6, Skipped = 7, Undefined = -1
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstProcState : int
    {
        /// <summary>0: Substrate has not been fully processed yet.  This is the default state for newly created substrates.</summary>
        [EnumMember]
        NeedsProcessing = 0,

        /// <summary>1: Substrate is being processed.  Substrate properties are being changed or measured by the equipment.</summary>
        [EnumMember]
        InProcess = 1,

        /// <summary>2: Substrate has been processed successfully.  No more processing will take place while the Substrate remains in this equipment.</summary>
        [EnumMember]
        Processed = 2,

        /// <summary>3: Processing of this substrate was aborted during processing.  Additional information may be required to determine this substrate's final state.</summary>
        [EnumMember]
        Aborted = 3,

        /// <summary>4: Processing of this Substrate was stopped during processing.  Additional information may be required to determine this substrate's final state.</summary>
        [EnumMember]
        Stopped = 4,

        /// <summary>5: The Substrate has completed all processing steps but one or more of them may have had an issue during processing.  Additional information may be required to determine this substrate's final state.</summary>
        [EnumMember]
        Rejected = 5,

        /// <summary>6: The Substrate has been lost by the equipment, or was manually removed by an external entity/decision authority, and its loss/removal has been reported to the equipment.</summary>
        [EnumMember]
        Lost = 6,

        /// <summary>7: As directed by a suitable decision authority, the equipment has been told to skip processing for this substrate.</summary>
        [EnumMember]
        Skipped = 7,

        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        [EnumMember]
        Undefined = -1,
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
        /// <summary>-1: Local default value to use when there is no valid value.</summary>

        [EnumMember]
        Undefined = -1,
    }

    /// <summary>
    /// Substrate Usage enumeration:
    /// <para>Product (0), Test (1), Filler (2), Undefined (-1)</para>
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

        /// <summary>-1: Local default value to use when there is no valid value.</summary>
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
        /// </summary>
        public static bool IsValid(this SubstProcState sps)
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
        /// <summary>This is the default value.  It indicates that the job engine is not currently making any request for this substrate [0]</summary>
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
    /// <para/>Initial (0), WaitingForStart (1), Running (2), Processed (3), Rejected (4), Skipped (5), Pausing (6), Paused (7), Stopping (8), Stopped (9), Aborting (10), Aborted (11), Lost (12), Returned (13)
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

        /// <summary>Indicates that the substrate routing and processing engine has been asked to pause normal processing for this substrate but that the completion conditions for pausing have not been reached yet. [6]</summary>
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

        /// <summary>Indicates that the substrate routing and processing engine has been asked to abort normal processing for this substrate but that the completion conditions for aborting have not been reached yet [10]</summary>
        [EnumMember]
        Aborting = 10,

        /// <summary>Indicates that the substrate routing and processing engine has completed the abort related activities for this substrate.  This is a terminal state.  It generally requires that the substrate be Aborted and that it may be AtDestination (or AtWork).  Such a substrate may not be AtSource. [11]</summary>
        [EnumMember]
        Aborted = 11,

        /// <summary>Indicates that the substrate has been marked lost.  This is a terminal state and may be detected at any location (AtSource, AtWork, or AtDestination) [12]</summary>
        [EnumMember]
        Lost = 12,

        /// <summary>Indicates that the substrate routing and processing engine has completed the return related activities for this substrate.  Generally a PendingSPS of Stopped will be applied to it if it has reached InProcess and the substrate will be returned to its Destination, or Source, location.  Final STS will be AtDestination, or AtSource, correspondingly. [13]</summary>
        [EnumMember]
        Returned = 13,
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given SubstrateJobState <paramref name="sjs"/> is final (Processed, Rejected, Skipped, Stopped, or Aborted)
        /// </summary>
        public static bool IsFinal(this SubstrateJobState sjs)
        {
            switch (sjs)
            {
                case SubstrateJobState.Processed:
                case SubstrateJobState.Rejected:
                case SubstrateJobState.Skipped:
                case SubstrateJobState.Stopped:
                case SubstrateJobState.Aborted:
                case SubstrateJobState.Lost:
                case SubstrateJobState.Returned:
                    return true;
                default:
                    return false;
            }
        }
    }
    
    #endregion
}
