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
        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, Action<E039UpdateItem.AddObject> addedObjectDelegate, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090SubstLoc(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags);

            if (addedObjectDelegate != null)
                addedObjectDelegate(addObjectUpdateItem);

            return ec;
        }

        public static string CreateE090SubstLoc(this IE039TableUpdater tableUpdater, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            E039UpdateItem[] updateItemArray = new List<E039UpdateItem>().GenerateCreateE090SubstLocItems(substLocName, out addObjectUpdateItem, attributes: attributes, flags: flags).ToArray();

            return tableUpdater.Update(updateItemArray).Run();
        }

        public static string CreateE090Subst(this IE039TableUpdater tableUpdater, string substName, Action<E039UpdateItem.AddObject> addedObjectDelegate, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            E039UpdateItem.AddObject addObjectUpdateItem;

            string ec = tableUpdater.CreateE090Subst(substName, out addObjectUpdateItem, srcSubstLocObjID, destSubstLocObjID: destSubstLocObjID, initialE090SubstrateObjState: initialE090SubstrateObjState, attributes: attributes, flags: flags);

            if (addedObjectDelegate != null)
                addedObjectDelegate(addObjectUpdateItem);

            return ec;
        }

        public static string CreateE090Subst(this IE039TableUpdater tableUpdater, string substName, out E039UpdateItem.AddObject addObjectUpdateItem, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            E039UpdateItem[] updateItemArray = new List<E039UpdateItem>().GenerateCreateE090SubstItems(substName, out addObjectUpdateItem, srcSubstLocObjID, destSubstLocObjID: destSubstLocObjID, initialE090SubstrateObjState: initialE090SubstrateObjState, attributes: attributes, flags: flags).ToArray();

            return tableUpdater.Update(updateItemArray).Run();
        }

        public static string NoteSubstMoved(this IE039TableUpdater table, IE039Object substObj, IE039Object toLocObj, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.All)
        {
            E039ObjectID substObjID = (substObj != null) ? substObj.ID : E039ObjectID.Empty;
            E039ObjectID toLocObjID = (toLocObj != null) ? toLocObj.ID : E039ObjectID.Empty;

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' '{2}'".CheckedFormat(Fcns.CurrentMethodName, substObjID, toLocObjID)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                ec = updateItemList.GenerateE090UpdateItems(substObj, toLocObjID: toLocObjID, updateBehavior: updateBehavior);

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

        public static string SetSubstProcState(this IE039TableUpdater table, IE039Object substObj, SubstProcState targetSPS, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.All)
        {
            E039ObjectID substObjID = (substObj != null) ? substObj.ID : default(E039ObjectID);

            string ec = null;
            using (var eeTrace = new Logging.EnterExitTrace(logger, "{0}: obj '{1}' {2}".CheckedFormat(Fcns.CurrentMethodName, substObjID, targetSPS)))
            {
                List<E039UpdateItem> updateItemList = new List<E039UpdateItem>();

                ec = updateItemList.GenerateE090UpdateItems(substObj, targetSPS: targetSPS, updateBehavior: updateBehavior);

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

        public static E039UpdateItem.AddObject GenerateCreateE090SubstLocItems(this string substLocName, List<E039UpdateItem> updateItemList, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addIfNeeded = false)
        {
            E039UpdateItem.AddObject addObjectItem;

            updateItemList.GenerateCreateE090SubstLocItems(substLocName, out addObjectItem, attributes: attributes, flags: flags, addIfNeeded: addIfNeeded);

            return addObjectItem;
        }

        public static List<E039UpdateItem> GenerateCreateE090SubstLocItems(this List<E039UpdateItem> updateItemList, string substLocName, out E039UpdateItem.AddObject addObjectUpdateItem, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool addIfNeeded = false)
        {
            E039ObjectID substLocObjID = new E039ObjectID(substLocName, Constants.SubstrateLocationObjectType, assignUUID: true);

            updateItemList.Add(addObjectUpdateItem = new E039UpdateItem.AddObject(substLocObjID, attributes: attributes, flags: flags, ifNeeded: addIfNeeded));
            updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substLocObjID, E039ObjectID.Empty, "Contains"), ifNeeded: addIfNeeded));

            return updateItemList;
        }

        public static List<E039UpdateItem> GenerateCreateE090SubstItems(this List<E039UpdateItem> updateItemList, string substName, out E039UpdateItem.AddObject addObjectUpdateItem, E039ObjectID srcSubstLocObjID, E039ObjectID destSubstLocObjID = null, E090SubstInfo? initialE090SubstrateObjState = null, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None)
        {
            E039ObjectID substObjID = new E039ObjectID(substName, Constants.SubstrateObjectType, assignUUID: true);

            destSubstLocObjID = destSubstLocObjID ?? srcSubstLocObjID ?? E039ObjectID.Empty;

            E090SubstInfo useInitialState = initialE090SubstrateObjState ?? E090SubstInfo.Initial;
            attributes = useInitialState.UpdateAttributeValues(new NamedValueSet()).MergeWith(attributes, mergeBehavior: NamedValueMergeBehavior.AddAndUpdate).MakeReadOnly();

            updateItemList.Add(addObjectUpdateItem = new E039UpdateItem.AddObject(substObjID, attributes: attributes, flags: flags));

            if (!srcSubstLocObjID.IsNullOrEmpty())
                updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substObjID, srcSubstLocObjID, "SrcLoc")));

            if (!destSubstLocObjID.IsEmpty)
                updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(substObjID, destSubstLocObjID, "DestLoc")));

            E039ObjectID createAtLocID = ((useInitialState.STS == SubstState.AtDestination) ? destSubstLocObjID : srcSubstLocObjID) ?? srcSubstLocObjID ?? destSubstLocObjID;

            if (!createAtLocID.IsEmpty)
                updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(createAtLocID, substObjID, "Contains")));

            return updateItemList;
        }

        public static string GenerateE090UpdateItems(this List<E039UpdateItem> updateItemList, IE039Object substObj, SubstProcState targetSPS = SubstProcState.Undefined, E039ObjectID toLocObjID = null, E090StateUpdateBehavior updateBehavior = E090StateUpdateBehavior.None)
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
                    updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(toLocObjID, substObj.ID, "Contains"), autoUnlinkFromPriorByTypeStr: true));
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
                    updateItemList.Add(new E039UpdateItem.AddLink(new E039Link(toLocObjID, substObj.ID, "Contains"), autoUnlinkFromPriorByTypeStr: true));

                if (updateSTS)
                    updateItemList.AddSetAttributesItem(obj: substObj, attributes: new NamedValueSet() { { "SubstState", inferredTargetSTS } });
            }

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

    public class E090SubstLocObserver : ISequencedObjectSourceObserver<IE039Object>
    {
        public E090SubstLocObserver(ISequencedObjectSource<IE039Object, int> objLocPublisher, bool alsoObserveContents = true)
        {
            objLocObserver = new SequencedRefObjectSourceObserver<IE039Object, int>(objLocPublisher);
            AlsoObserveContents = alsoObserveContents;
            Update(forceUpdate: true);
        }

        public IE039Object Object { get { return objLocObserver.Object; } }
        public E090SubstLocInfo Info { get; private set; }

        public bool AlsoObserveContents { get; private set; }

        public IE039Object ContainsObject { get; private set; }
        public E090SubstInfo ContainsSubstInfo { get; private set; }

        public bool IsUpdateNeeded { get { return objLocObserver.IsUpdateNeeded; } set { objLocObserver.IsUpdateNeeded = value; } }

        bool ISequencedSourceObserver.Update()
        {
            return this.Update(forceUpdate: false);
        }

        public bool Update(bool forceUpdate = false)
        {
            bool didUpdate = objLocObserver.Update();

            if (didUpdate || forceUpdate)
            {
                Info = new E090SubstLocInfo(Object);

                if (AlsoObserveContents)
                {
                    ContainsObject = Info.GetContainedE039Object();
                    ContainsSubstInfo = new E090SubstInfo(ContainsObject);
                }
            }

            return didUpdate;
        }

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public bool HasBeenSet { get { return objLocObserver.HasBeenSet; } }

        /// <summary>Returns the current sequence number.  May return zero if sequence number is set to skip zero and Increment is in progress on another thread.</summary>
        public int SequenceNumber { get { return objLocObserver.SequenceNumber; } }

        /// <summary>Returns the current sequence number read as a volatile (no locking) - May return zero if sequence number is set to skip zero and Increment is in progress on another thread</summary>
        public int VolatileSequenceNumber { get { return objLocObserver.VolatileSequenceNumber; } }

        public ISequencedObjectSourceObserver<IE039Object> UpdateInline()
        {
            Update();
            return this;
        }

        ISequencedSourceObserver ISequencedSourceObserver.UpdateInline()
        {
            Update();
            return this;
        }

        private SequencedRefObjectSourceObserver<IE039Object, int> objLocObserver;
    }

    public class E090SubstObserver : ISequencedObjectSourceObserver<IE039Object>
    {
        public E090SubstObserver(ISequencedObjectSource<IE039Object, int> objLocPublisher)
        {
            objObserver = new SequencedRefObjectSourceObserver<IE039Object, int>(objLocPublisher);
            Info = new E090SubstInfo(Object);
        }

        public IE039Object Object { get { return objObserver.Object; } }
        public E090SubstInfo Info { get; private set; }

        public bool IsUpdateNeeded { get { return objObserver.IsUpdateNeeded; } set { objObserver.IsUpdateNeeded = value; } }

        public bool Update()
        {
            bool didUpdate = objObserver.Update();

            if (didUpdate)
                Info = new E090SubstInfo(Object);

            return didUpdate;
        }

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public bool HasBeenSet { get { return objObserver.HasBeenSet; } }

        /// <summary>Returns the current sequence number.  May return zero if sequence number is set to skip zero and Increment is in progress on another thread.</summary>
        public int SequenceNumber { get { return objObserver.SequenceNumber; } }

        /// <summary>Returns the current sequence number read as a volatile (no locking) - May return zero if sequence number is set to skip zero and Increment is in progress on another thread</summary>
        public int VolatileSequenceNumber { get { return objObserver.VolatileSequenceNumber; } }

        public ISequencedObjectSourceObserver<IE039Object> UpdateInline()
        {
            Update();
            return this;
        }

        ISequencedSourceObserver ISequencedSourceObserver.UpdateInline()
        {
            Update();
            return this;
        }

        private SequencedRefObjectSourceObserver<IE039Object, int> objObserver;
    }
    #endregion

    #region E090SubstLocInfo, E090SubstInfo helper structs.

    /// <summary>
    /// This is a helper object that is generally used to extract Substrate Location related information from an SubstLoc IE039Object.
    /// <para/>At present all normal SubstLoc related information is derived from the "Contains", "SrcLoc" and "DestLoc" links from/to the given location.
    /// </summary>
    public struct E090SubstLocInfo : IEquatable<E090SubstLocInfo>
    {
        public E090SubstLocInfo(IE039Object obj)
            : this()
        {
            Obj = obj;
            ObjID = (obj != null) ? obj.ID : E039ObjectID.Empty;
            LinkToSubst = (obj != null) ? obj.LinksToOtherObjectsList.FirstOrDefault(link => (link.Key == "Contains")) : default(E039Link);
            SrcLinksToHere = (obj != null) ? obj.LinksFromOtherObjectsList.Where(link => (link.Key == "SrcLoc")).ToArray() : emptyLinkArray;
            DestLinksToHere = (obj != null) ? obj.LinksFromOtherObjectsList.Where(link => (link.Key == "DestLoc")).ToArray() : emptyLinkArray;
        }

        public E090SubstLocInfo(E090SubstLocInfo other)
            : this()
        {
            Obj = other.Obj;
            ObjID = other.ObjID;
            LinkToSubst = other.LinkToSubst;
            SrcLinksToHere = other.SrcLinksToHere.MakeCopyOf(mapNullToEmpty: false);
            DestLinksToHere = other.DestLinksToHere.MakeCopyOf(mapNullToEmpty: false);
        }

        private IE039Object Obj { get; set; }

        /// <summary>Gives the E039ObjectID of the object from which this structure was created, or E039ObjectID.Empty if the default constructor was used.</summary>
        public E039ObjectID ObjID { get { return _objID ?? E039ObjectID.Empty; } set { _objID = value; } }
        private E039ObjectID _objID;

        public E039Link LinkToSubst { get; set; }
        public E039Link[] SrcLinksToHere { get; set; }
        public E039Link[] DestLinksToHere { get; set; }

        public SubstLocState SLS 
        { 
            get 
            {
                if (!LinkToSubst.IsToIDEmpty)
                    return SubstLocState.Occupied;
                else if (LinkToSubst.IsEmpty)
                    return SubstLocState.Undefined;
                else 
                    return SubstLocState.Unoccupied; 
            } 
        }

        /// <summary>
        /// Attempts to follow the (first) LinkToSubst link and obtain the IE039Object for the current state of the linked object from its table.  
        /// Returns the resulting IE039Object, or the given <paramref name="fallbackObj"/> if the link could not be successfully followed.
        /// </summary>
        public IE039Object GetContainedE039Object(IE039Object fallbackObj = null)
        {
            return LinkToSubst.ToID.GetObject(fallbackObj);
        }

        private static readonly E039Link[] emptyLinkArray = new E039Link[0];

        /// <summary>Gives an empty E090SubstInfo object to be used as a default value.</summary>
        public static E090SubstLocInfo Empty { get { return new E090SubstLocInfo() { ObjID = E039ObjectID.Empty, SrcLinksToHere = emptyLinkArray, DestLinksToHere = emptyLinkArray }; } }

        /// <summary>Returns true if the contents are the same as the contents of the Empty E090SubstInfo object.</summary>
        public bool IsEmpty { get { return this.Equals(Empty); } }

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
            Obj = obj;
            _objID = (obj != null) ? obj.ID : null;

            INamedValueSet attributes = (obj != null) ? obj.Attributes : NamedValueSet.Empty;
            
            SPS = attributes["SubstProcState"].VC.GetValue(rethrow: false, defaultValue: SubstProcState.Undefined);
            STS = attributes["SubstState"].VC.GetValue(rethrow: false, defaultValue: SubstState.Undefined);
            LotID = attributes["LotID"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();
            SubstUsageStr = attributes["SubstUsage"].VC.GetValue<string>(rethrow: false).MapNullToEmpty();

            LocsLinkedToHere = (obj != null) ? obj.LinksFromOtherObjectsList.Where(link => (link.Key == "Contains")).ToArray() : emptyLinkArray;
            LinkToSrc = (obj != null) ? obj.LinksToOtherObjectsList.FirstOrDefault(link => (link.Key == "SrcLoc")) : default(E039Link);
            LinkToDest = (obj != null) ? obj.LinksToOtherObjectsList.FirstOrDefault(link => (link.Key == "DestLoc")) : default(E039Link);
        }

        public E090SubstInfo(E090SubstInfo other)
            : this()
        {
            Obj = other.Obj;
            _objID = other._objID;

            SPS = other.SPS;
            STS = other.STS;
            LotID = other.LotID;
            SubstUsageStr = other.SubstUsageStr;

            LocsLinkedToHere = other.LocsLinkedToHere.MakeCopyOf(mapNullToEmpty: false);
            LinkToSrc = other.LinkToSrc;
            LinkToDest = other.LinkToDest;
        }

        private static readonly E039Link[] emptyLinkArray = new E039Link[0];

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

        private IE039Object Obj { get; set; }

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

        /// <summary>Proxy for SubstLocID attribute - Gives the Name of the FromID for the first Contains link from that links to this object</summary>
        public string LocID { get { return LocsLinkedToHere.SafeAccess(0).FromID.Name; } }
        public E039Link [] LocsLinkedToHere { get; set; }

        /// <summary>Proxy link for SubstSource attribute - gives link to the SubstLoc object which is the Source location for this substrate.</summary>
        public E039Link LinkToSrc { get; set; }

        /// <summary>Proxy link for SubstDestination attribute- gives link to the SubstLoc object which is the Destination location for this substrate.</summary>
        public E039Link LinkToDest { get; set; }

        /// <summary>Gives an empty E090SubstInfo object to be used as a default value.</summary>
        public static E090SubstInfo Empty { get { return new E090SubstInfo() { LotID = "", SubstUsageStr = "", STS = SubstState.Undefined, SPS = SubstProcState.Undefined, LocsLinkedToHere = emptyLinkArray }; } }

        /// <summary>Returns true if the contents are the same as the contents of the Empty E090SubstInfo object.</summary>
        public bool IsEmpty { get { return this.Equals(Empty); } }

        /// <summary>Gives the initial E090SubstInfo to be used with newly created E090 objects.</summary>
        public static E090SubstInfo Initial { get { return new E090SubstInfo() { LotID = "", SubstUsage = SubstUsage.Product, STS = SubstState.AtSource, SPS = SubstProcState.NeedsProcessing }; } }

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
        Wafer = 0,
        FlatPanel = 1,
        CD = 2,
        Mask = 3,
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        Undefined = -1,
    }

    /// <summary>
    /// Substrate Usage enumeration:
    /// <para>Product (0), Test (1), Filler (2), Undefined (-1)</para>
    /// </summary>
    [DataContract(Namespace = MosaicLib.Constants.E090NameSpace)]
    public enum SubstUsage : sbyte
    {
        Product = 0,
        Test = 1,
        Filler = 2,
        /// <summary>-1: Local default value to use when there is no valid value.</summary>
        Undefined = -1,
        Other = -2,
    }

    public static partial class ExtensionMethods
    {
        public static bool IsOccupied(this SubstLocState sls) { return (sls == SubstLocState.Occupied); }
        public static bool IsUnoccupied(this SubstLocState sls) { return (sls == SubstLocState.Unoccupied); }

        public static bool IsAtSource(this SubstState sts) { return (sts == SubstState.AtSource); }
        public static bool IsAtWork(this SubstState sts) { return (sts == SubstState.AtWork); }
        public static bool IsAtDestination(this SubstState sts) { return (sts == SubstState.AtDestination); }

        public static bool IsNeedsProcessing(this SubstProcState sps) { return (sps == SubstProcState.NeedsProcessing); }
        public static bool IsInProcess(this SubstProcState sps) { return (sps == SubstProcState.InProcess); }

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
