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

        public const string SubstrateProcessingStateAttributeName = "SubstProcState";
        public const string SubstrateTransportStateAttributeName = "SubstState";
        public const string LotIDAttributeName = "LotID";
        public const string SubstrateUsageAttributeName = "SubstUsage";
    }

    #endregion

    #region E090StateUpdateBehavior flags

    [Flags]
    public enum E090StateUpdateBehavior : int
    {
        None = 0x00,
        AutoInProcess = 0x01,
        AllowReturnToNeedsProcessing = 0x02,
        AutoUpdateSTS = 0x04,
        All = (AutoInProcess | AllowReturnToNeedsProcessing | AutoUpdateSTS),
    }

    #endregion

    #region E090 ExtensionMethods: CreateE090SubstLoc, CreateE090Subst, NoteSubstMoved, SetSubstProcState, GenerateCreateE090SubstLocItems, GenerateCreateE090SubstItems, GenerateE090UpdateItems, IsSubstrate

    /// <summary>
    /// The E090 interface presented here are implemented as a set of extension methods that are used to create and managed E090 Substrate and SubstLoc types of E039 objects.
    /// </summary>
    public static partial class ExtensionMethods
    {
        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, Action<E039UpdateItem.AddObject> addedObjectDelegate, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090SubstLoc(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem);

            if (addedObjectDelegate != null)
                addedObjectDelegate(addObjectUpdateItem);

            return ec;
        }

        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem[] updateItemArray = new List<E039UpdateItem>().GenerateCreateE090SubstLocItems(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem).ToArray();

            return tableUpdater.Update(updateItemArray).Run();
        }

        public static string CreateE090Subst(this IE039TableUpdater tableUpdater, string substName, Action<E039UpdateItem.AddObject> addedObjectDelegate, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090Subst(substName, out addObjectUpdateItem, srcSubstLocObjID, destSubstLocObjID: destSubstLocObjID, initialE090SubstrateObjState: initialE090SubstrateObjState, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem);

            if (addedObjectDelegate != null)
                addedObjectDelegate(addObjectUpdateItem);

            return ec;
        }

        public static string CreateE090Subst(this IE039TableUpdater tableUpdater, string substName, out E039UpdateItem.AddObject addObjectUpdateItem, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039UpdateItem[] updateItemArray = new List<E039UpdateItem>().GenerateCreateE090SubstItems(substName, out addObjectUpdateItem, srcSubstLocObjID, destSubstLocObjID: destSubstLocObjID, initialE090SubstrateObjState: initialE090SubstrateObjState, attributes: attributes, flags: flags, addSyncExternalItem: addSyncExternalItem).ToArray();

            return tableUpdater.Update(updateItemArray).Run();
        }

        public static string NoteSubstMoved(this IE039TableUpdater table, IE039Object substObj, IE039Object toLocObj, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.All, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = (substObj != null) ? substObj.ID : E039ObjectID.Empty;
            E039ObjectID toLocObjID = (toLocObj != null) ? toLocObj.ID : E039ObjectID.Empty;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' '{2}'".CheckedFormat(Fcns.CurrentMethodName, substObjID, toLocObjID)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                ec = updateItemList.GenerateE090UpdateItems(substObj, toLocObjID: toLocObjID, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

                if (ec.IsNullOrEmpty())
                {
                    if (updateItemList.Count > 0)
                        ec = table.Update(updateItemList.ToArray()).Run();
                    else
                        logger.Debug.Emit("{0}: nothing to do: obj '{0}' is already at '{1}'", substObj.ID, toLocObjID);
                }

                eeTrace.ExtraMessage = ec;
            }

            return ec;
        }

        public static string SetSubstProcState(this IE039TableUpdater table, IE039Object substObj, SubstProcState targetSPS, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.All, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = (substObj != null) ? substObj.ID : default(E039ObjectID);

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' {2}".CheckedFormat(Fcns.CurrentMethodName, substObjID, targetSPS)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                ec = updateItemList.GenerateE090UpdateItems(substObj, targetSPS: targetSPS, updateBehavior: updateBehavior, addSyncExternalItem: addSyncExternalItem);

                if (ec.IsNullOrEmpty())
                {
                    if (updateItemList.Count > 0)
                        ec = table.Update(updateItemList.ToArray()).Run();
                    else
                        logger.Debug.Emit("{0}: nothing to do: obj '{0}' sps is already {1}", substObj.ID, targetSPS);
                }

                eeTrace.ExtraMessage = ec;
            }

            return ec;
        }

        public static E039UpdateItem.AddObject GenerateCreateE090SubstLocItems(this string substLocName, List<E039UpdateItem> updateItemList, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addIfNeeded = false, bool addSyncExternalItem = false)
        {
            E039UpdateItem.AddObject addObjectItem;

            updateItemList.GenerateCreateE090SubstLocItems(substLocName, out addObjectItem, attributes: attributes, flags: flags, addIfNeeded: addIfNeeded, addSyncExternalItem: addSyncExternalItem);

            return addObjectItem;
        }

        public static List<E039UpdateItem> GenerateCreateE090SubstLocItems(this List<E039UpdateItem> updateItemList, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addIfNeeded = false, bool addSyncExternalItem = false)
        {
            E039ObjectID substLocObjID = new E039ObjectID(substLocName, Constants.SubstrateLocationObjectType, assignUUID: true);

            updateItemList.Add(addObjectUpdateItem = new E039UpdateItem.AddObject(substLocObjID, attributes: attributes, flags: flags, ifNeeded: addIfNeeded));
            updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substLocObjID, E039ObjectID.Empty, Constants.ContainsLinkKey), ifNeeded: addIfNeeded));
            if (addSyncExternalItem)
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return updateItemList;
        }

        public static List<E039UpdateItem> GenerateCreateE090SubstItems(this List<E039UpdateItem> updateItemList, string substName, out E039UpdateItem.AddObject addObjectUpdateItem, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addSyncExternalItem = false)
        {
            E039ObjectID substObjID = new E039ObjectID(substName, Constants.SubstrateObjectType, assignUUID: true);

            destSubstLocObjID = destSubstLocObjID ?? srcSubstLocObjID ?? E039ObjectID.Empty;

            E090SubstInfo useInitialState = initialE090SubstrateObjState ?? E090SubstInfo.Initial;
            attributes = useInitialState.UpdateAttributeValues(new NamedValueSet()).MergeWith(attributes, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate).MakeReadOnly();

            updateItemList.Add(addObjectUpdateItem = new E039UpdateItem.AddObject(substObjID, attributes: attributes, flags: flags));

            if (!srcSubstLocObjID.IsNullOrEmpty())
                updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substObjID, srcSubstLocObjID, Constants.SourceLocationLinkKey)));

            if (!destSubstLocObjID.IsEmpty)
                updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substObjID, destSubstLocObjID, Constants.DestinationLocationLinkKey)));

            E039ObjectID createAtLocID = ((useInitialState.STS == SubstState.AtDestination) ? destSubstLocObjID : srcSubstLocObjID) ?? srcSubstLocObjID ?? destSubstLocObjID;

            if (!createAtLocID.IsEmpty)
                updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(createAtLocID, substObjID, Constants.ContainsLinkKey)));

            if (addSyncExternalItem)
                updateItemList.Add(new E039UpdateItem.SyncExternal());

            return updateItemList;
        }

        public static string GenerateE090UpdateItems(this List<E039UpdateItem> updateItemList, IE039Object substObj, SubstProcState targetSPS = SubstProcState.Undefined, E039ObjectID toLocObjID = null, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None, bool addSyncExternalItem = false)
        {
            if (substObj == null || updateItemList == null)
                return "{0}: given invalid or null parameter".CheckedFormat(Fcns.CurrentMethodName);

            toLocObjID = toLocObjID ?? E039ObjectID.Empty;

            E090SubstInfo currentSubstInfo = new E090SubstInfo(substObj);

            if (!substObj.IsSubstrate())
                return "{0}: given non substrate obj '{1}'".CheckedFormat(Fcns.CurrentMethodName, substObj.ID);

            if (!currentSubstInfo.IsValid)
                return "{0}: given unusable obj: {1}".CheckedFormat(Fcns.CurrentMethodName, currentSubstInfo);

            bool currentIsNeedsProcessing = (currentSubstInfo.SPS == SubstProcState.NeedsProcessing);
            bool currentIsInProcess = (currentSubstInfo.SPS == SubstProcState.InProcess);

            bool updateSPS = (targetSPS != SubstProcState.Undefined && targetSPS != currentSubstInfo.SPS);

            E039ObjectID fromLocObjID = currentSubstInfo.LocsLinkedToHere.FirstOrDefault().FromID;

            bool updateLocID = (!toLocObjID.IsEmpty && toLocObjID.Name != currentSubstInfo.LocID);

            if (updateLocID && fromLocObjID.IsEmpty)
                logger.Debug.Emit("{0}: attempt to move {1} issue: from location '{2}' object is not usable", Fcns.CurrentMethodName, substObj.ID, currentSubstInfo.LocID);

            SubstState inferredTargetSTS = currentSubstInfo.GetInferredSTS(updateLocID ? toLocObjID.Name : null, sps: updateSPS ? (SubstProcState ?) targetSPS : null);
            bool updateSTS = (inferredTargetSTS != currentSubstInfo.STS && updateBehavior.IsSet(E090StateUpdateBehavior.AutoUpdateSTS));

            // general pattern:

            // generate AtWork STS and/or related locationChanged transition

            if (inferredTargetSTS == SubstState.AtWork)
            {
                if (updateSTS)
                    updateItemList.AddSetAttributesItem(obj: substObj, attributes: new NamedValueSet() { { "SubstState", inferredTargetSTS } });

                if (updateLocID)
                    updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(toLocObjID, substObj.ID, Constants.ContainsLinkKey), autoUnlinkFromPriorByTypeStr: true));
            }

            // generate InProcess transition if needed
            // and then generate Final SPS transition if needed
            if (updateSPS)
            {
                bool addInProcessSPSTranstion = false;
                bool addToTargetSPSTransition = false;

                switch (targetSPS)
                {
                    case SubstProcState.NeedsProcessing:
                        addToTargetSPSTransition = (currentIsInProcess && updateBehavior.IsSet(E090StateUpdateBehavior.AllowReturnToNeedsProcessing));
                        break;
                    case SubstProcState.InProcess:
                        addToTargetSPSTransition = currentIsNeedsProcessing;
                        break;
                    case SubstProcState.Lost:
                    case SubstProcState.Skipped:
                        addToTargetSPSTransition = currentIsNeedsProcessing || currentIsInProcess;
                        break;
                    case SubstProcState.Processed:
                    case SubstProcState.Aborted:
                    case SubstProcState.Stopped:
                    case SubstProcState.Rejected:
                        if (currentIsInProcess)
                            addToTargetSPSTransition = true;
                        else if (currentIsNeedsProcessing && updateBehavior.IsSet(E090StateUpdateBehavior.AutoInProcess))
                        {
                            addInProcessSPSTranstion = true;
                            addToTargetSPSTransition = true;
                        }
                        break;

                    default:
                        break;
                }

                if (addToTargetSPSTransition)
                {
                    if (addInProcessSPSTranstion)
                        updateItemList.AddSetAttributesItem(obj: substObj, attributes: new NamedValueSet() { { "SubstProcState", SubstProcState.InProcess } });
                    updateItemList.AddSetAttributesItem(obj: substObj, attributes: new NamedValueSet() { { "SubstProcState", targetSPS } });
                }
                else
                {
                    return "{0}: cannot set obj '{0}' to sps:{1} [from:{2}]".CheckedFormat(Fcns.CurrentMethodName, substObj.ID, targetSPS, currentSubstInfo.STS);
                }
            }

            // generate AtSource or AtDestination STS and/or related locationChanged transition
            if (inferredTargetSTS == SubstState.AtSource || inferredTargetSTS == SubstState.AtDestination)
            {
                if (updateLocID)
                    updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(toLocObjID, substObj.ID, Constants.ContainsLinkKey), autoUnlinkFromPriorByTypeStr: true));

                if (updateSTS)
                    updateItemList.AddSetAttributesItem(obj: substObj, attributes: new NamedValueSet() { { "SubstState", inferredTargetSTS } });
            }

            if (addSyncExternalItem)
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
    }

    #endregion

    #region E090SubstLocObserver and E090SubstObserver objects

    public class E090SubstLocObserver : E039.E039ObjectObserverWithInfoExtraction<E090SubstLocInfo>
    {
        public E090SubstLocObserver(ISequencedObjectSource<IE039Object, int> objLocPublisher, bool alsoObserveContents = true)
            : base(objLocPublisher, (obj) => new E090SubstLocInfo(obj))
        {
            AlsoObserveContents = alsoObserveContents;

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

    public class E090SubstObserver : E039.E039ObjectObserverWithInfoExtraction<E090SubstInfo>
    {
        public E090SubstObserver(ISequencedObjectSource<IE039Object, int> objPublisher)
            : base(objPublisher, (obj) => new E090SubstInfo(obj))
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
        /// <summary>Normal constructor - attempts to decode the contents from the given e039 object <paramref name="obj"/></summary>
        public E090SubstLocInfo(IE039Object obj)
            : this()
        {
            ObjID = (obj != null) ? obj.ID : E039ObjectID.Empty;
            LinkToSubst = obj.FindFirstLinkTo(Constants.ContainsLinkKey);
            SrcLinksToHere = obj.FindLinksFrom(Constants.SourceLocationLinkKey).ToArray();
            DestLinksToHere = obj.FindLinksFrom(Constants.DestinationLocationLinkKey).ToArray();
        }

        /// <summary>Copy constructor</summary>
        public E090SubstLocInfo(E090SubstLocInfo other)
            : this()
        {
            ObjID = other.ObjID;
            LinkToSubst = other.LinkToSubst;
            SrcLinksToHere = other.SrcLinksToHere.MakeCopyOf(mapNullToEmpty: false);
            DestLinksToHere = other.DestLinksToHere.MakeCopyOf(mapNullToEmpty: false);
        }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

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
        /// Returns true if the contents of this object match the contentents of the given <paramref name="other"/> object
        /// </summary>
        public bool Equals(E090SubstLocInfo other)
        {
            return (ObjID.Equals(other.ObjID)
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
    /// <para/>This includes information obtained from the "SubstProcState" (SPS), "SubstState" (STS), "LotID", and "SubstUsage" attributes as well as information
    /// that is derived fromt he "Contains", "SrcLoc" and "DestLoc" links to/from this object.
    /// </summary>
    public struct E090SubstInfo : IEquatable<E090SubstInfo>
    {
        public E090SubstInfo(IE039Object obj) 
            : this()
        {
            _objID = (obj != null) ? obj.ID : null;

            INamedValueSet attributes = (obj != null) ? obj.Attributes : NamedValueSet.Empty;
            
            SPS = attributes["SubstProcState"].VC.GetValue(rethrow: false, defaultValue: SubstProcState.Undefined);
            STS = attributes["SubstState"].VC.GetValue(rethrow: false, defaultValue: SubstState.Undefined);
            LotID = attributes["LotID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
            SubstUsageStr = attributes["SubstUsage"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();

            LocsLinkedToHere = obj.FindLinksFrom(Constants.ContainsLinkKey).ToArray();
            LinkToSrc = obj.FindFirstLinkTo(Constants.SourceLocationLinkKey);
            LinkToDest = obj.FindFirstLinkTo(Constants.DestinationLocationLinkKey);
        }

        public E090SubstInfo(E090SubstInfo other)
            : this()
        {
            _objID = other._objID;

            SPS = other.SPS;
            STS = other.STS;
            LotID = other.LotID;
            SubstUsageStr = other.SubstUsageStr;

            LocsLinkedToHere = other.LocsLinkedToHere.MakeCopyOf(mapNullToEmpty: false);
            LinkToSrc = other.LinkToSrc;
            LinkToDest = other.LinkToDest;
        }

        private static readonly E039Link[] emptyLinkArray = EmptyArrayFactory<E039Link>.Instance;

        public NamedValueSet UpdateAttributeValues(NamedValueSet nvs)
        {
            nvs.SetValue("SubstProcState", SPS);
            nvs.SetValue("SubstState", STS);

            if (!LotID.IsNullOrEmpty() || nvs.Contains("LotID"))
                nvs.SetValue("LotID", LotID);

            if (!SubstUsageStr.IsNullOrEmpty() || nvs.Contains("SubstUsage"))
                nvs.SetValue("SubstUsage", SubstUsageStr);

            return nvs;
        }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

        /// <summary>From SubstState attribute</summary>
        public SubstState STS { get; set; }

        /// <summary>From SubstProcState attribute</summary>
        public SubstProcState SPS { get; set; }

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
        public string LocID { get { return LocsLinkedToHere.SafeAccess(0).FromID.Name; } }

        /// <summary>Gives the set of links that are linked to this object using the "Contains" key.</summary>
        public E039Link[] LocsLinkedToHere { get; set; }

        /// <summary>Proxy link for SubstSource attribute - gives link to the SubstLoc object which is the Source location for this substrate. [SrcLoc]</summary>
        public E039Link LinkToSrc { get; set; }

        /// <summary>Proxy link for SubstDestination attribute- gives link to the SubstLoc object which is the Destination location for this substrate. [DestLoc]</summary>
        public E039Link LinkToDest { get; set; }

        /// <summary>Gives an empty E090SubstInfo object to be used as a default value.</summary>
        public static E090SubstInfo Empty { get { return new E090SubstInfo() { LotID = "", SubstUsageStr = "", STS = SubstState.Undefined, SPS = SubstProcState.Undefined, LocsLinkedToHere = emptyLinkArray }; } }

        /// <summary>Returns true if the contents are the same as the contents of the Empty E090SubstInfo object.</summary>
        public bool IsEmpty { get { return this.Equals(Empty); } }

        /// <summary>
        /// Gives the initial E090SubstInfo to be used with newly created E090 objects.
        /// <para/>Sets: LotID = "", SubstUsage = Product, STS = AtSource, and SPS = NeedsProcessing.
        /// </summary>
        public static E090SubstInfo Initial { get { return new E090SubstInfo() { LotID = "", SubstUsage = SubstUsage.Product, STS = SubstState.AtSource, SPS = SubstProcState.NeedsProcessing }; } }

        /// <summary>
        /// Returns true if this object's contents are equal to the contents of the given <paramref name="other"/> object.
        /// </summary>
        public bool Equals(E090SubstInfo other)
        {
            return (ObjID.Equals(other.ObjID)
                    && STS == other.STS
                    && SPS == other.SPS
                    && LotID == other.LotID
                    && SubstUsageStr == other.SubstUsageStr
                    && LocsLinkedToHere.IsEqualTo(other.LocsLinkedToHere)
                    && LinkToSrc.Equals(other.LinkToSrc)
                    && LinkToDest.Equals(other.LinkToDest)
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
                        && LocsLinkedToHere.SafeLength() == 1
                        && !LinkToSrc.IsEmpty 
                        && !LinkToDest.IsEmpty); 
            } 
        }

        /// <summary>
        /// Infers the appropriate STS from the given locID (or the current LocID if given null) as:
        /// AtSource if NeedsProcessing and locID matches SrcLocID
        /// AtDestination if ProcessingComlete and locID matches DestLocID
        /// otherwise AtWork
        /// </summary>
        public SubstState GetInferredSTS(string locID = null, SubstProcState ? sps = null)
        {
            locID = locID.MapNullOrEmptyTo(LocID);

            SubstProcState useSPS = sps ?? SPS;

            if (useSPS.IsNeedsProcessing() && locID == LinkToSrc.ToID.Name)
                return SubstState.AtSource;
            else if (useSPS.IsProcessingComplete() && locID == LinkToDest.ToID.Name)
                return SubstState.AtDestination;
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

            return sb.ToString();
        }
    }

    #endregion

    #region State and Event eunumerations and related extension methods

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
}
