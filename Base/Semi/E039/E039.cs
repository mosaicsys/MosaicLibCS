//-------------------------------------------------------------------
/*! @file E039.cs
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
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Modular.Persist;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.Semi;
using MosaicLib.Semi.E039.Details;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Utils.StringMatching;

namespace MosaicLib.Semi.E039
{
    #region E039Table observation

    /// <summary>
    /// This interface is used by clients of an E039Table manager part to get observer access to the published state of the objects that are stored in the table.
    /// This interface is functional immediately after the part has been constructed and does not require that the part has been started or that it is online.
    /// </summary>
    public interface IE039TableObserver : IActivePartBase
    {
        /// <summary>
        /// Gives acceess to the notifcation object that is used to publish the last generated set of sequence numbers.
        /// </summary>
        INotificationObject<E039TableSeqNums> SeqNumsPublisher { get; }

        /// <summary>
        /// Gives the caller the ability to obtain copies of a set of objects defined first by the <paramref name="typeFilter"/> and second by the <paramref name="instanceFilter"/>, which are known to be of the indicated type (based on their behavior).
        /// <para/>null filters will be replaced with a filter that returns true in all cases.
        /// </summary>
        IE039Object[] GetObjects(E039TypeFilter typeFilter = null, E039InstanceFilter instanceFilter = null);

        /// <summary>
        /// Gives the caller the ability to obtain a count of the set of objects defined first by the <paramref name="typeFilter"/> and second by the <paramref name="instanceFilter"/>, which are known to be of the indicated type (based on their behavior).
        /// <para/>null filters will be replaced with a filter that returns true in all cases.
        /// </summary>
        int GetObjectCount(E039TypeFilter typeFilter = null, E039InstanceFilter instanceFilter = null);

        /// <summary>
        /// Gives the caller the ability to obtain an publisher, of the indicated type, for the object that is identified by the given <paramref name="objID"/>.  
        /// The returned notification object will be null if no object is found with the given objID or if the identified object does not support publiciation using the indicated type.
        /// </summary>
        INotificationObject<IE039Object> GetPublisher(E039ObjectID objID);
    }

    /// <summary>
    /// Helper extension methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// This method is a shorthand for table.GetPublisher(link.ToID);
        /// </summary>
        public static INotificationObject<IE039Object> GetLinkToPublisher(this IE039TableObserver table, E039Link link)
        {
            return table.GetPublisher(link.ToID);
        }

        /// <summary>
        /// This method is a shorthand for table.GetPublisher(link.FromID);
        /// </summary>
        public static INotificationObject<IE039Object> GetLinkFromPublisher(this IE039TableObserver table, E039Link link)
        {
            return table.GetPublisher(link.FromID);
        }

        /// <summary>
        /// tableObserver helper method.  Attempts to obtain the object for a given <paramref name="objID"/>.  
        /// First attempts to obtain the publisher for the <paramref name="objID"/> and then attempts to obtain the Object from the publisher.
        /// </summary>
        public static IE039Object GetObject(this IE039TableObserver tableObserver, E039ObjectID objID, IE039Object fallbackValue = null)
        {
            return ((tableObserver != null) ? tableObserver.GetPublisher(objID).GetObject(fallbackValue: null) : null) ?? fallbackValue;
        }

        /// <summary>
        /// Object Publisher helper method.  Attempts to obtain the object for a given <paramref name="publisher"/>.  
        /// If the <paramref name="publisher"/> is non-null and it contains a non-null Object then this method returns that value.
        /// Otherwise it returns the given <paramref name="fallbackValue"/>
        /// </summary>
        public static IE039Object GetObject(this IObjectSource<IE039Object> publisher, IE039Object fallbackValue = null)
        {
            IE039Object obj = (publisher != null) ? publisher.Object : null;

            return obj ?? fallbackValue;
        }
    }

    /// <summary>
    /// This structure contains the set of counter and sequence number values that are used to indicate (and count) when specific types of table changes or publications take place.
    /// </summary>
    public struct E039TableSeqNums : IEquatable<E039TableSeqNums>
    {
        /// <summary>
        /// Gives the sequence number assigned to the most recent table change (due to object addition or removal)
        /// </summary>
        public ulong TableChangeSeqNum;

        /// <summary>
        /// Gives the total number of type tables that have been added
        /// </summary>
        public ulong AddedTypeCount;

        /// <summary>
        /// Gives the total number of objects that have been added
        /// </summary>
        public ulong AddedItemsCount;

        /// <summary>
        /// Gives the total number of objects that have been removed.
        /// </summary>
        public ulong RemovedItemsCount;

        /// <summary>
        /// Gives the sequence number assigned to the most recent object content publication.
        /// </summary>
        public ulong PublishedObjectSeqNum;

        /// <summary>
        /// Returns true if this object and the given other object have the same contents
        /// </summary>
        public bool Equals(E039TableSeqNums other)
        {
            return (TableChangeSeqNum == other.TableChangeSeqNum
                    && AddedTypeCount == other.AddedTypeCount
                    && AddedItemsCount == other.AddedItemsCount
                    && RemovedItemsCount == other.RemovedItemsCount
                    && PublishedObjectSeqNum == other.PublishedObjectSeqNum);
        }
    }

    /// <summary>
    /// This filter is used to determine which <paramref name="type"/> tables are to be included in the filtered output.
    /// </summary>
    public delegate bool E039TypeFilter(string type);

    /// <summary>
    /// This filter is used to determine which <paramref name="obj"/> instances are to be considered for inclusion in the filtered output.
    /// </summary>
    public delegate bool E039InstanceFilter(IE039Object obj);

    /// <summary>
    /// This filter is used to determine which <param name="link"/> instances are to be considered for inclusion in the filtered output.
    /// </summary>
    public delegate bool E039LinkFilter(E039Link link);

    #endregion

    #region IE039TableUpdater, E039TableUpdateItem, related extension methods

    /// <summary>
    /// This interface is used by clients of an E039Table manager part to make changes to the table or to the objects that it contains.
    /// </summary>
    public interface IE039TableUpdater : IE039TableObserver
    {
        /// <summary>
        /// Action factory method.  The resulting action may be used to perform the given update item on the table.
        /// The resulting changes will be published at the completion of all of the update items, or as a side effect of running an explicit sync update item in the update item set.
        /// When the given <paramref name="logConfigSelect"/> is neither null nor empty, the resulting action's ActionLogging.Config will be selected from the dictionary of such objects from the part's configuration to allow the caller to customize action logging on a per update basis.
        /// </summary>
        IBasicAction Update(E039UpdateItem updateItem, string logConfigSelect = null);

        /// <summary>
        /// Action factory method.  The resulting action may be used to perform the given sequence of update items on the corresponding table(s).
        /// The resulting changes will be published at the completion of all of the update items, or as a side effect of running an explicit sync update item in the update item set.
        /// </summary>
        IBasicAction Update(params E039UpdateItem [] updateItems);

        /// <summary>
        /// Action factory method.  The resulting action may be used to perform the given update items on the corresponding table(s).
        /// The resulting changes will be published at the completion of all of the update items, or as a side effect of running an explicit sync update item in the update item set.
        /// When the given <paramref name="logConfigSelect"/> is neither null nor empty, the resulting action's ActionLogging.Config will be selected from the dictionary of such objects from the part's configuration to allow the caller to customize action logging on a per update basis.
        /// </summary>
        IBasicAction Update(E039UpdateItem[] updateItemsArray, string logConfigSelect = null);
    }

    /// <summary>
    /// This abstract class is the base (and containing namespace) for a set of derived object types that are used to define the set of supported
    /// E039 Table Update operations.  
    /// <para/>AddObject, RemoveObject, AddLink, RemoveLink, SetAttributes, TestAndSetAttrbutes, SyncPersist, SyncPublication
    /// </summary>
    public class E039UpdateItem
    {
        protected E039UpdateItem() { }

        public static E039UpdateItem Empty { get { return _empty; } }
        private static readonly E039UpdateItem _empty = new E039UpdateItem();

        /// <summary>
        /// This UpdateItem is used to add an object with the given objID and attributes to the Table.  
        /// The given Flags will be used to control how the addition is performed, and will be re-used when reloading the object from persistent storage
        /// the next time the part is constructed.
        /// </summary>
        public class AddObject : ObjIDAndAttributeBase
        {
            public AddObject(E039ObjectID objID, INamedValueSet attributes = null, E039ObjectFlags flags = E039ObjectFlags.None, bool ifNeeded = false, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate) 
                : base(objID, attributes) 
            {
                Flags = flags;
                IfNeeded = ifNeeded;
                MergeBehavior = mergeBehavior;
            }

            public E039ObjectFlags Flags { get; private set; }

            public bool IfNeeded { get; private set; }

            public NamedValueMergeBehavior MergeBehavior { get; private set; }

            public INotificationObject<IE039Object> AddedObjectPublisher { get; set; }
        }

        /// <summary>
        /// This UpdateItem is used to remove the specified object from the table.  
        /// The RemoveLinkedToOtherObjectsFilter may be used to enable recursive removal and will specify how that removal will be performed.
        /// </summary>
        public class RemoveObject : ObjIDBase
        {
            /// <summary>
            /// Normal constructor.  Caller provides a non-null <paramref name="objID"/> and an optional <paramref name="removeLinkedToOtherObjectsFilter"/>.
            /// When performed as part of an Update operation, this item will attempt to remove the object specified by the <paramref name="objID"/>.  
            /// <para/>If the <paramref name="removeLinkedToOtherObjectsFilter"/> is non-null, it will be used as a filter on the given object (and sub-objects as appropriate)
            /// After the current object has been removed, the objects to which it had been linked with links that this filter matches will be reviewed.
            /// For each such object, if no links to that object remain that match the given filter then the originally linked to object will also be removed.
            /// <para/>For example if the filter matches the link key Contains and a SubstLoc object is removed which contains a standalone Substrate object then
            /// the Substate object will be checked to see if it is the target of any Contains links (after removing the SubstLoc object) and if not then it will
            /// also be removed.
            /// </summary>
            public RemoveObject(E039ObjectID objID, E039LinkFilter removeLinkedToOtherObjectsFilter = null)
                : base(objID) 
            {
                RemoveLinkedToOtherObjectsFilter = removeLinkedToOtherObjectsFilter;
            }

            /// <summary>
            /// Alternate constructor.  Caller provides a non-null <paramref name="obj"/>, from which an E039ObjectID is obtained, and an optional <paramref name="removeLinkedToOtherObjectsFilter"/>.
            /// When performed as part of an Update operation, this item will attempt to remove the object specified by the obtained E039ObjectID.  
            /// <para/>If the <paramref name="removeLinkedToOtherObjectsFilter"/> is non-null, it will be used as a filter on the given object (and sub-objects as appropriate)
            /// After the current object has been removed, the objects to which it had been linked with links that this filter matches will be reviewed.
            /// For each such object, if no links to that object remain that match the given filter then the originally linked to object will also be removed.
            /// <para/>For example if the filter matches the link key Contains and a SubstLoc object is removed which contains a standalone Substrate object then
            /// the Substate object will be checked to see if it is the target of any Contains links (after removing the SubstLoc object) and if not then it will
            /// also be removed.
            /// </summary>
            public RemoveObject(IE039Object obj, E039LinkFilter removeLinkedToOtherObjectsFilter = null) 
                : this(obj.ID, removeLinkedToOtherObjectsFilter) 
            { }

            /// <summary>
            /// When this delegate is non-null, it will be used as a filter on the given object (and sub-objects as appropriate).  
            /// After the current object has been removed, the objects to which it had been linked with links that this filter matches will be reviewed.
            /// For each such object, if no links to that object remain that match the given filter then the originally linked to object will also be removed.
            /// <para/>For example if the filter matches the link key Contains and a SubstLoc object is removed which contains a standalone Substrate object then
            /// the Substate object will be checked to see if it is the target of any Contains links (after removing the SubstLoc object) and if not then it will
            /// also be removed.
            /// </summary>
            public E039LinkFilter RemoveLinkedToOtherObjectsFilter { get; private set; }
        }

        /// <summary>Adds the indicated link from the identified FromID object to the identified ToID object</summary>
        public class AddLink : LinkBase
        {
            /// <summary>
            /// Normal constructor.  Adds the indicated <paramref name="link"/> between the objects identified by its FromID and ToID and using its specified Key.
            /// optional parameters may be used to initialize the AutoUnlinkFromPriorByTypeStr and IfNeeded properties
            /// </summary>
            public AddLink(E039Link link, bool autoUnlinkFromPriorByTypeStr = false, bool ifNeeded = false)
                : base(link) 
            {
                AutoUnlinkFromPriorByTypeStr = autoUnlinkFromPriorByTypeStr;
                IfNeeded = ifNeeded;
            }

            /// <summary>
            /// When true this option requests that the link operation look at the target object (based on ToID) and if it finds a linkFrom link with the same
            /// Key as this link, it asks the prior object that was linked to this link's target object to unlink from it.  
            /// This has the effect of allowing this operation to move the link from a prior from object to the new from object in one step and can be done without
            /// the client needing to explicitly finding the prior from object's ID.
            /// </summary>
            public bool AutoUnlinkFromPriorByTypeStr { get; private set; }

            /// <summary>
            /// When true this option selects that the link request may be redundant and should be ignored if it is.  
            /// This may be the case if the link's ToID is empty and the link already exists (either to another object or not), or if the link already exists to the requested ToID object.
            /// </summary>
            public bool IfNeeded { get; private set; }

            /// <summary>
            /// Debugging and logging helper method
            /// </summary>
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder(base.ToString());

                if (AutoUnlinkFromPriorByTypeStr)
                    sb.Append(" AutoUnlinkFromPrior");

                if (IfNeeded)
                    sb.Append(" IfNeeded");

                return sb.ToString();
            }
        }

        /// <summary>Removes the given link between the identified FromID object and the identified ToID object</summary>
        public class RemoveLink : LinkBase
        {
            /// <summary>
            /// Normal constructor.  Caller provides the contents of the <paramref name="link"/> to be removed
            /// </summary>
            public RemoveLink(E039Link link) : base(link) { }
        }

        /// <summary>Merges the given set of Attributes into the object identified using the given ObjID, using the given MergeBehavior</summary>
        public class SetAttributes : ObjIDAndAttributeBase
        {
            /// <summary>
            /// Single ObjID Constructor.  Caller specifies the <paramref name="objID"/> of the object to apply this change to, the set of <paramref name="attributes"/> to change,
            /// and the <paramref name="mergeBehavior"/> to be used when merging the given <paramref name="attributes"/> in with the target object's current attribute values.
            /// </summary>
            public SetAttributes(E039ObjectID objID, INamedValueSet attributes, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
                : base(objID, attributes)
            {
                MergeBehavior = mergeBehavior;
            }

            /// <summary>
            /// ObjIDSet Constructor.  Caller specifies the <paramref name="objIDSet"/> of the objects to apply this change to, the set of <paramref name="attributes"/> to change,
            /// and the <paramref name="mergeBehavior"/> to be used when merging the given <paramref name="attributes"/> in with each target object's current attribute values.
            /// </summary>
            public SetAttributes(IEnumerable<E039ObjectID> objIDSet, INamedValueSet attributes, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
                : base(objIDSet, attributes)
            {
                MergeBehavior = mergeBehavior;
            }

            /// <summary>
            /// Defines the merge behavior that is to be used when merging the corresponding attributes into the target object's current attribute values.
            /// </summary>
            public NamedValueMergeBehavior MergeBehavior { get; private set; }
        }

        /// <summary>
        /// This is a variant of SetAttributes that supports a triggering set of test conditions (all of which must be met) before the set portion will be performed.
        /// </summary>
        public class TestAndSetAttributes : SetAttributes
        {
            public TestAndSetAttributes(E039ObjectID objID, INamedValueSet testAttributeSet, INamedValueSet attributes, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate, bool failIfTestConditionsNotMet = false)
                : base(objID, attributes, mergeBehavior)
            {
                TestAttributeSet = testAttributeSet.MapNullToEmpty().ConvertToReadOnly();
                FailIfTestConditionsNotMet = failIfTestConditionsNotMet;
            }

            public INamedValueSet TestAttributeSet { get; private set; }
            public bool FailIfTestConditionsNotMet { get; private set; }

            public bool TestConditionsMet { get; set; }

            public override string ToString()
            {
                return "{0} TestAttributes:{1}{2}".CheckedFormat(base.ToString(), TestAttributeSet.SafeToStringSML(), TestConditionsMet ? " ConditionsMet" : "");
            }
        }

        public class SyncPersist : ObjIDBase
        {
            public SyncPersist(E039ObjectID objID = null, TimeSpan? waitTimeLimit = null, bool failOnWaitTimeLimitReached = false)
                : base(objID)
            {
                WaitTimeLimit = waitTimeLimit;
                FailOnWaitTimeLimitReached = failOnWaitTimeLimitReached;
            }

            public TimeSpan? WaitTimeLimit { get; private set; }
            public bool FailOnWaitTimeLimitReached { get; private set; }
        }

        public class SyncPublication : ObjIDBase
        {
            public SyncPublication(E039ObjectID objID = null) : base(objID) { }
        }

        public class SyncExternal : E039UpdateItem
        {
            /// <summary>Normal constructor.</summary>
            /// <param name="syncPublicationFirst">When true this causes the part to run a SyncPublication operation before creating and running the external sync action.  Defaults to true.</param>
            /// <param name="waitTimeLimit">
            /// Used to define the time limit that the table updater uses to determine how long it will block for any given external sync action to complete.
            /// When this value is null it will be replaced with the ExternalSyncTimeLimit value from the table update parts configuration.
            /// When the resulting value is null or zero, the table update part will wait indifinitly for each external sync action to complete.
            /// Defaults to null.
            /// </param>
            /// <param name="failOnWaitTimeLimitReached">When true, the corresponding Update action will fail if the underlying external sync action time limit was reached.  Defaults to false.</param>
            /// <param name="failIfExternalSyncFactoryIsNotOnline">When true, the corresponding Update actin will fail if there is an external sync factory object, but it is not currently online.  Defaults to true.</param>
            /// <param name="failIfNoExternalSyncFactoryDefined">When true, the corresponding Update action will fail if there is no configured external sync factory object.  Defaults to false.</param>
            /// <param name="requestCancelOnTimeLimitReached">When true, a running external sync action will have its cancel requested if the time limit is reached before the action completed.  Defaults to true.</param>
            public SyncExternal(bool syncPublicationFirst = true, TimeSpan? waitTimeLimit = null, bool failOnWaitTimeLimitReached = false, bool failIfExternalSyncFactoryIsNotOnline = true, bool failIfNoExternalSyncFactoryDefined = false, bool requestCancelOnTimeLimitReached = true)
            {
                SyncPublicationFirst = syncPublicationFirst;
                WaitTimeLimit = waitTimeLimit;
                FailOnWaitTimeLimitReached = failOnWaitTimeLimitReached;
                FailIfExternalSyncFactoryIsNotOnline = failIfExternalSyncFactoryIsNotOnline;
                FailIfNoExternalSyncFactoryDefined = failIfNoExternalSyncFactoryDefined;
                RequestCancelOnTimeLimitReached = requestCancelOnTimeLimitReached;
            }

            /// <summary>When true this causes the part to run a SyncPublication operation before creating and running the external sync action.  Defaults to true.</summary>
            public bool SyncPublicationFirst { get; private set; }

            /// <summary>
            /// Used to define the time limit that the table updater uses to determine how long it will block for any given external sync action to complete.
            /// When this value is null it will be replaced with the ExternalSyncTimeLimit value from the table update parts configuration.
            /// When the resulting value is null or zero, the table update part will wait indifinitly for each external sync action to complete.
            /// Defaults to null.
            /// </summary>
            public TimeSpan? WaitTimeLimit { get; private set; }

            /// <summary>When true, the corresponding Update action will fail if the underlying external sync action time limit was reached.  Defaults to false.</summary>
            public bool FailOnWaitTimeLimitReached { get; private set; }

            /// <summary>When true, the corresponding Update actin will fail if there is an external sync factory object, but it is not currently online.  Defaults to true.</summary>
            public bool FailIfExternalSyncFactoryIsNotOnline { get; private set; }

            /// <summary>When true, the corresponding Update action will fail if there is no configured external sync factory object.  Defaults to false.</summary>
            public bool FailIfNoExternalSyncFactoryDefined { get; private set; }

            /// <summary>When true, a running external sync action will have its cancel requested if the time limit is reached before the action completed.  Defaults to true.</summary>
            public bool RequestCancelOnTimeLimitReached { get; private set; }
        }

        public class LinkBase : E039UpdateItem
        {
            public LinkBase(E039Link link) { Link = link; }

            public E039Link Link { get; private set; }

            public override string ToString()
            {
                return "{0}".CheckedFormat(Link);
            }
        }

        public class ObjIDAndAttributeBase : ObjIDBase
        {
            protected ObjIDAndAttributeBase(E039ObjectID objID, INamedValueSet attributes) 
                : base(objID)
            {
                Attributes = attributes.ConvertToReadOnly();
            }

            protected ObjIDAndAttributeBase(IEnumerable<E039ObjectID> objIDSet, INamedValueSet attributes)
                : base(objIDSet)
            {
                Attributes = attributes.ConvertToReadOnly();
            }

            public INamedValueSet Attributes { get; private set; }

            public override string ToString()
            {
                return "{0} {1}".CheckedFormat(base.ToString(), Attributes.SafeToStringSML());
            }
        }

        public class ObjIDBase : E039UpdateItem
        {
            /// <summary>
            /// Single objID constructor
            /// </summary>
            protected ObjIDBase(E039ObjectID objID) 
            { 
                ObjID = objID ?? E039ObjectID.Empty;
                ObjIDSet = null;
            }

            /// <summary>
            /// ObjIDSet constructor
            /// </summary>
            protected ObjIDBase(IEnumerable<E039ObjectID> objIDSet)
            {
                ObjID = E039ObjectID.Empty;
                ObjIDSet = objIDSet.ConvertToReadOnly();
            }

            /// <summary>
            /// When non-null and non-empty, this property defines the E039ObjectID of the E039Object instance that this item applies to.
            /// </summary>
            public E039ObjectID ObjID { get; private set; }

            /// <summary>
            /// When non-null, this property defines the set of E039ObjectIDs for the set of E039Object instances that this item will be applied to.  
            /// Use of this property is mutually exclusive with the use of the base class's ObjID property.
            /// <para/>Note: not all derived types support use of this property.
            /// </summary>
            public ReadOnlyIList<E039ObjectID> ObjIDSet { get; private set; }

            /// <summary>
            /// Debugging and logging helper.
            /// </summary>
            public override string ToString()
            {
                if (ObjIDSet == null)
                    return "{0}".CheckedFormat(ObjID.ToString(E039ToStringSelect.FullName));
                else
                    return "[{0}]".CheckedFormat(String.Join(",", ObjIDSet.Select(objID => objID.ToString(E039ToStringSelect.FullName))));
            }
        }
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// List{E039UpdateItem} helper method.  Adds a SetAttribute item to the given <paramref name="updateList"/> using the given parameters.  
        /// If <paramref name="objID"/> is null and <paramref name="obj"/> is non-null then the E039ObjectID used is obtained from the given <paramref name="obj"/>'s ID.
        /// </summary>
        public static List<E039UpdateItem> AddSetAttributesItem(this List<E039UpdateItem> updateList, E039ObjectID objID = null, IE039Object obj = null, INamedValueSet attributes = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
        {
            IE039Object getObjIDFromObj = obj;
            objID = objID ?? ((getObjIDFromObj != null) ? getObjIDFromObj.ID : E039ObjectID.Empty);

            updateList.Add(new E039UpdateItem.SetAttributes(objID: objID, attributes: attributes, mergeBehavior: mergeBehavior));

            return updateList;
        }
    }

    #endregion

    #region IE039ExternalSyncFactory

    /// <summary>
    /// The IE039TableUpdater uses this interface to support the use of the E039TableUpdateItem.ExternalSync object.
    /// </summary>
    public interface IE039ExternalSyncFactory : IPartBase
    {
        IClientFacet Sync();
    }
    
    #endregion

    #region E039ToStringSelect

    /// <summary>
    /// None (0x00), FullName (0x01), UUID (0x02), Attributes (0x04), LinkedShortIDs (0x10), LinkedFullIDs (0x20)
    /// </summary>
    [Flags]
    public enum E039ToStringSelect : int
    {
        /// <summary>Placeholder default value [0x00]</summary>
        None = 0x00,
        /// <summary>Output of an object ID should include the ID's FullName.  If this options is not selected then it shall just include the ID's Name but not its Type. [0x01]</summary>
        FullName = 0x01,
        /// <summary>Output of an object ID should include the ID's UUID if it has one assigned [0x02]</summary>
        UUID = 0x02,
        /// <summary>Output of an object should include its attributes [0x04]</summary>
        Attributes = 0x04,
        /// <summary>Output of links should include just the Name of the link endpoints [0x10]</summary>
        LinkedShortIDs = 0x10,
        /// <summary>Output of links should include the FullName's of each of the link endpoints [0x20]</summary>
        LinkedFullIDs = 0x20,

        /// <summary>FullName | UUID [0x03]</summary>
        DefaultObjIDSelect = (FullName | UUID),

        /// <summary>DefaultObjIDSelect(FullName | UUID) | Attributes | LinkedShortIDs [0x17]</summary>
        DefaultObjSelect = (DefaultObjIDSelect | Attributes | LinkedShortIDs),
    }

    #endregion

    #region E039 object types and interfaces: E039ObjectID, IE039Object, E039Link, E039ObjectFlags, and related extension methods

    /// <summary>
    /// This class is used to represent the immutable "identity" of an E039 object.  This object is immutable to all external client code
    /// as all of its public properties have only private setters, or no setter at all.
    /// Namely this is the object's Type and Name.  This identity may also include a UUID.
    /// This object also provides a "FullName" property which gives a string of the form "Type:Name".  This property is generally used for
    /// logging and as a dictionary key for dictionaries that index E039Object's by their type and name.
    /// All E039Object instances that stem from a common table entry shall make use of the same E039ObjectID instance.
    /// The UUID is intended support being persisted so that objects with a long lifetime can be reliably identified in relation to other 
    /// objects that are repeatedly created and removed but which share the same name and type as previously used objects.
    /// This object supports serialization and deserialization, however the E039Object explicitly serializes its own Name and UUID as it is
    /// generally serialized in a wrapper container that already knows the type of all of the E039Objects that are to be so serialized.
    /// As such the E039Object does not directly serialize its ID using this object's serialized output.
    /// </summary>
    [DataContract(Namespace=Constants.E039NameSpace, Name="ObjectID")]
    public class E039ObjectID : IEquatable<E039ObjectID>
    {
        /// <summary>
        /// public Default constructor.  Constructs an empty E039ObjectID.  
        /// The resulting object's Name, Type, FullName, and UUID will return empty strings.
        /// Table will be assigned to be null.
        /// </summary>
        public E039ObjectID() 
        {}

        /// <summary>
        /// public explicit constructor.  
        /// Allows caller to specify the <paramref name="name"/> and <paramref name="type"/> to be used.  
        /// If the optional <paramref name="assignUUID"/> parameter is explicitly set to true then this constructor will generate and retain a new non-empty UUID.
        /// </summary>
        public E039ObjectID(string name, string type, bool assignUUID = false, IE039TableObserver tableObserver = null)
            : this(name, type, assignUUID ? Guid.NewGuid().ToString() : null, tableObserver)
        {}

        /// <summary>
        /// public explicit constructor.  
        /// Allows caller to specify the <paramref name="name"/> and <paramref name="type"/> to be used and the <paramref name="tableObserver"/> to be saved for later GetPublisher calls
        /// Using this constructor will not assign a uuid.
        /// </summary>
        public E039ObjectID(string name, string type, IE039TableObserver tableObserver)
            : this(name, type, false, tableObserver)
        { }

        /// <summary>Custom constructor for use by E039 internals.  Gives caller access to assign all of the critical properties of an E039ObjectID</summary>
        internal E039ObjectID(string name, string type, string uuid, IE039TableObserver tableObserver)
        {
            Name = name;
            Type = type;
            FullName = GenerateFullName(type, name);
            UUID = uuid;
            TableObserver = tableObserver;
        }

        /// <summary>
        /// pseudo Copy constructor used by the E039 internals.  
        /// Makes a copy of the given other and allows the caller to replace the table being used
        /// </summary>
        internal E039ObjectID(E039ObjectID other, IE039TableObserver tableObserver)
        {
            _name = other._name;
            _type = other._type;
            FullName = other.FullName;
            _uuid = other._uuid;

            TableObserver = tableObserver;
        }

        /// <summary>Returns the string Name for this E039ObjectID</summary>
        public string Name { get { return _name.MapNullToEmpty(); } private set { _name = value.MapNullOrEmptyTo(null); } }

        /// <summary>Returns the string Type for this E039ObjectID</summary>
        public string Type { get { return _type.MapNullToEmpty(); } private set { _type = value.MapNullOrEmptyTo(null); } }

        /// <summary>Returns the full name of the object as "Type:Name"</summary>
        public string FullName { get; private set; }

        /// <summary>Returns the UUID assigned to this object (if any).  Generally this is only included in an E039ObjectID obtained from a table or which is being used to Add an object with a predefined UUID.</summary>
        public string UUID { get { return _uuid.MapNullToEmpty(); } private set { _uuid = value.MapNullOrEmptyTo(null); } }

        /// <summary>Returns the TableObserver instance which created this E039ObjectID (and thus contains the corresponding object) if non-null.  For client constructed E039ObjectID instances, this property will be null.</summary>
        public IE039TableObserver TableObserver { get; private set; }

        [DataMember(Name = "Name", IsRequired = false, EmitDefaultValue = false)]
        private string _name;

        [DataMember(Name = "Type", IsRequired = false, EmitDefaultValue = false)]
        private string _type;

        [DataMember(Name = "UUID", IsRequired = false, EmitDefaultValue = false)]
        private string _uuid;

        public bool IsEmpty { get { return (_name == null) && (_type == null) && FullName.IsNullOrEmpty() && (_uuid == null); } }

        [OnDeserialized]
        void OnDeserialized(StreamingContext sc)
        {
            FullName = GenerateFullName(Type, Name);
        }

        /// <summary>If either the given <paramref name="type"/> or the given <paramref name="name"/> are non-null and non-empty then this method returns the string <paramref name="type"/>:<paramref name="name"/>.  Otherwise it return the empty string.</summary>
        public static string GenerateFullName(string type, string name)
        {
            return (!name.IsNullOrEmpty() || !type.IsNullOrEmpty()) ? "{0}:{1}".CheckedFormat(type, name) : string.Empty;
        }

        /// <summary>Returns the reference Empty E039ObjectID</summary>
        public static E039ObjectID Empty { get { return _empty; } }
        private static readonly E039ObjectID _empty = new E039ObjectID() { };

        /// <summary>
        /// IEquatable{E039ObjectID} implementation method.  
        /// Returns true if the objects have the same Name and Type 
        /// and they either have the same UUID or at least one of them has a null or empty UUID
        /// and they either have the same Table or at least one of them's Table property is null.
        /// </summary>
        public bool Equals(E039ObjectID other)
        {
            // if the stored Name or Type do not match then they are not equal
            if (_name != other._name || _type != other._type)
                return false;

            // if both IDs include different non-empty UUIDs then return false
            if (_uuid != null && other._uuid != null && _uuid != other._uuid)
                return false;

            // if both stored Tables are non-null and they are not the same reference then return false
            if (TableObserver != null && other.TableObserver != null && !Object.ReferenceEquals(TableObserver, other.TableObserver))
                return false;

            // all tested values are equal
            return true;
        }

        /// <summary>Debugging and Logging helper method.  returns ToString(DefaultObjIDSelect = FullName | UUID)</summary>
        public override string ToString()
        {
            return ToString(E039ToStringSelect.DefaultObjIDSelect);
        }

        /// <summary>
        /// Adjustable ToString variant accepts <paramref name="toStringSelect"/> to indicate which flavor of ToString will be used.
        /// FullName includes the FullName, otherwise just the Name is included.  
        /// If UUID is included and this ID includes a UUID then the uuid will be appended to the resulting string.
        /// </summary>
        public string ToString(E039ToStringSelect toStringSelect)
        {
            StringBuilder sb = new StringBuilder();
            if (IsEmpty)
                sb.Append("[Empty]");
            else
            {
                if (toStringSelect.IsSet(E039ToStringSelect.FullName))
                    sb.Append(FullName);
                else
                    sb.Append(Name);

                if (toStringSelect.IsSet(E039ToStringSelect.UUID) && (_uuid != null))
                    sb.CheckedAppendFormat(" uuid:{0}", _uuid);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns true if the Type and Name are non-empty.  The UUID may be empty and Table may be null.
        /// </summary>
        public bool IsValid { get { return !Type.IsNullOrEmpty() && !Name.IsNullOrEmpty(); } }
    }

    /// <summary>
    /// (read only) Interface supported by published E039Object instances.
    /// </summary>
    public interface IE039Object : IEquatable<IE039Object>
    {
        /// <summary>Gives the object ID (Name and Type) of this object</summary>
        E039ObjectID ID { get; }

        /// <summary>Gives the flag values that are associated with this object</summary>
        E039ObjectFlags Flags { get; }

        /// <summary>Gives all of the attribute values that result from initial values combined with the accumulated set of Update calls that have been applied to this object's attribute values.</summary>
        INamedValueSet Attributes { get; }

        /// <summary>Gives the current set of E039Link objects that link from this object</summary>
        IList<E039Link> LinksToOtherObjectsList { get; }

        /// <summary>Gives the current set of E039Link objects that link to this object</summary>
        IList<E039Link> LinksFromOtherObjectsList { get; }

        /// <summary>Returns true if this object is equal to the Empty object.</summary>
        bool IsEmpty { get; }

        /// <summary>Variant of normal ToString method that supports selectable output using the <paramref name="toStringSelect"/> parameter</summary>
        string ToString(E039ToStringSelect toStringSelect);
    }

    /// <summary>
    /// This struct contains the information required to specify a directional relationship between two objects.  
    /// <para/>Note: once a Link Type and InstanceNum has been added to an objects LinksToOtherObjects set, unlinking will simply set the ToID to be Empty(null) rather than changing the array length.
    /// </summary>
    [DataContract(Namespace = Constants.E039NameSpace, Name = "Link")]
    public struct E039Link : IEquatable<E039Link>
    {
        /// <summary>Simply returns a default E039Link.  Provided for consistancy with other E039 related object types</summary>
        public static E039Link Empty { get { return default(E039Link); } }

        public E039Link(E039ObjectID fromID, E039ObjectID toID, string key)
            : this()
        {
            FromID = fromID;
            Key = key.MapNullOrEmptyTo(@"@_$ProxyForNullOrEmptyKey$_@");
            ToID = toID;
        }

        public E039Link(IE039Object fromObj, IE039Object toObj, string key)
            : this(fromObj.ID, toObj.ID, key)
        { }

        public E039Link(E039Link other)
            : this(other.FromID, other.ToID, other.Key)
        { }

        public E039Link(E039ObjectID fromID, string key)
            : this(fromID, E039ObjectID.Empty, key)
        { }

        public E039Link SetupForSerialization(bool forRemoteUse = false)
        {
            E039Link setupTarget = this;

            var fromID = setupTarget.FromID;
            var toID = setupTarget.ToID;

            setupTarget.fromName = (forRemoteUse) ? fromID.Name : null;
            setupTarget.fromType = (forRemoteUse) ? fromID.Type : null;
            setupTarget.fromUUID = (forRemoteUse) ? fromID.UUID.MapEmptyToNull() : null;
            setupTarget.toName = toID.Name;
            setupTarget.toType = toID.Type;
            setupTarget.toUUID = (forRemoteUse) ? toID.UUID.MapEmptyToNull() : null;

            return setupTarget;
        }

        /// <summary>Identifies the object that is the source of the link (the "from" end), or E039ObjectID.Empty if the source of the link has not been set to a valid non-null value.</summary>
        public E039ObjectID FromID { get { return _fromID ?? E039ObjectID.Empty; } set { _fromID = value; } }

        // We are currently not peristing the FromID since it is known by context in a serialized object body.
        private E039ObjectID _fromID;

        [DataMember(Name = "FromName", Order = 10, IsRequired = false, EmitDefaultValue = false)]
        private string fromName;

        [DataMember(Name = "FromType", Order = 20, IsRequired = false, EmitDefaultValue = false)]
        private string fromType;

        [DataMember(Name = "FromUUID", Order = 30, IsRequired = false, EmitDefaultValue = false)]
        private string fromUUID;

        /// <summary>identifies the Key that is used to identify this "type" of the link (client specified terminology)</summary>
        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        public string Key { get; set; }

        /// <summary>identifies the object that is the target of the link (the "to" end), or E039ObjectID.Empty if the target of the link has not been set to a valid non-null value.</summary>
        public E039ObjectID ToID { get { return _toID ?? E039ObjectID.Empty; } set { _toID = value; } }

        private E039ObjectID _toID;

        [DataMember(Name = "ToName", Order = 300, IsRequired = false, EmitDefaultValue = false)]
        private string toName;

        [DataMember(Name = "ToType", Order = 400, IsRequired = false, EmitDefaultValue = false)]
        private string toType;

        [DataMember(Name = "ToUUID", Order = 500, IsRequired = false, EmitDefaultValue = false)]
        private string toUUID;

        [OnDeserialized]
        void OnDeserialized(StreamingContext sc)
        {
            if (!fromName.IsNullOrEmpty() || !fromType.IsNullOrEmpty() || !fromUUID.IsNullOrEmpty())
                _fromID = new E039ObjectID(fromName, fromType, uuid: fromUUID, tableObserver: null);

            if (!toName.IsNullOrEmpty() || !toType.IsNullOrEmpty() || !toUUID.IsNullOrEmpty())
                _toID = new E039ObjectID(toName, toType, uuid: toUUID, tableObserver: null);
        }

        /// <summary>Returns true if the FromID, ToID, and Key are empty.</summary>
        public bool IsEmpty { get { return (FromID.IsEmpty && Key.IsNullOrEmpty() && ToID.IsEmpty); } }

        /// <summary>Returns true if the given ToID is empty</summary>
        public bool IsToIDEmpty { get { return ToID.IsEmpty; } }

        /// <summary>Returns true if the contents of this link object are equal to the contents of the given <paramref name="other"/> link object</summary>
        public bool Equals(E039Link other)
        {
            return (FromID.Equals(other.FromID)
                    && Key == other.Key
                    && ToID.Equals(other.ToID)
                    );
        }

        public string FullIDAndKeyStr
        {
            get { return "{0}_{1}".CheckedFormat(FromID.FullName, Key); }
        }

        public override string ToString()
        {
            return ToString(E039ToStringSelect.FullName);
        }

        public string ToString(E039ToStringSelect toStringSelect)
        {
            if (IsEmpty)
                return "[Empty]";

            return "Link from:{0} key:{1} to:{2}".CheckedFormat(FromID.ToString(toStringSelect), Key, ToID.ToString(toStringSelect));
        }

        public static E039LinkFilter FilterAll { get { return _filterAll; } }
        public static E039LinkFilter FilterNone { get { return _filterNone; } }
        private static readonly E039LinkFilter _filterAll = (link) => true;
        private static readonly E039LinkFilter _filterNone = (link) => false;
    }

    /// <summary>
    /// Object instance and use flags.
    /// <para/>None (0x0000), Pinned (0x0010), CreateIVA (0x0100), IsFinal (0x1000), ClientUsableFlags (0x0110)
    /// </summary>
    [DataContract(Namespace = Constants.E039NameSpace)]
    [Flags]
    public enum E039ObjectFlags : int
    {
        /// <summary>PlaceHolder default value [0x0000]</summary>
        [EnumMember]
        None = 0x00,

        /// <summary>Client usable: Flag indicates that the object shall not be removed as a side effect of a final link to it being removed [0x0010]</summary>
        [EnumMember]
        Pinned = 0x10,

        /// <summary>Client usable: Flag indicates that the client would like the table manager to create an IVA for this object [0x0100]</summary>
        [EnumMember]
        CreateIVA = 0x100,

        /// <summary>Flag indicates that the table manager has published this as a final value.  This is done immediately before the object is removed from the table [0x1000]</summary>
        [EnumMember]
        IsFinal = 0x1000,

        /// <summary>Gives the set of client usable flags (Pinned | CreateIVA) [0x0110]</summary>
        ClientUsableFlags = (E039ObjectFlags.Pinned | E039ObjectFlags.CreateIVA),
    }

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Returns true if the given <paramref name="flags"/> value has the IsFinal bit set.
        /// </summary>
        public static bool IsFinal(this E039ObjectFlags flags) { return flags.IsSet(E039ObjectFlags.IsFinal); }

        /// <summary>
        /// Returns true if the given <paramref name="obj"/> is non-null and its Flags property has the IsFinal bit set 
        /// to indicate that this is the final value that will be published to the corresonding publisher, 
        /// generally because the object is being removed from the table.
        /// </summary>
        public static bool IsFinal(this IE039Object obj) 
        { 
            return (obj != null) && obj.Flags.IsFinal(); 
        }

        /// <summary>
        /// Attempts to obtain, and return, the object identified by the given <paramref name="objID"/> using the TableObserver that the <paramref name="objID"/> references.
        /// <para/>Note:  This EM is only suitable for use with <paramref name="objID"/> instances that have been obtained from a TableObserver (or a publisher that it created)
        /// or where the id's was constructed with a reference to the appropriate tableObserver for which the id is expected to be known.
        /// <para/>If the given <paramref name="objID"/>'s TableObserver property is null or if no matching object is found in the given table then this method returns the <paramref name="fallbackValue"/>.
        /// </summary>
        public static IE039Object GetObject(this E039ObjectID objID, IE039Object fallbackValue = null)
        {
            IE039TableObserver tableObserver = ((objID != null) ? objID.TableObserver : null);

            if (tableObserver != null)
            {
                INotificationObject<IE039Object> publisher = tableObserver.GetPublisher(objID);
                if (publisher != null)
                    return publisher.Object ?? fallbackValue;
            }

            return fallbackValue;
        }

        /// <summary>
        /// Attempts to obtain, and return, the object publisher for the object identified by the given <paramref name="objID"/> using the TableObserver that the <paramref name="objID"/> references.
        /// <para/>Note:  This EM is only suitable for use with <paramref name="objID"/> instances that have been obtained from a TableObserver (or a publisher that it created)
        /// or where the id's was constructed with a reference to the appropriate tableObserver for which the id is expected to be known.
        /// <para/>If the given <paramref name="objID"/>'s TableObserver property is null or if no matching object is found in the given table then this method returns the <paramref name="fallbackValue"/>.
        /// </summary>
        public static INotificationObject<IE039Object> GetPublisher(this E039ObjectID objID, INotificationObject<IE039Object> fallbackValue = null)
        {
            IE039TableObserver tableObserver = ((objID != null) ? objID.TableObserver : null);

            if (tableObserver != null)
            {
                INotificationObject<IE039Object> publisher = tableObserver.GetPublisher(objID);
                if (publisher != null)
                    return publisher;
            }

            return fallbackValue;
        }

        /// <summary>
        /// Returns true if the given <paramref name="obj"/> is null or if it IsEmpty
        /// </summary>
        public static bool IsNullOrEmpty(this IE039Object obj)
        {
            return ((obj == null) || obj.IsEmpty);
        }

        /// <summary>
        /// If the given <paramref name="obj"/> param is passed as null then this method returns E039Object.Empty, otherwise it returns the given <paramref name="obj"/> value.
        /// </summary>
        public static IE039Object MapNullToEmpty(this IE039Object obj)
        {
            return ((obj != null) ? obj : E039Object.Empty);
        }

        /// <summary>
        /// If the given <paramref name="obj"/> is non-null and IsEmpty then this method returns null, otherwise it returns the given <paramref name="obj"/> value.
        /// </summary>
        public static IE039Object MapEmptyToNull(this IE039Object obj)
        {
            return ((obj != null) && obj.IsEmpty ? null : obj);
        }

        /// <summary>
        /// Returns true if the given <paramref name="objID"/> is null or if it IsEmpty
        /// </summary>
        public static bool IsNullOrEmpty(this E039ObjectID objID)
        {
            return ((objID == null) || objID.IsEmpty);
        }

        /// <summary>
        /// If the given <paramref name="objID"/> param is passed as null then this method returns E039ObjectID.Empty otherwise it returns the value of <paramref name="objID"/>
        /// </summary>
        public static E039ObjectID MapNullToEmpty(this E039ObjectID objID)
        {
            return ((objID != null) ? objID : E039ObjectID.Empty);
        }

        /// <summary>
        /// If the given <paramref name="objID"/> is non-null and IsEmpty then this method returns null, otherwise it returns the given <paramref name="objID"/> value.
        /// </summary>
        public static E039ObjectID MapEmptyToNull(this E039ObjectID objID)
        {
            return ((objID != null) && objID.IsEmpty ? null : objID);
        }

        /// <summary>
        /// Returns the first link with the given <paramref name="linkKey"/> key from the given <paramref name="obj"/>'s LinksToOtherObjectsList
        /// </summary>
        public static E039Link FindFirstLinkTo(this IE039Object obj, string linkKey)
        {
            if (obj != null)
                return obj.LinksToOtherObjectsList.FirstOrDefault(link => link.Key == linkKey);
            else
                return default(E039Link);
        }

        /// <summary>
        /// Returns the set of links with the given <paramref name="linkKey"/> key from the given <paramref name="obj"/>'s LinksToOtherObjectsList
        /// </summary>
        public static IEnumerable<E039Link> FindLinksTo(this IE039Object obj, string linkKey)
        {
            if (obj != null)
                return obj.LinksToOtherObjectsList.Where(link => link.Key == linkKey);
            else
                return Utils.Collections.EmptyArrayFactory<E039Link>.Instance;
        }

        /// <summary>
        /// Returns the first link with the given <paramref name="linkKey"/> key from the given <paramref name="obj"/>'s LinksFromOtherObjectsList
        /// </summary>
        public static E039Link FindFirstLinkFrom(this IE039Object obj, string linkKey)
        {
            if (obj != null)
                return obj.LinksFromOtherObjectsList.FirstOrDefault(link => link.Key == linkKey);
            else
                return default(E039Link);
        }

        /// <summary>
        /// Returns the set of links with the given <paramref name="linkKey"/> key from the given <paramref name="obj"/>'s LinksFromOtherObjectsList
        /// </summary>
        public static IEnumerable<E039Link> FindLinksFrom(this IE039Object obj, string linkKey)
        {
            if (obj != null)
                return obj.LinksFromOtherObjectsList.Where(link => link.Key == linkKey);
            else
                return Utils.Collections.EmptyArrayFactory<E039Link>.Instance;
        }
    }

    #endregion

    #region E039Object (for publication and internal use)

    /// <summary>
    /// primary IE039Object implementation class.  Also used for (most?) E039 related serialization purposes.
    /// This object supports mutability within this assembly and is immutable for all code outside of this assembly (property setters are defined to be internal)
    /// </summary>
    [DataContract(Namespace = Constants.E039NameSpace, Name = "ObjInst")]
    public class E039Object : IE039Object
    {
        /// <summary>Returns an empty IE039Object instance</summary>
        public static IE039Object Empty { get { return _empty; } }
        private static readonly IE039Object _empty = new E039Object();

        /// <summary>
        /// Default constructor - generates an empty object.  Recommend using E039Object.Empty singleton instance in place of this where possible.
        /// </summary>
        public E039Object()
        {
            ID = E039ObjectID.Empty;
        }

        /// <summary>
        /// "Copy" constructor.  Includes optional parameters that may be used to modify the copy in specific ways.
        /// </summary>
        public E039Object(IE039Object other, E039ObjectID alternateID = null, bool serializeForRemoteUse = false)
        {
            ID = alternateID ?? other.ID;

            if (serializeForRemoteUse)
                Type = ID.Type;

            Flags = other.Flags;
            Attributes = other.Attributes;

            LinksFromOtherObjectsList = other.LinksFromOtherObjectsList;
            LinksToOtherObjectsList = other.LinksToOtherObjectsList;
            SerializeForRemoteUse = serializeForRemoteUse;
        }

        public E039Object(E039ObjectID id, E039ObjectFlags flags, INamedValueSet attributes, IList<E039Link> linksToOtherObjectsList = null, IList<E039Link> linksFromOtherObjectsList = null)
        {
            ID = id;
            Flags = flags;
            Attributes = attributes;

            LinksToOtherObjectsList = linksToOtherObjectsList;
            LinksFromOtherObjectsList = linksFromOtherObjectsList;
        }

        /// <summary>
        /// Gives the object ID (Name and Type) of this object.
        /// For deserialized objects this property getter will return a valid value if the object was serialized with a non-null Type or null if it was not (the default).
        /// To prompt an object to populate this property getter on deserialize, the object must be obtained using a copy constructor with the serializeWithType parameter set to true.
        /// </summary>
        public E039ObjectID ID
        {
            get { return _id; }
            internal set
            {
                _id = value;
                Name = _id.Name.MapEmptyTo();
                UUID = _id.UUID.MapEmptyTo();
            }
        }
        private E039ObjectID _id;

        private bool SerializeForRemoteUse { get; set; }

        [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
        internal string Name { get; set; }

        [DataMember(Order = 110, IsRequired = false, EmitDefaultValue = false)]
        internal string UUID { get; set; }

        [DataMember(Order = 120, IsRequired = false, EmitDefaultValue = false)]
        internal string Type { get; set; }

        /// <summary>Gives the flag values that are associated with this object</summary>
        public E039ObjectFlags Flags { get; internal set; }

        [DataMember(Order = 200, Name = "Flags", IsRequired = false, EmitDefaultValue = false)]
        private string FlagsSerializationHelper { get { return Flags.ToString(); } set { Flags = value.TryParse<E039ObjectFlags>(); } }

        /// <summary>
        /// Gives all of the attribute values that result from initial values combined with the accumulated set of Update calls that have been applied to this object's attribute values.  
        /// This property is used for serialization/deserialization.
        /// </summary>
        public INamedValueSet Attributes { get { return _attributes ?? NamedValueSet.Empty; } internal set { _attributes = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        [DataMember(Order = 300, Name = "Attribs", IsRequired = false, EmitDefaultValue = false)]
        private NamedValueSet _attributes = null;

        /// <summary>Gives the current set of E039Link objects that link from this object</summary>
        public IList<E039Link> LinksToOtherObjectsList { get { return _linksToOtherObjectsList ?? _emptyLinkList; } internal set { _linksToOtherObjectsList = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        /// <summary>Gives the current set of E039Link objects that link to this object</summary>
        public IList<E039Link> LinksFromOtherObjectsList { get { return _linksFromOtherObjectsList ?? _emptyLinkList; } internal set { _linksFromOtherObjectsList = value.MapEmptyToNull().ConvertToReadOnly(mapNullToEmpty: false); } }

        private IList<E039Link> _linksToOtherObjectsList = null;
        private IList<E039Link> _linksFromOtherObjectsList = null;

        [DataMember(Name = "LinksOut", Order = 400, IsRequired = false, EmitDefaultValue = false)]
        private List<E039Link> SerializationHelperForLinksToOtherObjectsList 
        { 
            get { return ((_linksToOtherObjectsList != null) ? new List<E039Link>(_linksToOtherObjectsList.Select(link => link.SetupForSerialization(forRemoteUse: SerializeForRemoteUse))) : null); } 
            set { _linksToOtherObjectsList = value.ConvertToReadOnly(mapNullToEmpty: false); } 
        }

        [DataMember(Name = "LinksIn", Order = 500, IsRequired = false, EmitDefaultValue = false)]
        private List<E039Link> SerializationHelperForLinksFromOtherObjectsList
        {
            get { return ((SerializeForRemoteUse && (_linksFromOtherObjectsList != null)) ? new List<E039Link>(_linksFromOtherObjectsList.Select(link => link.SetupForSerialization(forRemoteUse: SerializeForRemoteUse))) : null); }
            set { _linksFromOtherObjectsList = value.ConvertToReadOnly(mapNullToEmpty: false); }
        }

        private static readonly IList<E039Link> _emptyLinkList = ReadOnlyIList<E039Link>.Empty;

        /// <summary>Returns true if this object is equal to the Empty object.</summary>
        public bool IsEmpty { get { return this.Equals(Empty); } }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc)
        {
            if (!Type.IsNullOrEmpty())
                _id = new E039ObjectID(Name, Type, UUID, tableObserver: null);

            if (_attributes != null)
                _attributes.MakeReadOnly();
        }

        /// <summary>
        /// Returns true if this object's ID, Flags, Attributes, LinksToOtherObjectsList and LinksFromOtherObjectsList are all Equal to the <paramref name="other"/>'s
        /// </summary>
        public bool Equals(IE039Object other)
        {
            return (other != null
                    && ID.Equals(other.ID)
                    && Flags == other.Flags
                    && Attributes.MapNullToEmpty().Equals(other.Attributes.MapNullToEmpty())
                    && LinksToOtherObjectsList.IsEqualTo(other.LinksToOtherObjectsList)
                    && LinksFromOtherObjectsList.IsEqualTo(other.LinksFromOtherObjectsList)
                );
        }

        /// <summary>Debug and Logging helper.  Returns ToString(E039ToStringSelect.DefaultObjSelect) [DefaultObjIDSelect(FullName | UUID) | Attributes | LinkedShortIDs]</summary>
        public override string ToString()
        {
            return ToString(E039ToStringSelect.DefaultObjSelect);
        }

        /// <summary>Variant of normal ToString method that supports selectable output using the <paramref name="toStringSelect"/> parameter</summary>
        public string ToString(E039ToStringSelect toStringSelect)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(ID.ToString(toStringSelect));

            bool linkShortIDs = toStringSelect.IsSet(E039ToStringSelect.LinkedShortIDs);
            E039ToStringSelect linkIDToStringSelect = linkShortIDs ? E039ToStringSelect.None : E039ToStringSelect.FullName;

            if (linkShortIDs || toStringSelect.IsSet(E039ToStringSelect.LinkedFullIDs))
            {
                if (_linksToOtherObjectsList != null)
                    sb.CheckedAppendFormat(" to:[{0}]", String.Join(",", _linksToOtherObjectsList.Select(link => link.ToString(linkIDToStringSelect)).ToArray()));

                if (_linksFromOtherObjectsList != null)
                    sb.CheckedAppendFormat(" from:[{0}]", String.Join(",", _linksFromOtherObjectsList.Select(link => link.ToString(linkIDToStringSelect)).ToArray()));
            }

            if (toStringSelect.IsSet(E039ToStringSelect.Attributes))
                sb.CheckedAppendFormat(" attribs:{0}", Attributes);

            return sb.ToString();
        }
    }

    #endregion

    #region E039BasicTable and E039BasicTableConfig

    public class E039BasicTablePartConfig
    {
        public E039BasicTablePartConfig(string partID)
        {
            PartID = partID;
            ObjectIVAPrefix = "{0}.".CheckedFormat(partID);

            ExternalSyncTimeLimit = (0.2).FromSeconds();

            this.ActionLoggingConfig = ActionLoggingConfig.Debug_Debug_Trace_Trace;
            PersistHelperActionLoggingConfig = new ActionLoggingConfig(ActionLoggingConfig.Trace_Trace_Trace_Trace, actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);
            PersistFileWrittenMesgType = Logging.MesgType.Debug;
            PersistFileWriteFailedMesgType = Logging.MesgType.Debug;
        }

        public E039BasicTablePartConfig(E039BasicTablePartConfig other, bool testPersitValues = true)
        {
            PartID = other.PartID;
            _partBaseIVI = other._partBaseIVI;
            _objectIVI = other._objectIVI;
            ObjectIVAPrefix = other.ObjectIVAPrefix;

            _isi = other._isi;

            PersistStorageAdapterFactory = other.PersistStorageAdapterFactory ?? ((PersistentObjectFileRingConfig config, Logging.IBasicLogger log) => 
                {
                    return new DataContractPersistentXmlTextFileRingStorageAdapter<E039PersistFileContents>("psa.{0}".CheckedFormat(config.FileBaseName), config)
                    {
                        SuccessEmitter = log.Debug,
                        IssueEmitter = log.Debug,
                        AutoSaveConditions = AutoSaveConditions.SuccessfulSaveMakesLoadSucceed,
                    };
                });

            TimeSpan defaultPersistWriteHoldoff = (0.1).FromSeconds();
            DefaultTypeSetSpec = new E039TableTypeSetPersistSpecItem(other.DefaultTypeSetSpec, fallbackPersistWriteHoldoff: defaultPersistWriteHoldoff, thisIsDefaultItem: true, testPersistValues: testPersitValues);

            DefaultFallbackReferenceSet = other.DefaultFallbackReferenceSet;
            DefaultFallbackReferenceHistorySet = other.DefaultFallbackReferenceHistorySet;

            defaultPersistWriteHoldoff = DefaultTypeSetSpec.PersistWriteHoldoff;

            TypeSetPersistSpecItemArray = (other.TypeSetPersistSpecItemArray ?? _emptyPersistSpecItemArray).Select(item => new E039TableTypeSetPersistSpecItem(item, fallbackPersistWriteHoldoff: defaultPersistWriteHoldoff, testPersistValues: testPersitValues)).ToArray();

            ExternalSyncFactory = other.ExternalSyncFactory;
            ExternalSyncTimeLimit = other.ExternalSyncTimeLimit;

            ActionLoggingConfig = other.ActionLoggingConfig;
            PersistHelperActionLoggingConfig = other.PersistHelperActionLoggingConfig;
            PersistFileWrittenMesgType = other.PersistFileWrittenMesgType;
            PersistFileWriteFailedMesgType = other.PersistFileWriteFailedMesgType;

            CustomActionLoggingConfigDict = other.CustomActionLoggingConfigDict;
        }

        internal void SetupForUse()
        {
            DefaultTypeSetSpec.SetupForUse();
            TypeSetPersistSpecItemArray.DoForEach(item => item.SetupForUse());
        }

        public string PartID { get; private set; }

        public IValuesInterconnection PartBaseIVI { get { return _partBaseIVI ?? _objectIVI ?? Values.Instance; } set { _partBaseIVI = value; } }
        public IValuesInterconnection ObjectIVI { get { return _objectIVI ?? _partBaseIVI ?? Values.Instance; } set { _objectIVI = value; } }
        public string ObjectIVAPrefix { get; set; }

        public ISetsInterconnection ISI { get { return _isi ?? Sets.Instance; } set { _isi = value; } }

        public Func<PersistentObjectFileRingConfig, Logging.IBasicLogger, IPersistentStorage<E039PersistFileContents>> PersistStorageAdapterFactory { get; set; }

        /// <summary>
        /// The DefaultTypeSetSpec is appended to the end of the TypeSetPersistSpecItemArray when defining the full set of type sets that the table uses.
        /// This spec is forced to use the name "DefaultPersistSet" and MatchRuleSet(MatchType.None) during cloning.
        /// </summary>
        public E039TableTypeSetPersistSpecItem DefaultTypeSetSpec { get; set; }

        /// <summary>
        /// This property may be used by the client code to give the table manager the actual set to use for all types that do not already have a reference set specified by their corresponding spec item entry.
        /// If this property is non-null and a tableset's spec item does not specify a type set specific reference set to use then the table set will use this reference set to record and the current object states.
        /// If a type's corresponding type set spec has a specified reference set then it will use that one.  If both this and the type specific spec do not specify reference sets then no reference set will be used for that type.
        /// </summary>
        public IReferenceSet<E039Object> DefaultFallbackReferenceSet { get; set; }

        /// <summary>
        /// This property may be used by the client code to give the table manager the actual set to use for all types that do not already have a reference history set specified by their corresponding spec item entry.
        /// If this property is non-null and a tableset's spec item does not specify a type set specific reference history set to use then the table set will use this reference history set to record and the current object states.
        /// </summary>
        public IReferenceSet<E039Object> DefaultFallbackReferenceHistorySet { get; set; }

        public E039TableTypeSetPersistSpecItem [] TypeSetPersistSpecItemArray { get; set; }

        /// <summary>Defines the factory object that is used to create external Sync actions which are used when the table client requests an external sync as part of a set of update itmes.</summary>
        public IE039ExternalSyncFactory ExternalSyncFactory { get; set; }

        /// <summary>Defines the fallback time limit that is to be used when a extenal sync update item does not explicitly specify the use of a specific time limit.  When both the client provided value and this value are null, or when the resulting time limit is zero, the table updater will wait indifinitly</summary>
        public TimeSpan ? ExternalSyncTimeLimit { get; set; }

        public ActionLoggingConfig ActionLoggingConfig { get; set; }
        public ActionLoggingConfig PersistHelperActionLoggingConfig { get; set; }
        public Logging.MesgType PersistFileWrittenMesgType { get; set;  }
        public Logging.MesgType PersistFileWriteFailedMesgType { get; set; }

        public ReadOnlyIDictionary<string, ActionLoggingConfig> CustomActionLoggingConfigDict { get; set; }

        private IValuesInterconnection _partBaseIVI, _objectIVI;
        private ISetsInterconnection _isi;

        private static readonly E039TableTypeSetPersistSpecItem [] _emptyPersistSpecItemArray = EmptyArrayFactory<E039TableTypeSetPersistSpecItem>.Instance;
    }

    public class E039TableTypeSetPersistSpecItem
    {
        public E039TableTypeSetPersistSpecItem(string setName = null)
        {
            SetName = setName.MapNullToEmpty();
        }

        public E039TableTypeSetPersistSpecItem(E039TableTypeSetPersistSpecItem other, TimeSpan fallbackPersistWriteHoldoff, bool thisIsDefaultItem = false, bool testPersistValues = true)
        {
            if (!thisIsDefaultItem)
            {
                SetName = other.SetName;
                TypeNameMatchRuleSet = new MatchRuleSet((other != null ? other.TypeNameMatchRuleSet : null), convertNullToAny: true);
            }
            else
            {
                SetName = "DefaultPersistSet";
                TypeNameMatchRuleSet = new MatchRuleSet(MatchType.None);
            }

            if (other != null)
            {
                if (other.PersistObjFileRingConfig != null)
                    PersistObjFileRingConfig = new PersistentObjectFileRingConfig(other.PersistObjFileRingConfig, testValues: testPersistValues);

                PersistWriteHoldoff = other.PersistWriteHoldoff.MapDefaultTo(fallbackPersistWriteHoldoff);

                ReferenceSetID = other.ReferenceSetID;
                ReferenceSetCapacity = other.ReferenceSetCapacity;
                ReferenceSet = other.ReferenceSet;
                ReferenceHistorySet = other.ReferenceHistorySet;
            }
        }

        public string SetName { get; set; }
        public MatchRuleSet TypeNameMatchRuleSet { get; set; }
        public PersistentObjectFileRingConfig PersistObjFileRingConfig { get; set; }

        /// <summary>Defines the nominal minimum period of time from a new table update to when the persist engine is told to write the changes to the table.  For non-default spec items, a zero value here will be replaced with the value from the default spec item.</summary>
        public TimeSpan PersistWriteHoldoff { get; set; }

        /// <summary>If this SetID and the related Set Capacity is non-zero then a ReferenceSet will be created and registered for this SetID and all published objects will also be added to/updated in the resulting reference set.</summary>
        public SetID ReferenceSetID { get; set; }

        /// <summary>If this Capacity is non-zero and the related SetID is non-empty then this value will define the capacity of the reference set that will be created.  This capacity must be large enough to hold all of the objects that may eventually be added to the corresponding TableTypeSet.</summary>
        public int ReferenceSetCapacity { get; set; }

        /// <summary>
        /// This method may be used to set both the ReferenceSetID and the ReferenceSetCapacity using the given <paramref name="partID"/>. 
        /// The Set name will be generated as PartID.SetName.  
        /// Other optional parameters may be used to control the generation of a UUID for the resulting SetID and to define the resulting set's capacity.
        /// </summary>
        public E039TableTypeSetPersistSpecItem CreateDefaultSetID(string partID, string uuid = null, bool generateUUIDForNull = true, int capacity = 200) 
        { 
            ReferenceSetID = new SetID("{0}.{1}".CheckedFormat(partID, SetName), uuid: uuid, generateUUIDForNull: generateUUIDForNull);
            ReferenceSetCapacity = capacity;
            return this;
        }

        /// <summary>
        /// This property may be used by the client code to give the table manager the actual set to use (as externally constructed and/or registered) rather than using cofiguration parameters
        /// to define how the table part shall create the set.
        /// </summary>
        public IReferenceSet<E039Object> ReferenceSet { get; set; }

        /// <summary>
        /// This property may be used by the client code to give the table manager the reference history set to use (as externally constructed, configured and optionally registered)
        /// </summary>
        public IReferenceSet<E039Object> ReferenceHistorySet { get; set; }

        /// <summary>
        /// This method is used by the table part to make any post clone changes that are needed before the configuration contents can be used.
        /// At present this is the place where the ReferenceSet is created if the client specified the use of one but did not give the instance to use.
        /// </summary>
        internal void SetupForUse()
        {
            if (ReferenceSet == null && !ReferenceSetID.IsNullOrEmpty() && ReferenceSetCapacity > 0)
                ReferenceSet = new ReferenceSet<E039Object>(ReferenceSetID, capacity: ReferenceSetCapacity, registerSelf: true);
        }
    }

    /// <summary>
    /// This is the default implementation object for the IE039TableObserver and IE039TableUpdator interfaces.
    /// </summary>
    public class E039BasicTablePart : SimpleActivePartBase, IE039TableObserver, IE039TableUpdater
    {
        #region Construction and related fields

        /// <summary>
        /// Constructor - most behavior features are defined by the provided <paramref name="config"/> contents.
        /// </summary>
        public E039BasicTablePart(E039BasicTablePartConfig config)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true, partBaseIVI: config.PartBaseIVI))
        {
            Config = new E039BasicTablePartConfig(config, testPersitValues: true);
            Config.SetupForUse();

            ActionLoggingReference.Config = Config.ActionLoggingConfig ?? ActionLoggingConfig.Debug_Debug_Trace_Trace;

            ObjectIVI = Config.ObjectIVI;
            ExternalSyncFactory = Config.ExternalSyncFactory;

            if (!Config.CustomActionLoggingConfigDict.IsNullOrEmpty())
            {
                customActionLoggingReferenceDict = new Dictionary<string, ActionLogging>();
                customActionLoggingReferenceDict.SafeAddRange(Config.CustomActionLoggingConfigDict.Select(kvp => KVP.Create(kvp.Key, new ActionLogging(ActionLoggingReference) { Config = kvp.Value })));
            }

            persistHelperPart = new PersistHelper("{0}.ph".CheckedFormat(PartID), Config);

            // build the configured set of typeSetTrackers and ask all that support persistence to load their fileContents (but do not process these yet).
            E039TableTypeSetPersistSpecItem [] effectiveTypeSetPersistSpecItemArray = Config.TypeSetPersistSpecItemArray.Concat(new[] { Config.DefaultTypeSetSpec }).ToArray();

            {
                List<TypeSetTracker> typeSetTrackerList = new List<TypeSetTracker>();

                foreach (var typeSetPersistSpec in effectiveTypeSetPersistSpecItemArray)
                {
                    TypeSetTracker tst = new TypeSetTracker(Config, typeSetPersistSpec, Log);
                    typeSetTrackerList.Add(tst);

                    if (tst.persistFileRingAdapter != null)
                    {
                        bool success = tst.persistFileRingAdapter.Load(allowThrow: false);
                        tst.fileContents = tst.persistFileRingAdapter.Object;

                        if (!success || tst.fileContents == null)
                            Log.Debug.Emit("Encountered issue loading persisted state for '{0}': {1}", tst.setConfig.SetName, tst.persistFileRingAdapter.LastExecption.ToString(ExceptionFormat.TypeAndMessage));
                    }
                }

                typeSetTrackerArray = typeSetTrackerList.ToArray();
                defaultTypeSetTracker = typeSetTrackerArray.SafeLast() ?? new TypeSetTracker(Config, new E039TableTypeSetPersistSpecItem("FallbackDefaultTypeSetTracker"), Log);
            }

            // go through each of the typeSetTracker's fileContents and add all corresponding TypeTableTrackers and tracked objects 

            // NOTE: that the resulting TypeTableTrackers may not end up in the same TypeSetTracker instance that they were loaded from.
            // this is intentional and allows this logic to support object persist migration to alternative sets on initial load.

            recentlyTouchedObjectTrackerList.Clear();

            foreach (var tst in typeSetTrackerArray)
            {
                if (tst.fileContents != null)
                {
                    foreach (var typeTable in tst.fileContents.TypeTableSet)
                    {
                        TypeTableTracker ttt = FindTypeTableTrackerForType(typeTable.Type, createIfNeeded: true);

                        // Find (or create) the TypeTableTracker for the persisted type.  
                        // The corresponding TypeSetTracker may not be the same one as the one that we just created (this supports migration of object location from an old typeset to a new one)

                        foreach (E039Object loadedObject in typeTable.ObjectInstanceSet)
                        {
                            E039ObjectID objID = new E039ObjectID(loadedObject.Name, typeTable.Type, loadedObject.UUID, this);

                            ObjectTracker ot = FindObjectTrackerForID(objID, createIfNeeded: true, initialFlags: loadedObject.Flags);

                            ot.obj = new E039Object(loadedObject, objID);
                            ot.objAttributesWorking = loadedObject.Attributes.ConvertToWritable();

                            foreach (var loadedLink in loadedObject.LinksToOtherObjectsList)
                            {
                                E039ObjectID adjLinkToID = new E039ObjectID(loadedLink.ToID, this);
                                E039Link adjLink = new E039Link(loadedLink) { FromID = objID, ToID = adjLinkToID };

                                LinkTrackerPair ltp = new LinkTrackerPair(adjLink);
                                ot.linkTrackerPairsToOtherObjectsDictionary[ltp.LinkKeyStr] = ltp;

                                ot.rebuildLinksToOtherObjectsList = true;
                            }

                            InnerMarkedTouchedIfNeeded(ot);
                        }
                    }
                }
            }

            // force rebuild of all fileContents prior to any persistent save
            typeSetTrackerArray.DoForEach(tst => { tst.fileContents = null; });

            // attempt to resolve all of the links in the LinkTrackerPairs by finding the matching ObjectTrackers for each such link
            // and update each tracted object's link IList contents as well.

            foreach (ObjectTracker ot in recentlyTouchedObjectTrackerList.ToArray())
            {
                // links from other objects are rebuilt completely from the links to other objects that were reloaded from persistent storage.
                foreach (LinkTrackerPair ltp in ot.linkTrackerPairsToOtherObjectsDictionary.Values)
                {
                    ltp.ObjectTracker = FindObjectTrackerForID(ltp.Link.ToID);

                    if (ltp.ObjectTracker != null)
                    {
                        LinkTrackerPair subordinateLTP = new LinkTrackerPair(ltp.Link, ltp.LinkKeyStr) { ObjectTracker = ot, OriginatingLTP = ltp };
                        ltp.SubordinateLTP = subordinateLTP;

                        ltp.ObjectTracker.linkTrackerPairsFromOtherObjectsDictionary[ltp.LinkFullIDAndKeyStr] = subordinateLTP;

                        ltp.ObjectTracker.rebuildLinksFromOtherObjectsList = true;

                        InnerMarkedTouchedIfNeeded(ltp.ObjectTracker);
                    }
                }
            }

            InnerPublishTouchedObjectTrackers();

            InnerPublishSeqNumsIfNeeded();

            SetupMainThreadStartingAndStoppingActions();
        }

        E039BasicTablePartConfig Config { get; set; }

        /// <summary>IVI to be used when creating object IVA's - this behavior is triggered based on the object's tracker has the E039ObjectFlags.CreateIVA flag set (generally only used for object types that bound for display by name.</summary>
        IValuesInterconnection ObjectIVI { get; set; }

        IE039ExternalSyncFactory ExternalSyncFactory { get; set; }

        static readonly E039ObjectID[] emptyE039ObjectIDArray = EmptyArrayFactory<E039ObjectID>.Instance;

        Dictionary<string, ActionLogging> customActionLoggingReferenceDict = null;

        #endregion

        #region IE039TableObserver interface

        INotificationObject<E039TableSeqNums> IE039TableObserver.SeqNumsPublisher 
        { 
            get { return seqNumsPublisher; } 
        }

        IE039Object[] IE039TableObserver.GetObjects(E039TypeFilter typeFilter, E039InstanceFilter instanceFilter)
        {
            lock (externalDicationaryMutex)
            {
                return InnerGetTypeAndInstanceFilteredSet(typeFilter, instanceFilter).ToArray();
            }
        }

        int IE039TableObserver.GetObjectCount(E039TypeFilter typeFilter, E039InstanceFilter instanceFilter)
        {
            lock (externalDicationaryMutex)
            {
                return InnerGetTypeAndInstanceFilteredSet(typeFilter, instanceFilter).Count();
            }
        }

        private IEnumerable<IE039Object> InnerGetTypeAndInstanceFilteredSet(E039TypeFilter typeFilter, E039InstanceFilter instanceFilter)
        {
            typeFilter = typeFilter ?? _allTypesFilter;
            instanceFilter = instanceFilter ?? _allInstancesFilter;

            return externalTypeToObjectNameDictionaryDictionary.Where(kvp => typeFilter(kvp.Key)).SelectMany(kvp => kvp.Value.Values.Select(ot => ot.volatileLastPublishedIObj).Where(o => ((o != null) && instanceFilter(o))));
        }

        private static readonly E039TypeFilter _allTypesFilter = ((typeStr) => true);
        private static readonly E039InstanceFilter _allInstancesFilter = ((objInst) => true);

        INotificationObject<IE039Object> IE039TableObserver.GetPublisher(E039ObjectID objSpec) 
        {
            objSpec = objSpec ?? E039ObjectID.Empty;

            lock (externalDicationaryMutex)
            {
                ObjectTracker ot = null;

                externalUUIDToObjectTrackerDictionary.TryGetValue(objSpec.UUID.Sanitize(), out ot);

                if (ot == null)
                {
                    Dictionary<string, ObjectTracker> typeTableDictionary = null;
                    externalTypeToObjectNameDictionaryDictionary.TryGetValue(objSpec.Type.Sanitize(), out typeTableDictionary);

                    if (typeTableDictionary != null)
                        typeTableDictionary.TryGetValue(objSpec.Name.Sanitize(), out ot);
                }

                if (ot != null)
                    return ot.objPublisher;
            }

            return null;
        }

        #endregion

        #region IE039TableUpdater interface (action factory methods) and related implementation method(s)

        IBasicAction IE039TableUpdater.Update(E039UpdateItem updateItem, string logConfigSelect)
        {
            StartPartIfNeeded();

            string logConfigSelectStr = (logConfigSelect.IsNullOrEmpty() ? "" : " {0}".CheckedFormat(logConfigSelect));
            string mesgDetails = "{0}:{1}{2}".CheckedFormat((updateItem ?? E039UpdateItem.Empty).GetType(), updateItem, logConfigSelectStr);

            return new BasicActionImpl(actionQ, (action) => PerformUpdates(action, firstUpdateItem: updateItem, updateItems: null), "Update", GetSelectedActionLoggingReference(logConfigSelect), mesgDetails: mesgDetails);
        }

        IBasicAction IE039TableUpdater.Update(params E039UpdateItem [] updateItems)
        {
            return ((IE039TableUpdater)this).Update(updateItems, logConfigSelect: null);
        }

        IBasicAction IE039TableUpdater.Update(E039UpdateItem[] updateItems, string logConfigSelect)
        {
            StartPartIfNeeded();

            string itemsStr = String.Join(",", updateItems.Select(updateItem => "{0}:{1}".CheckedFormat((updateItem ?? E039UpdateItem.Empty).GetType().GetTypeLeafName(), updateItem)));
            string mesgDetails = "{0}{1}{2}".CheckedFormat(itemsStr, (!itemsStr.IsNullOrEmpty() && !logConfigSelect.IsNullOrEmpty() ? " " : ""), logConfigSelect);

            return new BasicActionImpl(actionQ, (action) => PerformUpdates(action, firstUpdateItem: null, updateItems: updateItems), "Update", GetSelectedActionLoggingReference(logConfigSelect), mesgDetails: mesgDetails);
        }

        private ActionLogging GetSelectedActionLoggingReference(string logConfigSelect)
        {
            if (logConfigSelect.IsNullOrEmpty() || customActionLoggingReferenceDict == null)
                return ActionLoggingReference;
            else
                return customActionLoggingReferenceDict.SafeTryGetValue(logConfigSelect) ?? ActionLoggingReference;
        }

        string PerformUpdates(IProviderFacet action, E039UpdateItem firstUpdateItem, E039UpdateItem[] updateItems)
        {
            if (!BaseState.IsOnline)
                return "BaseState is not Online [{0}]".CheckedFormat(BaseState);

            string ec = null;
            bool addedPendingSyncOperation = false;

            if (firstUpdateItem != null && ec.IsNullOrEmpty())
            {
                // handle first update item
                ec = InnerPerformUpdate(action, firstUpdateItem, ref addedPendingSyncOperation);
            }

            if (!updateItems.IsNullOrEmpty() && ec.IsNullOrEmpty())
            {
                // handle each of the items in the updateItems array (if any)
                ec = updateItems.Select(updateItem => InnerPerformUpdate(action, updateItem, ref addedPendingSyncOperation)).FirstOrDefault(updateResultCode => !updateResultCode.IsNullOrEmpty());
            }

            // publish updates (on success or not)

            if (recentlyTouchedObjectTrackerList.Count > 0)
                InnerPublishTouchedObjectTrackers();

            InnerPublishSeqNumsIfNeeded();

            // return null/empty/ec based on success and based on determination of whether a pending sync operation was created.

            ec = ec.MapNullToEmpty();

            if (!ec.IsNullOrEmpty())
            {
                action.CompleteRequest(ec);

                if (addedPendingSyncOperation)
                    ServicePendingSyncOperations();
            }
            else if (!addedPendingSyncOperation)
            {
                action.CompleteRequest(ec);
            }
            // else we consumed the action and will signal its completion later.

            return null;
        }

        string InnerPerformUpdate(IProviderFacet action, E039UpdateItem updateItem, ref bool addedPendingSyncOperation)
        {
            if (updateItem == null)
                return string.Empty;

            if (updateItem is E039UpdateItem.AddObject)
                return InnerPerformAddObjectUpdateItem(updateItem as E039UpdateItem.AddObject);

            ObjectTracker objectTracker = null;
            ObjectTracker [] objectTrackerArray = null;
            E039Link link = default(E039Link);

            if (updateItem is E039UpdateItem.ObjIDBase && !(updateItem is E039UpdateItem.SyncPersist))
            {
                var uiAsObjIDBase = updateItem as E039UpdateItem.ObjIDBase;

                if (uiAsObjIDBase.ObjIDSet == null)
                {
                    var objID = uiAsObjIDBase.ObjID;

                    objectTracker = FindObjectTrackerForID(objID);
                    if (objectTracker == null)
                        return "No object found for E039ObjectID '{0}'".CheckedFormat(objID.ToString(E039ToStringSelect.DefaultObjIDSelect));
                }
                else
                {
                    var objIDSet = uiAsObjIDBase.ObjIDSet;
                    objectTrackerArray = objIDSet .Select(objID => FindObjectTrackerForID(objID)).ToArray();

                    if (objectTrackerArray.Count(ot => ot == null) > 0)
                        return "One or more IDs in [{0}] were not found".CheckedFormat(String.Join(",", objIDSet.Select(ot => ot.ToString(E039ToStringSelect.FullName))));
                }
            }
            else if (updateItem is E039UpdateItem.LinkBase)
            {
                link = (updateItem as E039UpdateItem.LinkBase).Link;
                E039ObjectID objID = link.FromID;
                objectTracker = FindObjectTrackerForID(objID);
                if (objectTracker == null)
                    return "Source/From object not found for link '{0}'".CheckedFormat(link.ToString(E039ToStringSelect.DefaultObjIDSelect));

                // switch the link's FromID to use the objectTracker version (the one that has the UUID in it)
                if (link.FromID.UUID.IsNullOrEmpty())
                    link.FromID = objectTracker.ObjID;
            }

            // This test MUST come before the SetAttributes test as each TestAndSetAttributes is also a SetAttributes update item.
            if (updateItem is E039UpdateItem.TestAndSetAttributes)
                return InnerPerformTestAndSetAttributesUpdateItem(objectTracker, updateItem as E039UpdateItem.TestAndSetAttributes);

            if (updateItem is E039UpdateItem.SetAttributes)
            {
                var uiAsSetAttributes = updateItem as E039UpdateItem.SetAttributes;
                if (objectTrackerArray != null)
                {
                    var ecSet = objectTrackerArray.Select(ot => InnerPerformSetAttributesUpdateItem(ot, uiAsSetAttributes));
                    return ecSet.Where(ec => ec.IsNeitherNullNorEmpty()).FirstOrDefault().MapNullToEmpty();
                }
                else
                {
                    return InnerPerformSetAttributesUpdateItem(objectTracker, uiAsSetAttributes);
                }
            }

            if (updateItem is E039UpdateItem.AddLink)
                return InnerPerformAddLinkUpdateItem(objectTracker, link, updateItem as E039UpdateItem.AddLink);

            if (updateItem is E039UpdateItem.RemoveLink)
                return InnerPerformRemoveLinkUpdateItem(objectTracker, link);

            if (updateItem is E039UpdateItem.RemoveObject)
            {
                InnerPerformRemoveObjectUpdateItem(objectTracker, updateItem as E039UpdateItem.RemoveObject);
                return string.Empty;
            }

            if (updateItem is E039UpdateItem.SyncPublication)
                return InnerPerformSyncPublicationUpdateItem();

            if (updateItem is E039UpdateItem.SyncPersist)
                return InnerPerformSyncPersistUpdateItem(action, updateItem as E039UpdateItem.SyncPersist, ref addedPendingSyncOperation);

            if (updateItem is E039UpdateItem.SyncExternal)
                return InnerPerformSyncExternalUpdateItem(updateItem as E039UpdateItem.SyncExternal);

            return "UpdateItem type '{0}' was not recognized".CheckedFormat(updateItem.GetType());
        }

        string InnerPerformTestAndSetAttributesUpdateItem(ObjectTracker ot, E039UpdateItem.TestAndSetAttributes updateItem)
        {
            INamedValueSet testAttributes = updateItem.TestAttributeSet;
            INamedValueSet objAttributes = ot.objAttributesWorking;

            INamedValue firstMismatch = testAttributes.FirstOrDefault(testNV => !objAttributes[testNV.Name].VC.Equals(testNV.VC));
            bool testConditionsMet = (firstMismatch == null);

            updateItem.TestConditionsMet = testConditionsMet;

            if (testConditionsMet)
                return InnerPerformSetAttributesUpdateItem(ot, updateItem);
            else if (updateItem.FailIfTestConditionsNotMet)
                return "Test failed: test attribute {0} did not match object '{1}' value {2}".CheckedFormat(firstMismatch, ot.ObjID, objAttributes[firstMismatch.Name].VC);
            else
                return string.Empty;
        }

        string InnerPerformSetAttributesUpdateItem(ObjectTracker ot, E039UpdateItem.SetAttributes updateItem)
        {
            INamedValueSet attributes = updateItem.Attributes;

            if (attributes == null)
                return "Attributes parameter cannot be null";

            ot.obj.Attributes = (ot.objAttributesWorking = ot.objAttributesWorking.MergeWith(attributes, updateItem.MergeBehavior));
            
            InnerMarkedTouchedIfNeeded(ot);

            return string.Empty;
        }

        string InnerPerformAddLinkUpdateItem(ObjectTracker ot, E039Link link, E039UpdateItem.AddLink addLink)
        {
            ObjectTracker otherOT = null;

            if (!link.IsToIDEmpty)
            {
                otherOT = FindObjectTrackerForID(link.ToID);
                if (otherOT == null)
                    return "Target/To object not found for link '{0}'".CheckedFormat(link.ToString(E039ToStringSelect.DefaultObjIDSelect));

                // switch the link's ToID to use the ObjID from the linkTo object's ObjectTracker (will have correct values of UUID and TableObserver)
                link.ToID = otherOT.ObjID;
            }

            string linkKeyStr = link.Key;

            LinkTrackerPair ltp = ot.linkTrackerPairsToOtherObjectsDictionary.SafeTryGetValue(linkKeyStr);

            if (ltp != null && (addLink.Link.ToID.IsEmpty || ltp.Link.ToID.Equals(link.ToID)) && addLink.IfNeeded)
                return string.Empty;        // we are adding a link key that already exists and the caller provided target is empty and the caller indicated to add the link if needed.  So do nothing.

            if (ltp != null && (ltp.ObjectTracker != null || !ltp.Link.IsToIDEmpty))
                return "Link key '{0}' already in use as '{1}'".CheckedFormat(linkKeyStr, ltp.Link);

            if (ltp == null)
            {
                ltp = new LinkTrackerPair(link) { ObjectTracker = otherOT };
                ot.linkTrackerPairsToOtherObjectsDictionary[linkKeyStr] = ltp;
            }
            else
            {
                ltp.Link.ToID = link.ToID;
                ltp.ObjectTracker = otherOT;
            }

            if (otherOT != null)
            {
                if (addLink.AutoUnlinkFromPriorByTypeStr)
                {
                    LinkTrackerPair priorSubordinateLTP = otherOT.linkTrackerPairsFromOtherObjectsDictionary.Values.FirstOrDefault(scanLTP => linkKeyStr == scanLTP.LinkKeyStr);

                    if (priorSubordinateLTP != null && priorSubordinateLTP.ObjectTracker != null && priorSubordinateLTP.OriginatingLTP != null)
                        InnerUnlinkFromToOtherObjectsSet(priorSubordinateLTP.ObjectTracker, priorSubordinateLTP.OriginatingLTP);
                }

                otherOT.linkTrackerPairsFromOtherObjectsDictionary[ltp.LinkFullIDAndKeyStr] = ltp.SubordinateLTP = new LinkTrackerPair(link, linkKeyStr) { ObjectTracker = ot, OriginatingLTP = ltp };
                otherOT.rebuildLinksFromOtherObjectsList = true;
                InnerMarkedTouchedIfNeeded(otherOT);
            }

            ot.rebuildLinksToOtherObjectsList = true;
            InnerMarkedTouchedIfNeeded(ot);     // this way the link from object gets the most recently assigned sequence number

            return string.Empty;
        }

        string InnerPerformRemoveLinkUpdateItem(ObjectTracker objectTracker, E039Link link)
        {
            LinkTrackerPair ltp = null;

            if (!objectTracker.linkTrackerPairsToOtherObjectsDictionary.TryGetValue(link.Key, out ltp) || ltp == null || !ltp.Link.Equals(link))
                return "Link to other object '{0}' not found, or did not match".CheckedFormat(link);

            string ec = InnerUnlinkFromToOtherObjectsSet(objectTracker, ltp);
            if (!ec.IsNullOrEmpty())
                return "Unlink for '{0}' failed: {1}".CheckedFormat(link, ec);

            return string.Empty;
        }

        string InnerUnlinkFromToOtherObjectsSet(ObjectTracker ot, LinkTrackerPair ltp)
        {
            string ec = null;

            ObjectTracker otherOT = ltp.ObjectTracker;
            LinkTrackerPair subordinateLTP = ltp.SubordinateLTP;

            if (otherOT != null)
            {
                if (ec.IsNullOrEmpty() && subordinateLTP == null)
                    ec = "Internal: subordinate LTP is missing";

                if (ec.IsNullOrEmpty() && (!Object.ReferenceEquals(subordinateLTP.ObjectTracker, ot) || !Object.ReferenceEquals(subordinateLTP.OriginatingLTP, ltp)))
                {
                    ec = "Mirror link is not valid ([From] has wrong instance)";
                    subordinateLTP = null;
                }

                if (subordinateLTP != null)
                {
                    otherOT.linkTrackerPairsFromOtherObjectsDictionary.Remove(ltp.LinkFullIDAndKeyStr);
                    otherOT.rebuildLinksFromOtherObjectsList = true;
                    InnerMarkedTouchedIfNeeded(otherOT);
                }
            }

            ltp.Link.ToID = E039ObjectID.Empty;
            ltp.ObjectTracker = null;
            ltp.SubordinateLTP = null;

            ot.rebuildLinksToOtherObjectsList = true;
            InnerMarkedTouchedIfNeeded(ot);

            return ec.MapNullToEmpty();
        }

        string InnerPerformAddObjectUpdateItem(E039UpdateItem.AddObject updateItem)
        {
            updateItem.AddedObjectPublisher = null;

            E039ObjectID objID = updateItem.ObjID;

            E039ObjectFlags clientFlags = updateItem.Flags & E039ObjectFlags.ClientUsableFlags;

            if (!objID.IsValid)
                return "Cannot add object for invalid id: '{0}'".CheckedFormat(objID);

            // if the caller did not provide a UUID then they do not want the item to carry one.

            // if the caller did not define the ObjID's TableObserver to be this one then update the TableObserver to refer to this one.
            if (!object.ReferenceEquals(objID.TableObserver, this))
                objID = new E039ObjectID(objID, this);

            ObjectTracker ot = FindObjectTrackerForID(objID);

            if (ot != null && !updateItem.IfNeeded)
                return "Cannot add object '{0}': object already exists".CheckedFormat(updateItem.ObjID);

            if (ot == null)
            {
                ot = FindObjectTrackerForID(objID, createIfNeeded: true, initialFlags: updateItem.Flags);

                ot.obj = new E039Object(objID, clientFlags, updateItem.Attributes);
                ot.objAttributesWorking = updateItem.Attributes.ConvertToWritable();

                seqNums.AddedItemsCount++;
                seqNums.TableChangeSeqNum = GetNextSeqNum();
            }
            else
            {
                // merge in the given uuid (if non-empty) and if the old object did not already have one.
                string updateItemUUID = objID.UUID;
                if (!updateItemUUID.IsNullOrEmpty() && ot.ObjID.UUID.IsNullOrEmpty())
                {
                    ot.AssignUUID(updateItemUUID);

                    // update the uuid to tracker dictionaries with the new UUID and tracker.
                    uuidToObjectTrackerDictionary[updateItemUUID] = ot;

                    lock (externalDicationaryMutex)
                    {
                        externalUUIDToObjectTrackerDictionary[updateItemUUID] = ot;
                    }
                }

                // replace the ClientUsableFlags flags in the object with the corresponding values given in the update item.  CreateIVA is a special case as we may need to create the IVA here.
                if ((ot.flags & E039ObjectFlags.ClientUsableFlags) != clientFlags)
                {
                    E039ObjectFlags newFlags = (ot.flags & ~E039ObjectFlags.ClientUsableFlags) | clientFlags;

                    ot.obj.Flags = ot.flags = newFlags;
                    
                    CreateIVAIfNeeded(ot);
                }

                // merge in attributes
                if (updateItem.MergeBehavior != NamedValueMergeBehavior.None && !updateItem.Attributes.IsNullOrEmpty())
                {
                    ot.obj.Attributes = (ot.objAttributesWorking = ot.objAttributesWorking.MergeWith(updateItem.Attributes, updateItem.MergeBehavior));
                }
            }

            InnerMarkedTouchedIfNeeded(ot);

            updateItem.AddedObjectPublisher = ot.objPublisher;

            return string.Empty;
        }

        List<ObjectTracker> objectTrackersToRemoveList = new List<ObjectTracker>();

        List<ObjectTracker> potentialObjectTrackersToRemoveListPerPass = new List<ObjectTracker>();

        void InnerPerformRemoveObjectUpdateItem(ObjectTracker ot, E039UpdateItem.RemoveObject updateItem)
        {
            objectTrackersToRemoveList.Add(ot);

            while (!objectTrackersToRemoveList.IsEmpty())
            {
                ot = objectTrackersToRemoveList.SafeTakeFirst();
                if (ot == null)
                    continue;

                potentialObjectTrackersToRemoveListPerPass.Clear();

                E039ObjectID objID = ot.ObjID;

                ot.obj.Flags |= E039ObjectFlags.IsFinal;

                InnerMarkedTouchedIfNeeded(ot);

                // Identify the set of other ObjectTrackers that we will be removing after we have unlinked this object fully.
                E039LinkFilter filter = updateItem.RemoveLinkedToOtherObjectsFilter ?? E039Link.FilterNone;

                ot.linkTrackerPairsToOtherObjectsDictionary.Values.Where(ltp => ((ltp.ObjectTracker != null) && !ltp.ObjectTracker.flags.IsSet(E039ObjectFlags.Pinned) && filter(ltp.Link))).DoForEach(ltp => potentialObjectTrackersToRemoveListPerPass.Add(ltp.ObjectTracker));

                // unlink this object from other objects: capture the current set of LinkTrackerPairs in both directions, clear the two dictionaries and then remove the links from any remaining linked objects
                LinkTrackerPair[] linkTrackerPairsFromOtherObjectsArray = ot.linkTrackerPairsFromOtherObjectsDictionary.Values.ToArray();
                LinkTrackerPair[] linkTrackerPairsToOtherObjectsArray = ot.linkTrackerPairsToOtherObjectsDictionary.Values.ToArray();

                ot.linkTrackerPairsFromOtherObjectsDictionary.Clear();
                ot.linkTrackerPairsToOtherObjectsDictionary.Clear();

                foreach (var ltp in linkTrackerPairsToOtherObjectsArray)
                {
                    if (ltp.ObjectTracker != null)
                        InnerUnlinkFromToOtherObjectsSet(ot, ltp);
                }

                foreach (var ltp in linkTrackerPairsFromOtherObjectsArray)
                {
                    if (ltp.OriginatingLTP != null)
                        InnerUnlinkFromToOtherObjectsSet(ltp.ObjectTracker, ltp.OriginatingLTP);
                }

                InnerHandleChangedLinksIfNeeded(ot);

                // finally remove the object tracker itself from the various dictionary representations of this table.

                bool idHasUUID = !objID.UUID.IsNullOrEmpty();

                // remove the object from the external dictionaries
                lock (externalDicationaryMutex)
                {
                    if (idHasUUID)
                        externalUUIDToObjectTrackerDictionary.Remove(objID.UUID);

                    Dictionary<string, ObjectTracker> externalObjectNameToTrackerDictionary = null;
                    externalTypeToObjectNameDictionaryDictionary.TryGetValue(objID.Type, out externalObjectNameToTrackerDictionary);

                    if (externalObjectNameToTrackerDictionary != null)
                        externalObjectNameToTrackerDictionary.Remove(objID.Name);
                }

                // remove the object from the internal dictionaries
                ot.typeTableTracker.objectTrackerDictionary.Remove(objID.Name);

                if (idHasUUID)
                    uuidToObjectTrackerDictionary.Remove(objID.UUID);

                // indicate that a table change has happened
                seqNums.RemovedItemsCount++;
                seqNums.TableChangeSeqNum = GetNextSeqNum();

                // recursively add the potentialObjectTrackersToRemoveListPerPass to the objectTrackersToRemoveList if none of their remaining links from other objects now match the given filter)
                while (!potentialObjectTrackersToRemoveListPerPass.IsEmpty())
                {
                    var potentialObjectTrackerToRemove = potentialObjectTrackersToRemoveListPerPass.SafeTakeFirst();
                    if (potentialObjectTrackerToRemove != null && !potentialObjectTrackerToRemove.linkTrackerPairsFromOtherObjectsDictionary.Values.Any(fromLTP => filter(fromLTP.Link)))
                        objectTrackersToRemoveList.Add(potentialObjectTrackerToRemove);
                }
            }
        }

        string InnerPerformSyncPublicationUpdateItem()
        {
            InnerPublishTouchedObjectTrackers();
            return string.Empty;
        }

        string InnerPerformSyncPersistUpdateItem(IProviderFacet action, E039UpdateItem.SyncPersist updateItem, ref bool addedPendingSyncOperation)
        {
            if (addedPendingSyncOperation)
                return "Only one SyncPersist update item may be used in any given Update action";

            // validate syncPersist settings

            // publish touched items (as if this was a SyncPublish item)
            InnerPublishTouchedObjectTrackers();

            ulong completeAfterSeqNum = seqNums.PublishedObjectSeqNum;

            string typeName = updateItem.ObjID.Type;
            TypeSetTracker[] relevantTypeSetTrackerArray = (typeName.IsNullOrEmpty() ? typeSetTrackerArray : new [] { FindTypeSetTrackerForType(typeName) }).Where(tst => tst.persistFileRingAdapter != null).ToArray();

            if (relevantTypeSetTrackerArray.Any(tst => tst == null))
                return "Given type name '{0}' was not found".CheckedFormat(typeName);

            if (relevantTypeSetTrackerArray.IsEmpty() || relevantTypeSetTrackerArray.All(tst => !tst.IsWritePending && !tst.IsWriteActive))
                return string.Empty;

            // there is work to do.

            foreach (var tst in relevantTypeSetTrackerArray)
            {
                if (tst.IsWritePending)
                {
                    UpdateFileContentsAndOptionallyIssueWrite(tst, issueWrite: true);
                }
            }

            // fill in the rest of the construction here
            PendingSyncPersistOperation pendingSyncOperation = new PendingSyncPersistOperation()
            {
                action = action,
                qpcTimeStamp = QpcTimeStamp.Now,
                syncUpdateItem = updateItem,
                completeAfterSeqNum = completeAfterSeqNum,
                relevantTypeSetTrackerArray = relevantTypeSetTrackerArray,
            };

            pendingSyncPersistOperationList.Add(pendingSyncOperation);

            addedPendingSyncOperation = true;

            return string.Empty;
        }

        string InnerPerformSyncExternalUpdateItem(E039UpdateItem.SyncExternal updateItem)
        {
            string ec = string.Empty;

            if (ec.IsNullOrEmpty() && updateItem.SyncPublicationFirst)
                ec = InnerPerformSyncPublicationUpdateItem();

            if (ec.IsNullOrEmpty())
            {
                if (ExternalSyncFactory != null)
                {}
                else if (updateItem.FailIfNoExternalSyncFactoryDefined)
                    ec = "No External Sync Factory has been defined";
                else
                    return string.Empty;        // we simply return in this case as there is no ExternalSyncFactory to use.
            }

            if (ec.IsNullOrEmpty())
            {
                IBaseState externalSyncFactoryBaseState = ExternalSyncFactory.BaseState;

                if (externalSyncFactoryBaseState.IsOnline)
                { }
                else if (updateItem.FailIfExternalSyncFactoryIsNotOnline)
                    ec = "External Sync Factory is not online [{0}, {1}]".CheckedFormat(ExternalSyncFactory.PartID, externalSyncFactoryBaseState);
                else
                    return string.Empty;        // we simply return in this case as the external sync factory is not usable right now.
            }

            if (ec.IsNullOrEmpty())
            {
                TimeSpan? waitTimeLimit = updateItem.WaitTimeLimit ?? Config.ExternalSyncTimeLimit;

                IClientFacet syncAction = ExternalSyncFactory.Sync().StartInline();

                // if the resulting wait time limit is null or zero then we use an indifinite wait, otherwise we use the given time limit as the time limit to use when waiting for the external sync action to complete.
                if ((waitTimeLimit ?? TimeSpan.Zero).IsZero())
                    ec = syncAction.WaitUntilComplete();
                else if (syncAction.WaitUntilComplete(waitTimeLimit ?? TimeSpan.Zero))
                    ec = syncAction.ActionState.ResultCode;
                else 
                {
                    if (updateItem.RequestCancelOnTimeLimitReached)
                        syncAction.RequestCancel();

                    if (updateItem.FailOnWaitTimeLimitReached)
                        ec = "SyncAction did not complete within the specified time limit [{0:f3} sec]".CheckedFormat((waitTimeLimit ?? TimeSpan.Zero).TotalSeconds);
                }
            }

            return ec;
        }

        /// <summary>
        /// Checks if the linksFromOtherObjectsListTouched or linksToOtherObjectsListTouched flag was set and rebuilds the E039Object's corresponding E039Link ILists from it.
        /// </summary>
        void InnerHandleChangedLinksIfNeeded(ObjectTracker ot)
        {
            if (ot.rebuildLinksFromOtherObjectsList)
            {
                ot.obj.LinksFromOtherObjectsList = new ReadOnlyIList<E039Link>(ot.linkTrackerPairsFromOtherObjectsDictionary.Values.Select(ltp => ltp.Link));
                ot.rebuildLinksFromOtherObjectsList = false;
                InnerMarkedTouchedIfNeeded(ot);
            }

            if (ot.rebuildLinksToOtherObjectsList)
            {
                ot.obj.LinksToOtherObjectsList = new ReadOnlyIList<E039Link>(ot.linkTrackerPairsToOtherObjectsDictionary.Values.Select(ltp => ltp.Link));
                ot.rebuildLinksToOtherObjectsList = false;
                InnerMarkedTouchedIfNeeded(ot);
            }
        }

        /// <summary>
        /// if the given <paramref name="objectTracker"/> has not been marked touched then this method
        /// marks it touched and adds it to the recentlyTouchedObjectTrackerList
        /// </summary>
        void InnerMarkedTouchedIfNeeded(ObjectTracker objectTracker)
        {
            if (!objectTracker.touched)
            {
                objectTracker.touched = true;
                recentlyTouchedObjectTrackerList.Add(objectTracker);
            }
        }

        /// <summary>
        /// Goes through all of the object trackers in the recentlyTouchedObjectTrackerList, 
        /// publishes each one (assigning a unique sequence number to each such publication), and then 
        /// recursively publishes each of the upward linked objects using the publicationTriggeredByLinkedObject option by invoking InnerRecursivePublishThroughUplinks on these published objects.
        /// </summary>
        void InnerPublishTouchedObjectTrackers()
        {
            ObjectTracker[] touchedOTArray = recentlyTouchedObjectTrackerList.ToArray();

            recentlyTouchedObjectTrackerList.Clear();

            // rebuild ReadOnlyILists<E039Link> for LinksFromOtherObjectsList and LinksToOtherObjectsList for objects that have been touched and which flaged that they needed to have one or both lists rebuilt
            // Note: this will not touch any new objects as we are only rebuilding the lists for objects that have already been marked as touched.
            foreach (var ot in touchedOTArray)
                InnerHandleChangedLinksIfNeeded(ot);

            foreach (var ot in touchedOTArray)
                ot.Publish(seqNums.PublishedObjectSeqNum = GetNextSeqNum(), publicationTriggeredByLinkedObject: false);

            // sweep through all of the just published objects and do an upstream linked object publish if the upstream object has a lower seqNum than than the last published one for the object we just published.
            foreach (var ot in touchedOTArray)
                InnerRecursivePublishThroughLinksFromOtherObjects(ot, ot.lastPublishedSeqNum, firstLevel: true);

            // build up the list of active ReferenceSetUpdateCollectors and add all of the touched items lastReferenceSetItemSeqNum into their corresponding removedSetItemSeqNumLists and add the new items to their corresonding addedSetItemTrackersLists
            foreach (var ot in touchedOTArray)
            {
                var setUpdateCollector = ot.typeSetTracker.referenceSetUpdateCollector;
                if (setUpdateCollector != null)
                {
                    if (!activeSetUpdateCollectorHashSet.Contains(setUpdateCollector))
                    {
                        activeSetUpdateCollectorHashSet.Add(setUpdateCollector);
                        activeSetUpdateCollectorList.Add(setUpdateCollector);
                    }

                    if (ot.lastReferenceSetItemSeqNum != 0)
                        setUpdateCollector.removedSetItemSeqNumList.Add(ot.lastReferenceSetItemSeqNum);

                    if (ot.lastPublishedObj != null && !ot.obj.Flags.IsSet(E039ObjectFlags.IsFinal))
                        setUpdateCollector.addedSetItemTrackersList.Add(ot);
                    else
                        ot.lastReferenceSetItemSeqNum = 0;
                }
            }

            int activeSetUpdateCollectorListCount = activeSetUpdateCollectorList.Count;
            if (activeSetUpdateCollectorListCount > 0)
            {
                // distribute the set updates into their respective sets now.
                for (int idx = 0; idx < activeSetUpdateCollectorListCount; idx++)
                {
                    var setUpdateTracker = activeSetUpdateCollectorList[idx];

                    // clone the touched objects to be published so that we can enable their serializeWithType option.  This will allow the derserialized versions to fully reconsitute their E039ObjectID on deserialization (using the annotated OnDeserialized method).
                    E039Object[] addedObjectsArray = setUpdateTracker.addedSetItemTrackersList.Select(ot => new E039Object(ot.lastPublishedObj, serializeForRemoteUse: true)).ToArray();

                    if (setUpdateTracker.referenceSet != null)
                    {
                        setUpdateTracker.removedSetItemSeqNumList.Sort(seqNumComparer);

                        long[] sortedRemovedItemSeqNumArray = setUpdateTracker.removedSetItemSeqNumList.ToArray();

                        long firstAddedItemSeqNum = setUpdateTracker.referenceSet.RemoveBySeqNumsAndAddItems(sortedRemovedItemSeqNumArray, addedObjectsArray);

                        // update the lastReferenceSetItemSeqNum values for the touched ObjectTrackers (if any)
                        setUpdateTracker.addedSetItemTrackersList.DoForEach(ot => { ot.lastReferenceSetItemSeqNum = firstAddedItemSeqNum++; });
                    }

                    if (setUpdateTracker.referenceHistorySet != null)
                        setUpdateTracker.referenceHistorySet.RemoveAndAdd(null, addedObjectsArray);

                    setUpdateTracker.Clear();
                }

                activeSetUpdateCollectorHashSet.Clear();
                activeSetUpdateCollectorList.Clear();
            }
        }

        /// <summary>
        /// Recursively republish the current object up to the root object(s) by following all of the paths using this object's linkTrackerPairsFromOtherObjectsDictionary
        /// At each layer, this method checks if the object already has the given seqNum or a newer one and if not then this method republishes the last published object to trigger clients to be aware that one or more linked object states may have changed.
        /// The requirement that we stop recursing if the current tracker is not the firstLevel and its lastPublishedSeqNum is the same as, or newer than, the given seqNum also prevents infinite loops even if the client concocts object patterns with loops in them.
        /// </summary>
        void InnerRecursivePublishThroughLinksFromOtherObjects(ObjectTracker ot, ulong seqNum, bool firstLevel = false)
        {
            if (ot != null && ot.lastPublishedSeqNum < seqNum || firstLevel)
            {
                if (!firstLevel)
                    ot.Publish(seqNum, publicationTriggeredByLinkedObject: true);

                foreach (var ltp in ot.linkTrackerPairsFromOtherObjectsDictionary.Values)
                    InnerRecursivePublishThroughLinksFromOtherObjects(ltp.ObjectTracker, seqNum);
            }
        }

        #endregion

        #region SimpleActivePart overrides, SetupMainThreadStartingAndStoppingActions

        private void SetupMainThreadStartingAndStoppingActions()
        {
            AddMainThreadStartingAction(() => persistHelperPart.CreateGoOnlineAction(true).RunInline());

            AddMainThreadStoppingAction(() => 
            {
                // set the persistHelperPart offline and dispose of it.
                persistHelperPart.CreateGoOfflineAction().RunInline();
                Fcns.DisposeOfObject(ref persistHelperPart);

                // make last attempt to flush any outstanding tables by directly updating the persist reference copies and then asking the relevant persistFileRingAdapters to Save
                foreach (var tst in typeSetTrackerArray)
                {
                    if (tst.persistFileRingAdapter != null && tst.IsWritePending)
                    {
                        UpdateFileContentsAndOptionallyIssueWrite(tst, issueWrite: false);
                        tst.persistFileRingAdapter.Save(allowThrow: false);
                    }
                }
            });
        }

        protected override void PerformMainLoopService()
        {
            ServicePersistWrites();
            ServicePendingSyncOperations();
        }

        #endregion

        #region PendingSyncPersist operations

        void ServicePendingSyncOperations()
        {
            if (pendingSyncPersistOperationList.Count > 0)
            {
                foreach (var pendingSyncOp in pendingSyncPersistOperationList.ToArray())
                {
                    if (!pendingSyncOp.action.ActionState.IsComplete)
                    {
                        TimeSpan syncOpAge = pendingSyncOp.qpcTimeStamp.Age;
                        if (pendingSyncOp.relevantTypeSetTrackerArray.All(tst => tst.lastSucceededSaveActionSeqNum >= pendingSyncOp.completeAfterSeqNum))
                            pendingSyncOp.action.CompleteRequest(string.Empty);
                        else if (pendingSyncOp.syncUpdateItem.WaitTimeLimit != null && syncOpAge > (pendingSyncOp.syncUpdateItem.WaitTimeLimit ?? TimeSpan.Zero))
                            pendingSyncOp.action.CompleteRequest(pendingSyncOp.syncUpdateItem.FailOnWaitTimeLimitReached ? "Time limit reached after {0:f6} seconds".CheckedFormat(syncOpAge.TotalSeconds) : string.Empty);
                    }

                    if (pendingSyncOp.action.ActionState.IsComplete)
                        pendingSyncPersistOperationList.Remove(pendingSyncOp);      // this is safe since the foreach captured the list contents as an array.
                }
            }
        }

        class PendingSyncPersistOperation
        {
            public E039UpdateItem.SyncPersist syncUpdateItem;
            public QpcTimeStamp qpcTimeStamp;
            public IProviderFacet action;
            public ulong completeAfterSeqNum;
            public TypeSetTracker[] relevantTypeSetTrackerArray;
        }

        List<PendingSyncPersistOperation> pendingSyncPersistOperationList = new List<PendingSyncPersistOperation>();

        #endregion

        #region internal fields (persistHelperPart, typeSetTrackerArray, uuidToObjectTrackerDictionary, typeNameToTypeTableTrackerDictionary, recentlyTouchedObjectTrackerList)

        PersistHelper persistHelperPart;

        TypeSetTracker[] typeSetTrackerArray = null;
        TypeSetTracker defaultTypeSetTracker = null;
        Dictionary<string, ObjectTracker> uuidToObjectTrackerDictionary = new Dictionary<string, ObjectTracker>();
        Dictionary<string, TypeTableTracker> typeNameToTypeTableTrackerDictionary = new Dictionary<string, TypeTableTracker>();

        List<ObjectTracker> recentlyTouchedObjectTrackerList = new List<ObjectTracker>();

        static readonly ObjectTracker emptyObjectTracker = new ObjectTracker(E039ObjectID.Empty, null);

        #endregion

        #region ReferenceSetUpdateCollector and related fields

        HashSet<ReferenceSetUpdateCollector> activeSetUpdateCollectorHashSet = new HashSet<ReferenceSetUpdateCollector>();
        List<ReferenceSetUpdateCollector> activeSetUpdateCollectorList = new List<ReferenceSetUpdateCollector>();
        static readonly IComparer<long> seqNumComparer = Comparer<long>.Default;

        private class ReferenceSetUpdateCollector
        {
            public IReferenceSet<E039Object> referenceSet;
            public IReferenceSet<E039Object> referenceHistorySet;

            public List<long> removedSetItemSeqNumList = new List<long>();
            public List<ObjectTracker> addedSetItemTrackersList = new List<ObjectTracker>();

            /// <summary>
            /// Clears the removedSetItemSeqNumList and the addedSetItemTrackersList
            /// </summary>
            public void Clear()
            {
                removedSetItemSeqNumList.Clear();
                addedSetItemTrackersList.Clear();
            }
        }

        #endregion

        #region support fields for asynchronous methods

        object externalDicationaryMutex = new object();
        Dictionary<string, ObjectTracker> externalUUIDToObjectTrackerDictionary = new Dictionary<string, ObjectTracker>();
        Dictionary<string, Dictionary<string, ObjectTracker>> externalTypeToObjectNameDictionaryDictionary = new Dictionary<string, Dictionary<string, ObjectTracker>>();

        #endregion

        ObjectTracker FindObjectTrackerForID(E039ObjectID id, bool createIfNeeded = false, E039ObjectFlags initialFlags = E039ObjectFlags.None)
        {
            if (id == null || id.IsEmpty)
                return null;

            ObjectTracker ot = null;
 
            if (!id.UUID.IsNullOrEmpty() && uuidToObjectTrackerDictionary.TryGetValue(id.UUID, out ot) && ot != null)
                return ot;

            TypeTableTracker ttt = FindTypeTableTrackerForType(id.Type, createIfNeeded: createIfNeeded);

            if (ttt == null)
                return null;

            if (ttt.objectTrackerDictionary.TryGetValue(id.Name, out ot) && ot != null)
                return ot;

            if (!createIfNeeded)
                return null;

            ot = new ObjectTracker(id, this)
            {
                flags = initialFlags,
                typeTableTracker = ttt,
                typeSetTracker = ttt.typeSetTracker,
            };

            // create all of the various possible versions of the publishers for the object and provide the non-null initial value.

            ot.objPublisher = new InterlockedNotificationRefObject<IE039Object>();

            CreateIVAIfNeeded(ot);

            // Add the ObjectTracker to the various dictionaries

            ttt.objectTrackerDictionary[id.Name] = ot;

            bool idHasUUID = !id.UUID.IsNullOrEmpty();

            if (idHasUUID)
                uuidToObjectTrackerDictionary[id.UUID] = ot;

            lock (externalDicationaryMutex)
            {
                if (idHasUUID)
                    externalUUIDToObjectTrackerDictionary[id.UUID] = ot;

                Dictionary<string, ObjectTracker> externalObjectNameToTrackerDictionary = null;
                externalTypeToObjectNameDictionaryDictionary.TryGetValue(id.Type, out externalObjectNameToTrackerDictionary);

                if (externalObjectNameToTrackerDictionary == null)
                    externalTypeToObjectNameDictionaryDictionary[id.Type] = externalObjectNameToTrackerDictionary = new Dictionary<string, ObjectTracker>();

                externalObjectNameToTrackerDictionary[id.Name] = ot;
            }

            return ot;
        }

        private void CreateIVAIfNeeded(ObjectTracker ot)
        {
            E039ObjectID id = ot.ObjID;

            if (ot.flags.IsSet(E039ObjectFlags.CreateIVA) && ot.objIVA == null)
                ot.objIVA = ObjectIVI.GetValueAccessor<E039Object>("{0}{1}.{2}".CheckedFormat(Config.ObjectIVAPrefix, id.Type, id.Name));
        }

        TypeTableTracker FindTypeTableTrackerForType(string typeName, bool createIfNeeded = false)
        {
            typeName = typeName.Sanitize();

            TypeTableTracker ttt = null;
            if (typeNameToTypeTableTrackerDictionary.TryGetValue(typeName, out ttt) && ttt != null)
                return ttt;

            if (createIfNeeded)
            {
                TypeSetTracker tstForType = FindTypeSetTrackerForType(typeName);
                ttt = tstForType.FindTypeTableTracker(typeName, createIfNeeded: true);

                typeNameToTypeTableTrackerDictionary[typeName] = ttt;

                lock (externalDicationaryMutex)
                {
                    if (!externalTypeToObjectNameDictionaryDictionary.ContainsKey(typeName))
                        externalTypeToObjectNameDictionaryDictionary[typeName] = new Dictionary<string, ObjectTracker>();
                }

                seqNums.AddedTypeCount++;
                seqNums.TableChangeSeqNum = GetNextSeqNum();
            }
            
            return ttt;
        }

        TypeSetTracker FindTypeSetTrackerForType(string typeName)
        {
            typeName = typeName.Sanitize();

            foreach (var tst in typeSetTrackerArray)
            {
                if (tst.setConfig.TypeNameMatchRuleSet.MatchesAny(typeName, valueToUseWhenSetIsNullOrEmpty: false))
                    return tst;
            }

            return defaultTypeSetTracker;      // we always use the default one if none other match
        }

        ulong seqNumGenerator = 0;

        /// <summary>
        /// increments the seqNumGenerator value and returns it.
        /// </summary>
        ulong GetNextSeqNum()
        {
            return ++seqNumGenerator;
        }

        private void InnerPublishSeqNumsIfNeeded()
        {
            if (!lastPublishedSeqNums.Equals(seqNums))
                seqNumsPublisher.Object = lastPublishedSeqNums = seqNums;
        }

        /// <summary>This is the reference copy for the sequence number information that this part publishes.</summary>
        E039TableSeqNums seqNums;

        /// <summary>This is a copy of the last sequence number that this part has published.</summary>
        E039TableSeqNums lastPublishedSeqNums;

        GuardedNotificationValueObject<E039TableSeqNums> seqNumsPublisher = new GuardedNotificationValueObject<E039TableSeqNums>(default(E039TableSeqNums));

        private class LinkTrackerPair
        {
            public LinkTrackerPair(E039Link link, string key = null)
            {
                Link = link;
                LinkKeyStr = key ?? link.Key;
                LinkFullIDAndKeyStr = link.FullIDAndKeyStr;
            }

            public E039Link Link;
            public ObjectTracker ObjectTracker { get; set; }

            public string LinkKeyStr { get; private set; }
            public string LinkFullIDAndKeyStr { get; private set; }

            public LinkTrackerPair OriginatingLTP { get; set; }
            public LinkTrackerPair SubordinateLTP { get; set; }
        }

        private class ObjectTracker
        {
            public ObjectTracker(E039ObjectID objID, IE039TableObserver tableObserver)
            {
                if (Object.ReferenceEquals(objID.TableObserver, tableObserver))
                    ObjID = objID;
                else
                    ObjID = new E039ObjectID(objID, tableObserver);
            }

            public void AssignUUID(string uuid)
            {
                obj.ID = ObjID = new E039ObjectID(ObjID.Name, ObjID.Type, uuid, ObjID.TableObserver);
            }

            public E039ObjectID ObjID { get; private set; }
            public E039ObjectFlags flags;

            public TypeTableTracker typeTableTracker;
            public TypeSetTracker typeSetTracker;

            public Dictionary<string, LinkTrackerPair> linkTrackerPairsFromOtherObjectsDictionary = new Dictionary<string, LinkTrackerPair>();
            public Dictionary<string, LinkTrackerPair> linkTrackerPairsToOtherObjectsDictionary = new Dictionary<string, LinkTrackerPair>();
            public bool rebuildLinksFromOtherObjectsList, rebuildLinksToOtherObjectsList;

            public bool touched;

            public E039Object obj;
            public NamedValueSet objAttributesWorking;

            public ulong lastPublishedSeqNum;
            public E039Object lastPublishedObj;        // used for serialization and IVA publication.  Each instance must be treated as being immutable (read only) once it has been published.
            public volatile IE039Object volatileLastPublishedIObj;

            public volatile InterlockedNotificationRefObject<IE039Object> objPublisher = new InterlockedNotificationRefObject<IE039Object>();

            public IValueAccessor<E039Object> objIVA = null;

            public long lastReferenceSetItemSeqNum;

            public void Publish(ulong assignedObjSeqNumValue, bool publicationTriggeredByLinkedObject = false)
            {
                ulong seqNum = assignedObjSeqNumValue;

                lastPublishedSeqNum = seqNum;

                if (touched || !publicationTriggeredByLinkedObject || volatileLastPublishedIObj == null)
                {
                    lastPublishedObj = new E039Object(obj);
                    volatileLastPublishedIObj = lastPublishedObj;
                }

                if (objPublisher != null)
                    objPublisher.Object = volatileLastPublishedIObj;

                if (objIVA != null)
                    objIVA.Set(volatileLastPublishedIObj);

                typeTableTracker.lastPublishedSeqNum = seqNum;
                typeSetTracker.lastPublishedSeqNum = seqNum;

                touched = false;
            }
        }

        private class TypeTableTracker
        {
            public string typeName;

            public IDictionaryWithCachedArrays<string, ObjectTracker> objectTrackerDictionary = new IDictionaryWithCachedArrays<string, ObjectTracker>();
            public ObjectTracker[] ObjectTrackerArray { get { return objectTrackerDictionary.ValueArray; } }

            public ulong lastPublishedSeqNum;

            public TypeSetTracker typeSetTracker;
        }

        private class TypeSetTracker
        {
            public TypeSetTracker(E039BasicTablePartConfig partConfig, E039TableTypeSetPersistSpecItem typeSetPersistSpecItem, Logging.IBasicLogger log)
            {
                setConfig = typeSetPersistSpecItem;

                if (setConfig.PersistObjFileRingConfig != null)
                    persistFileRingAdapter = partConfig.PersistStorageAdapterFactory(setConfig.PersistObjFileRingConfig, log);

                var referenceSetToUse = typeSetPersistSpecItem.ReferenceSet ?? partConfig.DefaultFallbackReferenceSet;
                var referenceHistorySetToUse = typeSetPersistSpecItem.ReferenceHistorySet ?? partConfig.DefaultFallbackReferenceHistorySet;

                if (referenceSetToUse != null || referenceHistorySetToUse != null)
                    referenceSetUpdateCollector = new ReferenceSetUpdateCollector() { referenceSet = referenceSetToUse, referenceHistorySet = referenceHistorySetToUse };

                saveHoldoffTimer = new QpcTimer() { TriggerInterval = typeSetPersistSpecItem.PersistWriteHoldoff, SelectedBehavior = QpcTimer.Behavior.ZeroTriggerIntervalRunsTimer };
            }

            public TypeTableTracker FindTypeTableTracker(string typeName, bool createIfNeeded = false)
            {
                typeName = typeName.Sanitize();

                TypeTableTracker ttt = null;

                if ((!typeNameToTypeTableTrackerDictionary.TryGetValue(typeName, out ttt) || ttt == null) && createIfNeeded)
                {
                    ttt = new TypeTableTracker() { typeName = typeName, typeSetTracker = this };
                    typeNameToTypeTableTrackerDictionary[typeName] = ttt;
                }

                return ttt;
            }

            public E039TableTypeSetPersistSpecItem setConfig;

            public IDictionaryWithCachedArrays<string, TypeTableTracker> typeNameToTypeTableTrackerDictionary = new IDictionaryWithCachedArrays<string, TypeTableTracker>();
            public TypeTableTracker[] TypeTableTrackerArray { get { return typeNameToTypeTableTrackerDictionary.ValueArray; } }

            public ulong lastPublishedSeqNum;
            public QpcTimer saveHoldoffTimer;

            public IPersistentStorage<E039PersistFileContents> persistFileRingAdapter;
            public E039PersistFileContents fileContents;

            public IBasicAction lastIssuedSaveAction;
            public ulong lastIssuedSaveActionSeqNum;

            public ulong lastSucceededSaveActionSeqNum;

            /// <summary>Returns true if there is a non-null save action and it is not complete</summary>
            public bool IsWriteActive { get { return (lastIssuedSaveAction != null) && !lastIssuedSaveAction.ActionState.IsComplete; } }

            /// <summary>Returns true if no Write action is active and the lastPublishedSeqNum is not equal to the last successfully saved sequence number.</summary>
            public bool IsWritePending { get { return (!IsWriteActive && lastPublishedSeqNum != lastSucceededSaveActionSeqNum); } }

            public ReferenceSetUpdateCollector referenceSetUpdateCollector;
        }

        private void ServicePersistWrites(bool startPendingWritesNow = false)
        {
            if (!BaseState.IsOnline || typeSetTrackerArray == null)
                return;

            foreach (var tst in typeSetTrackerArray)
            {
                if (tst.IsWriteActive || tst.persistFileRingAdapter == null)
                    continue;

                if (tst.lastIssuedSaveAction != null)
                {
                    if (tst.lastIssuedSaveAction.ActionState.Succeeded)
                    {
                        tst.lastSucceededSaveActionSeqNum = tst.lastIssuedSaveActionSeqNum;
                        Log.Emitter(Config.PersistFileWrittenMesgType).Emit("Persist File '{0}' written [wroteSeqNum:{1}, lastPublishedSeqNum:{1}]", tst.persistFileRingAdapter.LastObjectFilePath, tst.lastSucceededSaveActionSeqNum, tst.lastPublishedSeqNum);
                    }
                    else
                    {
                        Log.Emitter(Config.PersistFileWriteFailedMesgType).Emit("Persist File '{0}' write failed: {1}", tst.persistFileRingAdapter.LastObjectFilePath, tst.persistFileRingAdapter.LastExecption.ToString(ExceptionFormat.TypeAndMessage));
                    }

                    tst.lastIssuedSaveAction = null;
                }

                if (tst.IsWritePending)
                {
                    tst.saveHoldoffTimer.StartIfNeeded();

                    if (tst.saveHoldoffTimer.IsTriggered || startPendingWritesNow)
                        UpdateFileContentsAndOptionallyIssueWrite(tst, issueWrite: true);
                }
                else
                {
                    tst.saveHoldoffTimer.StopIfNeeded();
                }
            }
        }

        private void UpdateFileContentsAndOptionallyIssueWrite(TypeSetTracker tst, bool issueWrite = true)
        {
            // generate or update the fileContents as needed immediately prior to writing.  
            // This cannot be done while any prior write for this object tree is already in progress.

            int numTypes = tst.TypeTableTrackerArray.Length;

            if (tst.fileContents == null || tst.fileContents.TypeTableSet.SafeCount() != numTypes)
            {
                // rebuild fileContents (on first save attempt and/or after a type has been added to the tableset)

                E039PersistTypeTable[] typeTableArray = tst.TypeTableTrackerArray.Select(ttt => new E039PersistTypeTable() { Type = ttt.typeName, ObjectInstanceSet = new E039PersistObjectInstanceSet(ttt.ObjectTrackerArray.Select(ot => ot.lastPublishedObj)) }).ToArray();
                E039PersistTypeTableSet typeTableSet = new E039PersistTypeTableSet(typeTableArray);

                tst.fileContents = new E039PersistFileContents() { TypeTableSet = typeTableSet };
            }
            else
            {
                // perform inplace replacement of the objects to be serialized (to minimize garbage generation)
                for (int typeIdx = 0; typeIdx < numTypes; typeIdx++)
                {
                    var ttt = tst.TypeTableTrackerArray[typeIdx];
                    var persistTypeTable = tst.fileContents.TypeTableSet[typeIdx];

                    int numObjs = ttt.ObjectTrackerArray.Length;

                    // if this type's number of objects have changed then simply replace the ObjectInstanceSet (less code than manually growing or shrinking it)
                    if (persistTypeTable.ObjectInstanceSet.SafeCount() != numObjs)
                        persistTypeTable.ObjectInstanceSet = new E039PersistObjectInstanceSet(ttt.ObjectTrackerArray.Select(ot => ot.lastPublishedObj));
                    else
                    {
                        // otherwise just replace all of the current objects in the ObjectInstanceSet with the set of last published objects from the internal table set trees.
                        //  This is safe since the last published objects are all effectively immutable.
                        for (int objIdx = 0; objIdx < numObjs; objIdx++)
                            persistTypeTable.ObjectInstanceSet[objIdx] = ttt.ObjectTrackerArray[objIdx].lastPublishedObj;
                    }
                }
            }

            tst.persistFileRingAdapter.Object = tst.fileContents;

            if (issueWrite)
            {
                tst.lastIssuedSaveAction = persistHelperPart.SavePersist(tst.persistFileRingAdapter).StartInline();
                tst.lastIssuedSaveActionSeqNum = tst.lastPublishedSeqNum;
            }
        }

        private class PersistHelper : SimpleActivePartBase
        {
            public PersistHelper(string partID, E039BasicTablePartConfig config)
                : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true, disablePartBaseIVIUse: true, disablePartRegistration: true))
            {
                ActionLoggingReference.Config = config.PersistHelperActionLoggingConfig ?? config.ActionLoggingConfig ?? ActionLoggingConfig.Trace_Trace_Trace_Trace;
            }

            public IBasicAction SavePersist<TValueSetType>(IPersistentStorage<TValueSetType> persistStorageObject) 
                where TValueSetType : class, IPersistSequenceable, new()
            {
                return new BasicActionImpl(actionQ, () => 
                { 
                    persistStorageObject.Save(allowThrow: true); 
                    return string.Empty; 
                }, CurrentMethodName, ActionLoggingReference);
            }
        }
    }

    #endregion

    #region E039TableDeltaEventHandler (et. al.)
    #endregion

    #region E039PersistFileContents (may be used with externally provided DCA factories)

    [DataContract(Namespace = Constants.E039NameSpace)]
    public class E039PersistFileContents : IPersistSequenceable
    {
        public E039PersistFileContents()
        {
            TypeTableSet = new Details.E039PersistTypeTableSet();
        }

        [DataMember(Order = 10)]
        public ulong PersistedVersionSequenceNumber { get; set; }

        [DataMember(Order = 20)]
        public Details.E039PersistTypeTableSet TypeTableSet { get; set; }
    }

    #endregion

    #region Details (E039PersistTypeTableSet, E039PersistTypeTable, E039PersistObjectInstanceSet)

    namespace Details
    {
        /// <summary>PersistTypeTable instance container class, derived from List{E039PersistTypeTable}, used to allow DataContract/XML element naming to be customized.</summary>
        [CollectionDataContract(Namespace = Constants.E039NameSpace, ItemName = "TypeTable")]
        public class E039PersistTypeTableSet : List<E039PersistTypeTable>
        {
            /// <summary>Default constructor.  Creates empty list</summary>
            public E039PersistTypeTableSet()
            { }

            /// <summary>Copy constructor.  Creates list which references each of the E039BasicObjectImpl's produced by the given itemIter iterator/enumerable set.</summary>
            public E039PersistTypeTableSet(IEnumerable<E039PersistTypeTable> itemIter)
                : base(itemIter)
            { }
        }

        [DataContract(Namespace = Constants.E039NameSpace, Name = "TypeTable")]
        public class E039PersistTypeTable
        {
            public E039PersistTypeTable() 
            {
                ObjectInstanceSet = new E039PersistObjectInstanceSet();
            }

            [DataMember(Order = 10)]
            public string Type { get; set; }

            [DataMember(Order = 20, Name="ObjTable")]
            public E039PersistObjectInstanceSet ObjectInstanceSet { get { return _objectInstanceSet ?? (_objectInstanceSet = new E039PersistObjectInstanceSet()); } set { _objectInstanceSet = value; } }
            private E039PersistObjectInstanceSet _objectInstanceSet = null;
        }

        /// <summary>Object instance container class, derived from List{E039BasicObjectImpl}, used to allow DataContract/XML element naming to be customized.</summary>
        [CollectionDataContract(Namespace = Constants.E039NameSpace, ItemName = "ObjInst")]
        public class E039PersistObjectInstanceSet : List<E039Object>
        {
            /// <summary>Default constructor.  Creates empty list</summary>
            public E039PersistObjectInstanceSet() 
            { }

            /// <summary>Copy constructor.  Creates list which references each of the E039BasicObjectImpl's produced by the given itemIter iterator/enumerable set.</summary>
            public E039PersistObjectInstanceSet(IEnumerable<E039Object> itemIter) 
                : base(itemIter) 
            { }
        }
    }

    #endregion

    #region E039ObjectObserverWithInfoExtraction, IE039DerivedObjectObserverHelper, E039ObjectDerivedObjectInfoExtractionHelper

    /// <summary>
    /// This is a base class that is used to help create Observer pattern derived types for different types of E039Object.  
    /// </summary>
    public class E039ObjectObserverWithInfoExtraction<TObjectInfoType> : ISequencedObjectSourceObserver<IE039Object>
    {
        /// <summary>
        /// Standard constructor.  If <paramref name="infoFactoryDelegate"/> is null then this object will assign Info to default{TObjectInfoType} each time this observer is updated.
        /// </summary>
        public E039ObjectObserverWithInfoExtraction(ISequencedObjectSource<IE039Object, int> objPublisher, Func<IE039Object, TObjectInfoType> infoFactoryDelegate = null)
        {
            AutoUpdateOnCast = true;

            ObjPublisher = objPublisher;

            if (ObjPublisher != null)
                objObserver = new SequencedRefObjectSourceObserver<IE039Object, int>(ObjPublisher);

            this.infoFactoryDelegate = infoFactoryDelegate ?? (obj => default(TObjectInfoType));

            Update(forceUpdate: true);
        }

        /// <summary>
        /// "Copy" constructor.  
        /// <para/>Note: this constructor implicitly Updates the constructed observer so it may not give the same property values (Info, Object, ID) if a new object has been published since the <paramref name="other"/> observer was last Updated.
        /// <para/>Note: this copy constructor does not copy the <paramref name="other"/>'s UpdateAndGetObjectUpdateActionArray.  Any desired object update actions for this new observer must be added explicitly.
        /// </summary>
        protected E039ObjectObserverWithInfoExtraction(E039ObjectObserverWithInfoExtraction<TObjectInfoType> other)
            : this((other != null) ? other.ObjPublisher : null, (other != null) ? other.infoFactoryDelegate : null)
        { }

        /// <summary>
        /// Debugging and logging helper method
        /// </summary>
        public override string ToString()
        {
            string isUpdateNeededStr = IsUpdateNeeded ? " [UpdateIsNeeded]" : "";

            if (objObserver == null)
                return "Obs{0} [Null]".CheckedFormat(isUpdateNeededStr);
            else if (Object == null)
                return "Obs{0} [Empty]".CheckedFormat(isUpdateNeededStr);
            else
                return "Obs{0} {1}".CheckedFormat(isUpdateNeededStr, Info);
        }

        /// <summary>For derived types that support implicit or explicit cast operators this property determines if that instance will automatically Update itself on use of any such cast operator (or not).  <para/>Defaults to true</summary>
        public bool AutoUpdateOnCast { get; set; }

        /// <summary>Gives access to the object publisher instance from which this observer was constructed.  Used during copy construction.</summary>
        public ISequencedObjectSource<IE039Object, int> ObjPublisher { get; private set; }

        /// <summary>This gives the SequenceRefObjectSourceObserver that is used by this aggregate observer.  NOTE: This field may be null.</summary>
        protected SequencedRefObjectSourceObserver<IE039Object, int> objObserver;

        /// <summary>This is the factory delegate function that was provided during original construction.  It is used to generate the Info object's contents after each Update operation.</summary>
        protected Func<IE039Object, TObjectInfoType> infoFactoryDelegate;

        /// <summary>Gives the last Object instance that was observered from the publisher when this object was last Updated, or null if there is no such object.</summary>
        public IE039Object Object { get { return (objObserver != null) ? objObserver.Object : null; } }

        /// <summary>Gives the E039ObjectID of the last Object instance that was observed from the publisher, or E039ObjectID.Empty if there is no such object.</summary>
        public E039ObjectID ID { get { return (Object ?? E039Object.Empty).ID; } }

        /// <summary>Gives the <typeparamref name="TObjectInfoType"/> object produced by the infoFactoryDelegate</summary>
        public TObjectInfoType Info { get; protected set; }

        /// <summary>Gives the QpcTimeStamp from the last update that generated new Info contents (due to the update being needed or being forced).</summary>
        public QpcTimeStamp LastUpdateTimeStamp { get; protected set; }

        /// <summary>get/set: equivalent to the IsUpdateNeeded flag for the underlying sequenced object source observer.  returns true when source's seq number does not match seq number during last update.  May be set to true to indicate that an update is needed.</summary>
        public virtual bool IsUpdateNeeded 
        {
            get { return (objObserver != null) && objObserver.IsUpdateNeeded; } 
            set { if (objObserver != null) objObserver.IsUpdateNeeded = value; } 
        }

        /// <summary>This method is called by dervied types when casting to obtain contents.  If AutoUpdateOnCast is true and the object IsUpdateNeeded then this method calls Update(false).</summary>
        protected void UpdateOnCastIfNeeded()
        {
            if (IsUpdateNeeded && AutoUpdateOnCast)
                Update(forceUpdate: false);
        }

        bool ISequencedSourceObserver.Update()
        {
            return this.Update(forceUpdate: false);
        }

        /// <summary>
        /// Updates the Object and Info from the publisher.  
        /// <paramref name="forceUpdate"/> may be used to force updating the Info and any added update actions even when the underlying sequenced object source observer has already observed the most recently published object instance.
        /// Returns true if the underlying sequenced object source obsrver's Update method indicated that the update was needed.
        /// </summary>
        public virtual bool Update(bool forceUpdate = false)
        {
            bool didUpdate = (objObserver != null) ? objObserver.Update() : false;

            if (didUpdate || forceUpdate)
            {
                var o = Object;

                Info = infoFactoryDelegate(o);

                LastUpdateTimeStamp = QpcTimeStamp.Now;

                foreach (var updateAction in UpdateAndGetObjectUpdateActionArray())
                    updateAction(o);
            }

            return didUpdate;
        }

        private List<Action<IE039Object>> objectUpdateActionList = null;
        private Action<IE039Object>[] objectUpdateActionArray = null;
        private Action<IE039Object>[] UpdateAndGetObjectUpdateActionArray()
        {
            return (objectUpdateActionArray ?? (objectUpdateActionArray = objectUpdateActionList.SafeToArray()));
        }

        /// <summary>Allows the caller to add a set of object update action's to this observer</summary>
        public virtual E039ObjectObserverWithInfoExtraction<TObjectInfoType> Add(params Action<IE039Object>[] objectUpdateActionParamsArray)
        {
            if (objectUpdateActionParamsArray != null)
            {
                objectUpdateActionArray = null;
                (objectUpdateActionList ?? (objectUpdateActionList = new List<Action<IE039Object>>())).AddRange(objectUpdateActionParamsArray);

                objectUpdateActionParamsArray.DoForEach(objectUpdateAction => objectUpdateAction(Object));
            }

            return this;
        }

        /// <summary>Allows the caller to add a set of object observer helper instances to this observer.  Internally adds object update actions for each of the curresponding helper's UpdateFrom methods.</summary>
        public virtual E039ObjectObserverWithInfoExtraction<TObjectInfoType> Add(params IE039DerivedObjectObserverHelper[] objectObserverHelpersParamsArray)
        {
            return Add(objectObserverHelpersParamsArray.Select(helper => (Action<IE039Object>) helper.UpdateFrom).ToArray());
        }

        #region region ISequencedObjectSourceObserver<IE039Object>, ISequencedSourceObserver remaining methods and properties

        /// <summary>Returns true if the sequence number has been incremented or has been explicitly set</summary>
        public bool HasBeenSet { get { return (objObserver != null) && objObserver.HasBeenSet; } }

        /// <summary>Returns the current sequence number.  May return zero if sequence number is set to skip zero and Increment is in progress on another thread.</summary>
        public int SequenceNumber { get { return (objObserver != null) ? objObserver.SequenceNumber : 0; } }

        /// <summary>Returns the current sequence number read as a volatile (no locking) - May return zero if sequence number is set to skip zero and Increment is in progress on another thread</summary>
        public int VolatileSequenceNumber { get { return (objObserver != null) ? objObserver.VolatileSequenceNumber : 0; } }

        ISequencedObjectSourceObserver<IE039Object> ISequencedObjectSourceObserver<IE039Object>.UpdateInline()
        {
            Update();
            return this;
        }

        ISequencedSourceObserver ISequencedSourceObserver.UpdateInline()
        {
            Update();
            return this;
        }

        #endregion
    }

    /// <summary>Interface supported by objects that are usable with <seealso cref="E039ObjectObserverWithInfoExtraction{TObjectInfoType}"/> class.</summary>
    public interface IE039DerivedObjectObserverHelper
    {
        /// <summary>This methods is called each time the hosting observer's UpdateMethod is called when needed, or is called with the forceUpdate parameter set to true.  This method is passed the most recently observed Object.</summary>
        void UpdateFrom(IE039Object updateFromObject);
    }

    /// <summary>Helper class that can be used to attach additional types of Info object observers to an existing <seealso cref="E039ObjectObserverWithInfoExtraction{TObjectInfoType}"/> object</summary>
    public class E039ObjectDerivedObjectInfoExtractionHelper<TDerivedObjectInfoType> : IE039DerivedObjectObserverHelper
    {
        /// <summary>Constructor.  Caller provides the <paramref name="infoFactoryDelegate"/> that is to be used to generate the Info property contents from each newly observed object.</summary>
        public E039ObjectDerivedObjectInfoExtractionHelper(Func<IE039Object, TDerivedObjectInfoType> infoFactoryDelegate)
        {
            this.infoFactoryDelegate = infoFactoryDelegate ?? (obj => default(TDerivedObjectInfoType));
        }

        private Func<IE039Object, TDerivedObjectInfoType> infoFactoryDelegate;

        /// <summary>Gives the <typeparamref name="TDerivedObjectInfoType"/> generated using the constructed infoFactoryDelegate from the last observed object.</summary>
        public TDerivedObjectInfoType Info { get; private set; }

        void IE039DerivedObjectObserverHelper.UpdateFrom(IE039Object updateFromObject)
        {
            Info = infoFactoryDelegate(updateFromObject);
        }
    }

    #endregion
}
