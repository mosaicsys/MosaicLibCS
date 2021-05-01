//-------------------------------------------------------------------
/*! @file Interconnect/Sets.cs
 *  @brief Defines a group of classes and interfaces that are used to supported use to interconnect sources and observers for Sets of objects.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2015 Mosaic Systems Inc.
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;

using MosaicLib.Modular.Common;
using MosaicLib.Utils;

// Modular.Interconnect is the general namespace for tools that help interconnect Modular Parts without requiring that that have pre-existing knowledge of each-other's classes and instances.
// This file contains the definitions for the underlying Modular.Interconnect.Sets namespace which is one of the areas of functionality that helps in this regard.
//  
// Modular.Interconnect.Sets provides a group of tools that are used to define containers for storing and propagating sets of objects, typically immutable ones.
//
// The basic concept here is a that an Interconnect Set is a way to group a sequence of client provided objects and to track the client's additions to the set and removals from the set
// in such a way that these changes can be easily, and efficiently, replicated using set deltas (an externally usable representation of such removals and additions).
//
// In order to accomplish this each Set will only offer limited means to change it.  The client can add new items on the end of the set (append) and remove items from anywhere in the set.  
// The client cannot replace or add items in the middle of the set.
//
// Sets are heavily Generics based in that the client needs to specify the type of the items that are placed in a given set for most concrete set types.  
// The client should only use types with sets where the type supports immutability and the client should only add items to the set that are immutable.  
// The Set implementations do not actually require this and since they do not look inside of the client provided objects they do not direcly care if the client adds mutable objects to the set.
//
// There are a few types of set implementation objects.  The first is the "Reference" type of set.  This is a concrete object that is used by a client to define a set contents, to optionally
// support making ongoing changes to the set contents.  Reference sets represent the original, canonical, version of a Set and is the only one that a client can directly manipulates the 
// contents of.  The second is the "Copy" set.  A copy set can be made from any other type of Interconnect Set and it will represent a snapshot into the contents of that set, 
// taken at the time it was copied.  Copy set contents do not chnage after the copy has been made, even if the source of the copy's contents change thereafter.  Finally there is the
// "Tracking" set.  This is a hybird between a "Reference" set and a "Copy" set.  Tracking sets are intended to capture the logic required to convert between set representation and set delta 
// representation.  Set deltas are the primary means used to replicate set contents from one tracking set to another, typically through some IPC means such as Interconnect.Remoting.
// This information is available to the client in the SetType property.
//
// Each Reference set is given a SetID that includes a Name and a UUID that is assigned when the set is created.  
// All Copies of a given Reference set, Tracking or otherwise, will carry the same SetID either by shared instance or by content.
//
// Reference sets can be Changeable or Fixed.  Reference sets become fixed by calling their SetFixed method.  
// Fixed Refernce sets are a special case in that the system knows that their contents can never change.
//
// Reference and Tracking sets can have their UpdateState be InProgress or Complete.  
// When a TrackingSet has not fully processed all of the deltas from the last copy set that it was updating from it will show that the UpdateState is InProgress.
// When it has completed processing of all such deltas it will give the UpdateState as Complete.  Setting the Reference set's UpdateState to InProgress will force all resulting
// copy and tracking states to keep indicating that they are InProgress even if they have propagated all deltas.
//
// Each set has a SeqNum.  Reference sets increment this sequence number each time the set contents have been changed.  Tracking sets look at the this information along with other
// information about any incremental updates that are in progress to determine if they still need to be serviced/updated at any given time.  Typeically a sequence number delta
// indicates that the Tracking set needs to start another incremental update cycle.
//
// Each set represents an ordered group of items, each one of which caries a unique Int64 sequence number.  
// The root Reference set is generally the only set that assigned these sequence numbers to an item.
// The ordered group is confined to allow new items to be added/appended onto the end of the sequence, each with a newly assigned Int64 sequence number.  
// Items may be removed from the set in any order but new ones can only be added to the end of the set and items cannot be replaced or inserted in the middle.
// Combined this makes certain that the list of item sequence numbers is always monotonically increasing from one end of the sequence to the other.  There may be gaps
// in the middle if the client removes items from the middle but the sequence numbers will never be out of order from one end to the other.
// These characteristics allows the logic here to compare two sets that represent differnt points in time or the reference and/or partially applied updates so that
// the logic can generate a delta set by direct linear comparison between the two sets.
//
// Typically each Reference set is given a maximum element count (Capacity) that it must stay within.  If more items are appended to the Reference set than will fit then the Reference set
// will automatically Remove the required number of elements from the head (start) of the set to keep the set within the configured size limit.
// This pattern also gives the most common add/remove pattern for large sets where items are added to the end and removed from the beginning but the block in the middle
// is simply shifted over.  This information will be used to optimize the delta generation logic by allowing it to know when it can skip over large numbers of elements in the middle 
// of the set without actually scanning through them each time it generates a delta set.
//
// Concept of a AdjustableTrackingSet - a TrackingSet that can accumulate a group of items into a set that is larger than the original capacity of the set that it is tracking (or smaller).  
// It does this by only removing items when it gets full.  It also needs to handle set reset, either by resetting itself or by forcing new items to be appended by using an incoming 
// ItemSeqNum offset.
//
// Generally intended remote subscription model:
// A) A Reference set is generic (templatized) on a specific item type.
// B) All of the Tracking set implementation objects that are created from a reference set are also generic on the same type as the reference set.
// C) Tracking sets know if the type they are generic on support being ISerializable or if the corresponding type has been marked with the DataContract attributes.  
//      Tracking sets that have not been so marked cannot be used to serialize or deserialize the SetDelta objects that they are used to create.
//      When such a TrackingSet knows that its item type is serializable (data contract or otherwise), it will create a simple JSON data contract serialization helper
//      which can be used to generate serialized versions of SetDelta instances from this set into a stream and which can be used to deserialize such SetDelta instances from
//      a suitable stream.
// D) When used with Remoting, the Client side Set replication remoting message stream tool creates a Tracking set of the client indicated type and SetID and locally registers it as a reference set
//      with the indicates ISetsInterconnection instance (usually Sets.Instance).
// E) At the Server side when the message stream is initialized it finds the registered reference set from the given SetID and creates a new tracking set to track it with.
// F) On each available instance it generates a SetDelta using this local tracking set, serializes it into a remoting message and then sends the remoting message to the client side.
// G) The Client side receives each such serialized SetDelta message, and applies them to the locally created tracking set as if it were a locally created reference set.
//
// in this way the contents of an original reference set can be incrementally replicated to a remote client where additional logic (such as a WPF Gui) can subscribe to,
// and incrementally process the set contents.  For example this technique is currently in use to capture log messages into a reference set using the SetLogMessageHandler LMH type.
// Then the GUI can subscribe (locally or remotely) to the resulting set and reconstruct the set of log messages for display.  In addition the AdjustableTrackingSet can similarly
// be used in combination with an underlying TrackingSet to implement common filtering techniques.  
// 
// It is also worth noting here that single threaded GUIs can use Tracking sets to convert asynchronous updates to a reference set into synchronously deliverable set contents and 
// set delta contents in that the existing operation of a tracking set in generating such set deltas is pull driven using a client's thread.
//
// This portion of Modular.Interconnect envisions that such set use models will also be useful for many other replication, and subscription types of uses, especially involving
// GUI and visualization of such set contents.

namespace MosaicLib.Modular.Interconnect.Sets
{
    #region ISetsInterconnection interface

    /// <summary>
    /// This interface defines the publically usable interface for logic instances that can be used by client code to register sets for others to use
    /// and to find sets that others have registered.
    /// </summary>
    public interface ISetsInterconnection
    {
        /// <summary>
        /// Registers the given ITrackableSet <paramref name="set"/> in both the mapping tables for name based lookup and for UUID based lookup.
        /// If there is already a set of the same name in the table then the <paramref name="howToHandleDuplicateSetNames"/> is used to determine what to do.
        /// The UUIDs in the set's SetID are required to be unique.  As such this method never replaces a previously registered set by UUID without the explicit use of the UnregisterSet method beforehand.
        /// <para/>NOTE: generally ReferenceSets and TrackingSets which have been constructed to make use of an internal mutex are reasonable candidates for registration for use by other external and loosly coupled objects.
        /// </summary>
        ISetsInterconnection RegisterSet(ITrackableSet set, DuplicateSetNameRegistrationBehavior howToHandleDuplicateSetNames = DuplicateSetNameRegistrationBehavior.Add);

        /// <summary>
        /// Unregisters the given <paramref name="set"/> from both the mapping tables for name based lookup and for UUID based lookup.  If the set is not present in one or both of these tables then that table will no be modified.
        /// </summary>
        ISetsInterconnection UnregisterSet(ITrackableSet set);

        /// <summary>
        /// Attempts to find and return the registered ITrackable set that has the given <paramref name="uuid"/> and return it.  If no such set is found then this method returns null.
        /// </summary>
        ITrackableSet FindSetByUUID(string uuid);

        /// <summary>
        /// Attempts to find and return the group of registered ITrackable sets that have the given <paramref name="name"/> and return it.  If no such set is found then this method returns an empty array.
        /// </summary>
        ITrackableSet[] FindSetsByName(string name);
    }

    /// <summary>
    /// This enumeration is used to define how the RegisterPart method should handle cases where there is already another part of the same name in the table.
    /// <para/>None (0), Replace
    /// </summary>
    public enum DuplicateSetNameRegistrationBehavior : int
    {
        /// <summary>The new registration will be ignored if one or more other sets have already been registered with the same name.  This case will generate a log message however.</summary>
        None = 0,

        /// <summary>The new registration will cause the table entry to be updated to refer only to the newly given set rather than to any prior ones.  This case will generate a log message as well.</summary>
        Replace,

        /// <summary>The new registration will cause the table entry to be added to the list of sets that share this given name.  This case will generate a log message as well.</summary>
        Add,
    }

    #endregion

    #region ISetsInterconnection extension methods

    /// <summary>partial static class contains (more) Extension Methods</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Attempts to find and return the first set for the given setID.  
        /// If the setID has a non-empty UUID that has been registered already then this method will find and return it.
        /// else this method will request all of the sets with the given setID.Name and will return the first of them
        /// otherwise this method return null.
        /// </summary>
        public static ITrackableSet FindFirstSet(this SetID setID, ISetsInterconnection isi = null)
        {
            isi = isi ?? Sets.Instance;

            ITrackableSet set = null;

            if (!setID.UUID.IsNullOrEmpty())
                set = isi.FindSetByUUID(setID.UUID);
            else
                set = isi.FindSetsByName(setID.Name).FirstOrDefault();

            return set;
        }
    }

    #endregion

    #region Interconnection Sets class and singleton

    /// <summary>
    /// "namespace" class, with singleton Instance property, used to handle registration (and unregistration) of ITrackableSets.  
    /// Provides Find methods to allow a client to find registered sets by the set name or the set UUID.
    /// </summary>
    public class Sets : ISetsInterconnection
    {
        #region Singleton Instance

        /// <summary>
        /// AutoConstruct singleton Instance of the Sets class that may be used when an explicit Sets instance is not provided.
        /// </summary>
        public static ISetsInterconnection Instance { get { return singletonHelper.Instance; } }
        private static SingletonHelperBase<ISetsInterconnection> singletonHelper = new SingletonHelperBase<ISetsInterconnection>(() => new Sets());

        #endregion

        #region ISetsInterconnection interface implementation

        /// <summary>
        /// Registers the given ITrackableSet <paramref name="set"/> in both the mapping tables for name based lookup and for UUID based lookup.
        /// If there is already a set of the same name in the table then the <paramref name="howToHandleDuplicateSetNames"/> is used to determine what to do.
        /// The UUIDs in the set's SetID are required to be unique.  As such this method never replaces a previously registered set by UUID without the explicit use of the UnregisterSet method beforehand.
        /// <para/>NOTE: generally ReferenceSets and TrackingSets which have been constructed to make use of an internal mutex are reasonable candidates for registration for use by other external and loosly coupled objects.
        /// </summary>
        public ISetsInterconnection RegisterSet(ITrackableSet set, DuplicateSetNameRegistrationBehavior howToHandleDuplicateSetNames = DuplicateSetNameRegistrationBehavior.Add)
        {
            if (set != null && set.SetID != null)
            {
                string setUUID = set.SetID.UUID;
                string setName = set.SetID.Name;

                lock (mutex)
                {
                    if (!setUUID.IsNullOrEmpty())
                        uuidToSetDictionary[setUUID] = set;

                    if (!setName.IsNullOrEmpty())
                    {
                        List<ITrackableSet> listOfSets = nameToListOfSetsDictionary.SafeTryGetValue(setName);

                        if (listOfSets == null)
                            nameToListOfSetsDictionary[setName] = (listOfSets = new List<ITrackableSet>());

                        switch (howToHandleDuplicateSetNames)
                        {
                            case DuplicateSetNameRegistrationBehavior.None:
                                if (listOfSets.Count == 0)
                                    listOfSets.Add(set);
                                break;
                            case DuplicateSetNameRegistrationBehavior.Replace:
                                if (listOfSets.Count > 0)
                                    listOfSets.Clear();
                                listOfSets.Add(set);
                                break;
                            case DuplicateSetNameRegistrationBehavior.Add:
                                listOfSets.Add(set);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Unregisters the given <paramref name="set"/> from both the mapping tables for name based lookup and for UUID based lookup.  If the set is not present in one or both of these tables then that table will no be modified.
        /// </summary>
        public ISetsInterconnection UnregisterSet(ITrackableSet set)
        {
            if (set != null && set.SetID != null)
            {
                string setUUID = set.SetID.UUID;
                string setName = set.SetID.Name;

                lock (mutex)
                {
                    if (!setUUID.IsNullOrEmpty())
                        uuidToSetDictionary.Remove(setUUID);

                    if (!setName.IsNullOrEmpty())
                    {
                        List<ITrackableSet> listOfSets = nameToListOfSetsDictionary.SafeTryGetValue(setName);
                        if (listOfSets != null)
                        {
                            listOfSets.Remove(set);

                            if (listOfSets.Count == 0)
                                nameToListOfSetsDictionary.Remove(setName);
                        }
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Attempts to find and return the registered ITrackable set that has the given <paramref name="uuid"/> and return it.  If no such set is found then this method returns null.
        /// </summary>
        public ITrackableSet FindSetByUUID(string uuid)
        {
            ITrackableSet set;

            lock (mutex)
            {
                set = uuidToSetDictionary.SafeTryGetValue(uuid);
            }

            return set;
        }

        /// <summary>
        /// Attempts to find and return the group of registered ITrackable sets that have the given <paramref name="name"/> and return it.  If no such set is found then this method returns an empty array.
        /// </summary>
        public ITrackableSet[] FindSetsByName(string name)
        {
            ITrackableSet[] setsArray;

            lock (mutex)
            {
                setsArray = nameToListOfSetsDictionary.SafeTryGetValue(name).SafeToArray();
            }

            return setsArray;
        }

        #endregion

        #region private fields and methods

        public Sets() { }

        private readonly object mutex = new object();
        private Dictionary<string, ITrackableSet> uuidToSetDictionary = new Dictionary<string, ITrackableSet>();
        private Dictionary<string, List<ITrackableSet>> nameToListOfSetsDictionary = new Dictionary<string, List<ITrackableSet>>();

        #endregion
    }

    #endregion

    #region Primary generic ISet related interfaces: ISet, ISet<TObjectType>, ITrackableSet, IReferenceSet<TObjectType>, ITrackingSet, ITrackingSet<TObjectType>

    /// <summary>Basic interface that is implemented by all Set object types in this file/namespace.  Also includes object valued index and ToArray methods.  Implements ICollection.</summary>
    public interface ISet 
        : ICollection
    {
        /// <summary>Gives the SetID of this set which includes the Name and UUID</summary>
        SetID SetID { get; }

        /// <summary>Gives the SetType of this set.</summary>
        SetType SetType { get; }

        /// <summary>Gives the SetChangeability of this set.</summary>
        SetChangeability Changeability { get; }

        /// <summary>Gives the UpdateState of this set.</summary>
        UpdateState UpdateState { get; }

        /// <summary>Returns true if the UpdateSet is UpdateState.InProgess</summary>
        bool IsUpdateInProgress { get; }

        /// <summary>Gives the SetType of the source set for this set, or of this set if it is a source.</summary>
        SetType SourceSetType { get; }

        /// <summary>Gives the SetChangeability of the source set for this set, or of this set it is a source.</summary>
        SetChangeability SourceChangeability { get; }

        /// <summary>Returns true if the SourceChangeability is SetChangeability.Fixed</summary>
        bool IsSourceFixed { get; }

        /// <summary>For TrackingSets, gives the string error code for the last operation on this set, or string.Empty if the last operation succeeded.</summary>
        string ResultCode { get; }

        /// <summary>Gives the set's most recently generated sequence number (dervied from the source set for Copy sets).  This sequence is unique to each set.  Tracking sets maintain their own SeqNum series that is not directly derived from their source set.</summary>
        Int64 SeqNum { get; }

        /// <summary>Gives the SeqNumRangeInfo for the current contents of this set.</summary>
        SeqNumRangeInfo ItemListSeqNumRangeInfo { get; }

        /// <summary>Indexed getter that returns the indexed item as an object.</summary>
        object this[int index] { get; }

        /// <summary>Converts the Set's contents to an Array of objects and returns it.</summary>
        object[] ToArray();

        /// <summary>Generates and returns the contents of this set as an array of ItemAndSeqNum{object} elements.</summary>
        ItemAndSeqNum<object>[] ToItemAndSeqNumArray();

        /// <summary>Gives the currently configured capacity for this set.</summary>
        int Capacity { get; }
    }

    /// <summary>This Generic interface gives a typed extension to the ISet interface and adds the ICollection{TObjectType} interface.  It also adds TObjectType specific means to access the set.</summary>
    public interface ISet<TObjectType>
        : ISet, ICollection<TObjectType>
    {
        /// <summary>Generic indexed getter that returns the indexed TObjectType item.</summary>
        new TObjectType this[int index] { get; }

        /// <summary>Generates and returns the contents of this set as an array of TObjectType items.</summary>
        new TObjectType[] ToArray();

        /// <summary>Generates and returns the contents of this set as an array of ItemAndSeqNum{TObjectType} elements.</summary>
        new ItemAndSeqNum<TObjectType>[] ToItemAndSeqNumArray();

        /// <summary>Generates and returns a Copy type set derived from the current set.</summary>
        SetBase<TObjectType> CreateCopy();

        /// <summary>Returns the count of the number of items that are currently in the set.</summary>
        new int Count { get; }
    }

    /// <summary>
    /// This struct allows the caller to obtain a set of structs that give the caller access to both the individual items and each of the item sequence numbers that they have been assigned in the set.
    /// </summary>
    public struct ItemAndSeqNum<TObjectType>
    {
        /// <summary>Gives the item's sequence number in the set which was assigned when the item was added to the set.</summary>
        public long ItemSeqNum { get; set; }

        /// <summary>Gives the item in this pair</summary>
        public TObjectType Item { get; set; }

        /// <summary>Debug and logging helper method</summary>
        public override string ToString()
        {
            if (Item != null)
                return "ItemSeqNum:{0} Item:[{1}]".CheckedFormat(ItemSeqNum, Item);
            else
                return "ItemSeqNum:{0} [ItemIsNull]".CheckedFormat(ItemSeqNum);
        }
    }

    /// <summary>This interface extends ISet.  It adds a method that allows the caller to create correctly typed ITrackingSet objects from the current set (typically an IReferenceSet or another ITrackingSet).</summary>
    public interface ITrackableSet : ISet
    {
        /// <summary>Generate a ITrackingSet (which can be casted to the underlying implementation's ITrackingSet{TObjectType}) from the current ITrackableSet (typically an IReferenceSet or another ITrackingSet).</summary>
        ITrackingSet CreateTrackingSet();
    }

    /// <summary>This Generic interface exents the ISet{TObjectType} interface and adds the ITrackableSet interface.  It adds methods that are used to unregiser the set and to perform TObjectType specific changes to the set.</summary>
    public interface IReferenceSet<TObjectType> 
        : ISet<TObjectType>, ITrackableSet
    {
        /// <summary>If the set was registered when it was created.  This method can be used to ask it to unregister itself with the Sets object that it originally registered itself with.</summary>
        void UnregisterSelf();

        /// <summary>This method adds (appends) one or more TObjectType items to the set contents.  If this operation would cause the set content size to exceed its Capacity limit then an appropriate number of the prior contents will be removed to make space for the incoming items.</summary>
        IReferenceSet<TObjectType> Add(params TObjectType[] addItemsArray);

        /// <summary>Removes the first element from the current contents for which the given predicate returns true.  If the predicate is given as null then this simply removes the first element.</summary>
        bool RemoveFirst(Func<TObjectType, bool> matchPredicate);

        /// <summary>Removes all items from the set for which the given predicate returns true.  If the predicate is given as null then this removes all current items from the set.</summary>
        bool RemoveAll(Func<TObjectType, bool> matchPredicate);

        /// <summary>Removes the item at the given index in the set.</summary>
        void RemoveAt(int index);

        /// <summary>
        /// Combination method.  If a non-null <paramref name="itemsToRemoveMatchPredicate"/> predicate is given then this method first removes all items from the set for which the given predicate returns true.  
        /// Then this method adds (appends) the given set of <paramref name="itemsToAdd"/> to the set, performing additional capacity limit triggered pruning as required.
        /// Pass null for the <paramref name="itemsToRemoveMatchPredicate"/> allows this method to be used as a form of AddRange
        /// </summary>
        void RemoveAndAdd(Func<TObjectType, bool> itemsToRemoveMatchPredicate, params TObjectType[] itemsToAdd);

        /// <summary>
        /// Removes the given set of items (referenced by their sequence numbers) and adds the given set of <paramref name="itemsToAdd"/>.  
        /// If any items are added then this method returns the sequence number of the first added item.  
        /// Following added item sequence numbers are sequentially assigned.
        /// If no items are added then this method returns the sequence number that will be assigned to the next item that is added to the set.
        /// <para/>It is highly recommended that the <paramref name="removeSeqNumSetArray"/> be given in sorted order.
        /// </summary>
        Int64 RemoveBySeqNumsAndAddItems(Int64[] removeSeqNumSetArray, params TObjectType[] itemsToAdd);

        /// <summary>Tells the set to change its SetChangability to be Fixed.</summary>
        ISet<TObjectType> SetFixed();

        /// <summary>Provides a Generic ITrackingSet{TObjectType} factory method.  Returned value is an ITrackingSet{TObjectType} that can be used to track this reference set asynchronosly.</summary>
        new ITrackingSet<TObjectType> CreateTrackingSet();
    }

    /// <summary>
    /// Extends ITrackableSet and adds the INotifyPropertyChanged and INotifyCollectionChanged interfaces.  
    /// Adds methods that are used to determine when the ITrackingSet needs to be updated, to perform these update cycles either directly from the source set for this tracking set,
    /// or as a side effect of applying the ISetDelta's from a matching ITrackingSet.  ITrackingSets can be used to update ISetDelta objects by handling serialization and deserialization of
    /// the delta objects in the ISetDelta object.  This facility is generally used when reflecting an ITrackingSet's contents through some form of IPC capable conduit.
    /// </summary>
    public interface ITrackingSet 
        : ITrackableSet, INotifyPropertyChanged, INotifyCollectionChanged
    {
        /// <summary>Returns true if the source set for this set has been updated since this set last finished updating, or if the last update iteration was only partially complete.</summary>
        bool IsUpdateNeeded { get; }

        /// <summary>
        /// Performs an update iteration.  When non-zero maxDeltaItemCount is used to specify the maximum number of incremental set changes that can be applied per iteration.  When zero, no such limit is applied.
        /// Supports call chaining.
        /// </summary>
        ITrackingSet PerformUpdateIteration(int maxDeltaItemCount = 0);

        /// <summary>
        /// Performs an update iteration and optionally generates the corresponding ISetDelta object.
        /// When non-zero <paramref name="maxDeltaItemCount"/> is used to specify the maximum number of incremental set changes that can be applied per iteration.
        /// When <paramref name="generateSetDelta"/> is false the method will return null.  When <paramref name="generateSetDelta"/> is true, the method will attempt to create and return an ISetDelta to represent the incremental update deltas.  If there are no such changes the method returns null.
        /// </summary>
        /// <exception cref="SetUseException">When called on an <seealso cref="AdjustableTrackingSet{TObjectType}"/> the instance will throw this exception type</exception>
        ISetDelta PerformUpdateIteration(int maxDeltaItemCount, bool generateSetDelta);

        /// <summary>
        /// Accepts an ISetDelta object that was generated by an external ITrackingSet of the same object type as this one and applies the delta to this set.
        /// Supports call chaining.
        /// </summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or the given setDelta does not already contain deserialized items and TObjectType is not known to support use with DataContract serialization</exception>
        ITrackingSet ApplyDeltas(ISetDelta setDelta);

        /// <summary>
        /// Generate and return a SetDelta that gives the entire current contents of the tracking set in a single set delta.  
        /// This is used for initializing new derived sets that are updated incrementally using set deltas if the root tracking set is already in use (and thus cannot be replaced)
        /// </summary>
        ISetDelta GenerateInitializerSetDelta(int maxItemsPerRangeItem = 100);

        /// <summary>Accepts an ISetDelta object and serializes it into the given <paramref name="intoStream"/>.  Currently uses JSON DataContract serialization.</summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or if TObjectType is not known to support use with DataContract serialization</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's WriteObject method that is used here (aka There is a problem with the instance being serialized).</exception>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        void Serialize(ISetDelta setDelta, System.IO.Stream intoStream);

        /// <summary>Deserializes, and returns, an ISetDelta object from the given <paramref name="fromStream"/> and returns it.  Uses the internally constructed JSON DataContract deserialization object.</summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or if TObjectType is not known to support use with DataContract serialization</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's ReadObject method that is used here.</exception>
        ISetDelta Deserialize(System.IO.Stream fromStream);
    }

    /// <summary>Extends ISet{TObjectType> and adds the ITrackingSet interface.</summary>
    public interface ITrackingSet<TObjectType> 
        : ISet<TObjectType>, ITrackingSet
    {
        /// <summary>
        /// Performs an update iteration and optionally generates the corresponding ISetDelta object.
        /// When non-zero <paramref name="maxDeltaItemCount"/> is used to specify the maximum number of incremental set changes that can be applied per iteration.
        /// When <paramref name="generateSetDelta"/> is false the method will return null.  When <paramref name="generateSetDelta"/> is true, the method will attempt to create and return an ISetDelta to represent the incremental update deltas.  If there are no such changes the method returns null.
        /// </summary>
        /// <exception cref="SetUseException">When called on an <seealso cref="AdjustableTrackingSet{TObjectType}"/> the instance will throw this exception type</exception>
        new ISetDelta<TObjectType> PerformUpdateIteration(int maxDeltaItemCount, bool generateSetDelta);

        /// <summary>
        /// Generate and return a SetDelta that gives the entire current contents of the tracking set in a single set delta.  
        /// This is used for initializing new derived sets that are updated incrementally using set deltas if the root tracking set is already in use (and thus cannot be replaced)
        /// </summary>
        new ISetDelta<TObjectType> GenerateInitializerSetDelta(int maxItemsPerRangeItem = 100);

        /// <summary>Provides a Generic ITrackingSet{TObjectType} factory method.  Returned value is an ITrackingSet{TObjectType} that can be used to track this tracking set asynchronosly.</summary>
        new ITrackingSet<TObjectType> CreateTrackingSet();
    }

    #endregion

    #region Primary types that are used to support the ISet types

    /// <summary>Set Identity container class (immutable once constructed/deserialized).  Includes a Name and UUID.  Serializable.</summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public class SetID : IEquatable<SetID>
    {
        /// <summary>Constructor with explicitly provided <paramref name="name"/> and optionally provided <paramref name="uuid"/></summary>
        public SetID(string name, string uuid = null, bool generateUUIDForNull = true)
        {
            Name = name.MapNullToEmpty();
            UUID = uuid ?? (generateUUIDForNull ? Guid.NewGuid().ToString() : null);
        }

        /// <summary>Gives the Set's Name</summary>
        [DataMember(Order = 10)]
        public string Name { get; private set; }

        /// <summary>Gives the Set's UUID</summary>
        [DataMember(Order = 20, EmitDefaultValue=false, IsRequired=false)]
        public string UUID { get; private set; }

        /// <summary>Returns true if this and the given <paramref name="other"/> SetID have the same Name and UUID</summary>
        public bool IsEqualTo(SetID other)
        {
            return Equals(other);
        }

        /// <summary>Returns true if this and the given <paramref name="other"/> SetID have the same Name and UUID (this comparison ignors the UUID if either either one is null)</summary>
        public bool Equals(SetID other)
        {
            return (other != null
                    && Name == other.Name
                    && (UUID == other.UUID || UUID == null || other.UUID == null)
                    );
        }

        /// <summary>Support object.Equals override for use in testing</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as SetID);
        }

        /// <summary>When Equals is overriden, then GetHashCode should also be overriden.</summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>Debuging and logging assistant method</summary>
        public override string ToString()
        {
            if (UUID.IsNullOrEmpty())
                return "{0} [NoUUID]".CheckedFormat(Name);
            else
                return "{0} {1}".CheckedFormat(Name, UUID);
        }

        /// <summary>Returns an Empty SetID instance (both Name and UUID are set to the empty string)</summary>
        public static SetID Empty { get { return empty; } }
        private static readonly SetID empty = new SetID(String.Empty, null, generateUUIDForNull: false);

        /// <summary>Returns true if this SetID's Name and UUID are both null or empty.</summary>
        public bool IsEmpty { get { return (Name.IsNullOrEmpty() && UUID.IsNullOrEmpty()); } }
    }

    /// <summary>
    /// This structure gives some essential summary information about the contents of the Set (from which this value is obtained).
    /// The Count gives the number of items that are currently in the set.  
    /// The First and Last give the sequence numbers of the first (oldest) and last (newest) items in the list.  
    /// SeqNumDelta simply gives Last minus First.
    /// </summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public struct SeqNumRangeInfo : IEquatable<SeqNumRangeInfo>
    {
        /// <summary>
        /// Gives the item sequence number of the first element in the set (when non-empty) or of the previously first element of the set (if the set is empty).  
        /// This value updated whenever the first item of the set is removed and there is still at least one element that remains in the set thereafter.
        /// This value is also updated whenever an element is added to an otherwise empty set.
        /// </summary>
        [DataMember(Order = 10)]
        public Int64 First { get; set; }

        /// <summary>Gives the item squence number of the last element that was added to the set, without regard to wether that element is still in the set.</summary>
        [DataMember(Order = 20)]
        public Int64 Last { get; set; }

        /// <summary>Gives the difference Last minus First.  This is not a proxy for the count of the number of element in the set which may be smaller than this value and may be one larger than this number.</summary>
        public Int64 SeqNumDelta { get { return Last - First; } }

        /// <summary>Gives the number of elements in the set.</summary>
        [DataMember(Order = 30)]
        public int Count { get; set; }

        /// <summary>Returns true if the given <paramref name="other"/> has the same contents as this object has.</summary>
        public bool IsEqualTo(SeqNumRangeInfo other)
        {
            return Equals(other);
        }

        /// <summary>Returns true if the given <paramref name="other"/> has the same contents as this object has.</summary>
        public bool Equals(SeqNumRangeInfo other)
        {
            return (First == other.First
                    && Last == other.Last
                    && Count == other.Count
                    );
        }

        /// <summary>Support object.Equals override for use in testing</summary>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is SeqNumRangeInfo))
                return false;

            return Equals((SeqNumRangeInfo)obj);
        }

        /// <summary>When Equals is overriden, then GetHashCode should also be overriden.</summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>Debuging and logging assistant method</summary>
        public override string ToString()
        {
            return "FirstSeq:{0} LastSeq:{1} Count:{2}".CheckedFormat(First, Last, Count);
        }
    }

    /// <summary>
    /// Lists the different types of Set objects
    /// <para/>Copy (0), Reference, Tracking, TrackingAccumulator
    /// </summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public enum SetType : int
    {
        /// <summary>Value for a Copy Set</summary>
        [EnumMember]
        Copy = 0,

        /// <summary>Value for a Reference Set</summary>
        [EnumMember]
        Reference,

        /// <summary>Value for a Tracking Set</summary>
        [EnumMember]
        Tracking,

        /// <summary>Value for a Tracking Accumulator Set</summary>
        [EnumMember]
        TrackingAccumulator,
    }

    /// <summary>
    /// Gives the levels of Changability of a Set
    /// <para/>One of Changeable, and Fixed (read-only)
    /// </summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public enum SetChangeability : int
    {
        /// <summary>Value for a set that can be modified.</summary>
        [EnumMember]
        Changeable = 0,

        /// <summary>Value for a set that cannot be modified (will throw if any public or public interface method is used to attempt to change the set contents).</summary>
        [EnumMember]
        Fixed,
    }

    /// <summary>
    /// Gives the current Update state for a set.
    /// <para/>On of Initial, Empty, InProgress, Complete, Failed
    /// </summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public enum UpdateState : int
    {
        /// <summary>Set is still in its construction state</summary>
        [EnumMember]
        Initial = 0,

        /// <summary>The Set is Empty</summary>
        [EnumMember]
        Empty,

        /// <summary>The Set (or a set that it tracks) is currently being updated</summary>
        [EnumMember]
        InProgress,

        /// <summary>The last Set update was completed and there were no additional pending updates known at that time.</summary>
        [EnumMember]
        Complete,

        /// <summary>The last Set update operation failed.</summary>
        [EnumMember]
        Failed,
    }

    /// <summary>Exception class for reporting attempts to use a set in an invalid or otherwise unsupported way, including attempting to change a set that has been created or set to be Fixed.</summary>
    public class SetUseException : System.Exception
    {
        /// <summary>Basic constructor</summary>
        public SetUseException(string message) : base(message) { }
    }

    #endregion

    #region SetID extension methods

    /// <summary>partial static class contains (more) Extension Methods</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given <paramref name="setID"/> is null or is Empty</summary>
        public static bool IsNullOrEmpty(this SetID setID)
        {
            return (setID == null || setID.IsEmpty);
        }
    }

    #endregion

    #region ISetDelta related types

    /// <summary>Public interface for SetDelta implementation objects that are generated and used by ITrackingSet objects.</summary>
    public interface ISetDelta
    {
        /// <summary>Gives the SetID of the source IReferenceSet that is being tracked and for which this delta object has been generated.</summary>
        SetID SetID { get; }

        /// <summary>Gives the UpdateState of the source state as last captured by the ITrackingSet that generated this delta object.</summary>
        UpdateState SourceUpdateState { get; }

        /// <summary>True if the ITrackingSet that generated this delta had been reset at the start of its corresponding update cycle.</summary>
        bool ClearSetAtStart { get; }

        /// <summary>Returns a sequence of ISetDeltaRemoveRangeItem objects that represent the set items to remove when applying this delta</summary>
        IEnumerable<ISetDeltaRemoveRangeItem> RemoveRangeItems { get; }

        /// <summary>Returns a sequence of ISetDeltaAddContiguousRangeItem objects that represent the set of items to append to this set when applying this delta.</summary>
        IEnumerable<ISetDeltaAddContiguousRangeItem> AddRangeItems { get; }

        /// <summary>Returns the total number of items that are reported as being removed in this set delta</summary>
        int TotalRemovedItemCount { get; }

        /// <summary>Returns the total number of items that are reported as being added in this set delta</summary>
        int TotalAddedItemCount { get; }

        /// <summary>Returns the source set's Capacity at the time that this set detla was created</summary>
        int SourceSetCapacity { get; }
    }

    /// <summary>Public interface for SetDelta implementation objects that are generated and used by ITrackingSet objects.</summary>
    public interface ISetDelta<TObjectType> : ISetDelta
    {
        /// <summary>Returns a sequence of ISetDeltaAddContiguousRangeItem objects that represent the set of items to append to this set when applying this delta.</summary>
        new IEnumerable<ISetDeltaAddContiguousRangeItem<TObjectType>> AddRangeItems { get; }
    }

    /// <summary>This interface defines a portion of an ISetDelta entity that represents a block of one or more contiguous items that have been removed from the set when applying the ISetDelta.</summary>
    public interface ISetDeltaRemoveRangeItem
    {
        /// <summary>Gives the item SeqNum of the first item in the contiguous range that is to be removed when processing this range.</summary>
        Int64 RangeStartSeqNum { get; }

        /// <summary>Gives the index into the source set of the first item in this contiguous range just before it was removed from the source set..</summary>
        Int32 RangeStartIndex { get; }
        
        /// <summary>Gives the count of the number of items in this range that are to be removed</summary>
        Int32 Count { get; }
    }

    /// <summary>This interface defines a portion of an ISetDelta entity that includes a block of one or more contiguous items that are to be added to the set when applying the ISetDelta.</summary>
    public interface ISetDeltaAddContiguousRangeItem
    {
        /// <summary>Gives the item SeqNum of the first item in this contiguous range to be added to the set when processing this range</summary>
        Int64 RangeStartSeqNum { get; }

        /// <summary>Gives the index in the source set of the first item in this contiguous range at which to add this range of items</summary>
        Int32 RangeStartIndex { get; }

        /// <summary>Gives the, possibly null or empty, sequence of deserialized objects that are to be added when processing this range</summary>
        IEnumerable<object> RangeObjects { get; }
    }

    /// <summary>This interface defines a portion of an ISetDelta entity that includes a block of one or more contiguous items that are to be added to the set when applying the ISetDelta.</summary>
    public interface ISetDeltaAddContiguousRangeItem<TObjectType> : ISetDeltaAddContiguousRangeItem
    {
        /// <summary>Gives the, possibly null or empty, sequence of deserialized objects that are to be added when processing this range</summary>
        new IEnumerable<TObjectType> RangeObjects { get; }
    }

    #endregion

    #region Implementation classes (SetBase, ReferenceSet, TrackingSet, ApplyDeltasConfig, AdjustableTrackingSet)

    #region SetBase

    /// <summary>
    /// This is the base class for all of the ISet style implementation objects.  This object includes the bulk of the functionality, including all common functionality, that is required
    /// to implement Reference sets, Copy sets, and Tracking sets.  This class implements ISet{TObjectType} and ISet.
    /// </summary>
    /// <typeparam name="TObjectType">Gives the Generic/template type of the object that this set can contain and manage.</typeparam>
    /// <remarks>
    /// This class is derived from DisposableBase simply so that the RegisteredSet derived class can make use of explicit dispose as a means to automatically Clear the set and Unregister it.
    /// SetBase and the Interconnect.Set concept directly determine when contained elements should actually be disposed and as such these classes and pattern should not generally be used with
    /// object types that are IDisposable and which require that they be correctly disposed of before they are finalized by the GC.
    /// </remarks>
    public class SetBase<TObjectType> 
        : DisposableBase, ISet<TObjectType>, ISet
    {
        #region Constructor variants (public default, public copy, protected base)

        /// <summary>Default constructor.  Creates a Fixed, Copy type set that is Empty and which uses the Empty SetID</summary>
        public SetBase() 
            : this(SetID.Empty, SetType.Copy, SetChangeability.Fixed, UpdateState.Empty, false)
        {}

        /// <summary>Copy constructor.  Creates a Fixed clone of the given rhs.  This operation will lock the rhs.itemContainerListMutex if it has one</summary>
        public SetBase(SetBase<TObjectType> rhs)
            : this(rhs.SetID, rhs.SetType, SetChangeability.Fixed, UpdateState.Initial, false)
        {
            // our copy constructor automatically locks the mutex object, if any, from the rhs before making the copy.
            using (ScopedLock sc = new ScopedLock(rhs.itemContainerListMutex))
            {
                UpdateState = rhs.UpdateState.MergeWith(UpdateState.InProgress);

                SeqNum = rhs.SeqNum;

                SourceSetType = rhs.SourceSetType;
                SourceChangeability = rhs.SourceChangeability;

                itemContainerList.AddRange(rhs.itemContainerList);
                itemListSeqNumRangeInfo = rhs.ItemListSeqNumRangeInfo;

                UpdateState = rhs.UpdateState.MergeWith(UpdateState.Complete);

                if (rhs.Capacity > 0)
                    _capacity = rhs.Capacity;
            }
        }

        /// <summary>protected constructor used by derived classes  Allows caller to specify the SetID, SetType, initial Changeability, initial UpdateState and to create and setup the mutex if required.</summary>
        protected SetBase(SetID setID, SetType setType, SetChangeability changeability, UpdateState updateState, bool createMutex = true)
        {
            SetID = setID;
            SetType = setType;
            Changeability = changeability;
            UpdateState = updateState;

            itemContainerListMutex = (createMutex ? new object() : null);

            if (createMutex)
            {
                owningManagedThread = System.Threading.Thread.CurrentThread;
                owningManagedThreadID = owningManagedThread.ManagedThreadId;
            }
        }

        #endregion

        #region Debug helper

        /// <summary>Debug and logging helper method</summary>
        public override string ToString()
        {
            return "{0} {1} {2} count:{3} {4} {5}".CheckedFormat(SetID, SetType, typeof(TObjectType).GetTypeDigestName(), Count, UpdateState, Changeability);
        }

        #endregion

        #region protected fields and related types

        /// <summary>This class is the container object used by the SetBase to contain and track Items that are added to and removed from the set.</summary>
        public class ItemContainer
        {
            /// <summary>Constructor requires a seqNum value and the item itself.</summary>
            public ItemContainer(Int64 seqNum, TObjectType item)
            {
                SeqNum = seqNum;
                Item = item;
            }

            /// <summary>Readonly SeqNum that was given to this item.</summary>
            public Int64 SeqNum { get; private set; }
            /// <summary>Readonly Item itself</summary>
            public TObjectType Item { get; private set; }
            /// <summary>Gives the last index of the item in the set's underlying list.  Used when reporting set additions and removal via the CollectionChanged events</summary>
            public int LastIndexOfItemInList { get; set; }
            /// <summary>Enumeration version of the LastIndexOfItemInSet used with item removal to indicate if this item was known to be the first or last item in the last at the time it was removed.</summary>
            public RemovedFromSetPosition RemovedFromSetPosition { get; set; }

            /// <summary>Debugger helper method.</summary>
            public override string ToString()
            {
                return "Seq:{0} '{1}' lastIdx:{2} RmvFrom:{3}".CheckedFormat(SeqNum, Item, LastIndexOfItemInList, RemovedFromSetPosition);
            }
        }

        /// <summary>This enumeratino is used to indicate where in the set's list an item was removed from.</summary>
        public enum RemovedFromSetPosition : int
        {
            /// <summary>The location is not known (default)</summary>
            None = 0,
            /// <summary>Item had been the first item in the set (if item is both First and Last then it will report First)</summary>
            First,
            /// <summary>Item had been somewhere between the first item and the last item in the set (exclusive)</summary>
            Middle,
            /// <summary>Item had been the last item in the set (if item is both First and Last then it will report First)</summary>
            Last,
        }

        /// <summary>This field gives the actual storage object for the set of ItemContainers that contain the items that are currently in the set.</summary>
        internal List<ItemContainer> itemContainerList = new List<ItemContainer>();

        /// <summary>
        /// When non-null, this field gives the mutex that is to be used when accessing or changing properties or contents of the set.
        /// </summary>
        /// <remarks>
        /// Annotate this as readonly since the value is only assigned once in the constructor (to either new object() or null).
        /// </remarks>
        internal readonly object itemContainerListMutex;

        /// <summary>For sets that have a non-nll itemContainerListMutex, this records the Thread that currently "owns" the set (ie which has r/w access to it).</summary>
        internal System.Threading.Thread owningManagedThread = null;

        /// <summary>For sets that have a non-nll itemContainerListMutex, this records the ThreadID of the Thread that currently "owns" the set (ie which has r/w access to it).</summary>
        internal int owningManagedThreadID = 0;

        /// <summary>Sequence number source counter for all sequence numbers that are attached to ItemContainers and which are disseminated from there</summary>
        private Int64 nextItemSeqNum = 1;

        /// <summary>This method accepts a TObjectType item and returns a newly constructed ItemContainer that contains the given item and which has been assigned the next item SeqNum value.</summary>
        protected ItemContainer InnerCreateItemContainer(TObjectType item)
        {
            return new ItemContainer(nextItemSeqNum++, item);
        }

        /// <summary>returns the item sequence number that will be used for the next newly created item.</summary>
        protected Int64 PeekNextItemSeqNum { get { return nextItemSeqNum; } }

        /// <summary>
        /// This method is used any time the contents of the set has changed and the set needs give the outside world a shorthand notation to know that its contents may have changed.
        /// This method sets the SetBase's SeqNum to nextSetSeqNum and increments nextSetSeqNum.
        /// </summary>
        protected void AssignNewSetSeqNum()
        {
            SeqNum = nextSetSeqNum++;
        }

        /// <summary>Sequence number source counter for this SetBase's SeqNum values.</summary>
        private Int64 nextSetSeqNum = 1;

        /// <summary>This is the SetBase's storage for its ItemListSeqNumRangeInfo property</summary>
        protected SeqNumRangeInfo itemListSeqNumRangeInfo = new SeqNumRangeInfo();

        #endregion

        #region ISet implementation (including explicit parts), includes ICollection implementation

        /// <summary>Gives the SetID of this set which includes the Name and UUID</summary>
        public SetID SetID { get; protected set; }

        /// <summary>Gives the SetType of this set.</summary>
        public SetType SetType { get; protected set; }

        /// <summary>Gives the SetChangeability of this set.</summary>
        public SetChangeability Changeability { get; protected set; }

        /// <summary>Gives the UpdateState of this set.</summary>
        public virtual UpdateState UpdateState { get; protected set; }

        /// <summary>Returns true if the UpdateState is UpdateState.InProgess</summary>
        public virtual bool IsUpdateInProgress 
        { 
            get { return (UpdateState == UpdateState.InProgress); } 
        }

        /// <summary>Gives the SetType of the source set for this set, or of this set if it is a source.</summary>
        public SetType SourceSetType 
        { 
            get { return ((SetType == SetType.Reference) ? SetType : sourceSetType); } 
            protected set { sourceSetType = value; } 
        }

        /// <summary>Gives the SetChangeability of the source set for this set, or of this set it is a source.</summary>
        public SetChangeability SourceChangeability 
        {
            get { return ((SetType == SetType.Reference) ? Changeability : sourceChangeability); } 
            protected set { sourceChangeability = value; } 
        }

        /// <summary>Backing storage for the last observed SetType from the source set for this one (as appropriate)</summary>
        private SetType sourceSetType;

        /// <summary>Backing storage for the last observed SetChangability from the source set for this one (as appropriate)</summary>
        private SetChangeability sourceChangeability;

        /// <summary>Returns true if the SourceChangeability is SetChangeability.Fixed</summary>
        public bool IsSourceFixed
        {
            get { return (SourceChangeability == SetChangeability.Fixed); }
        }

        /// <summary>For TrackingSets, gives the string error code for the last operation on this set, or string.Empty if the last operation succeeded.</summary>
        public string ResultCode { get; protected set; }

        /// <summary>Gives the set's most recently generated sequence number (dervied from the source set for Copy sets).  This sequence is unique to each set.  Tracking sets maintain their own SeqNum series that is not directly derived from their source set.</summary>
        public Int64 SeqNum { get; protected set; }

        /// <summary>Gives the SeqNumRangeInfo for the current contents of this set.</summary>
        public SeqNumRangeInfo ItemListSeqNumRangeInfo 
        { 
            get 
            {
                using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
                {
                    return itemListSeqNumRangeInfo;
                }
            } 
        }

        #region ISet object versions of ISet<TObjectType> implementations - underlying implementations implement locking when needed

        /// <summary>Indexed getter that returns the indexed item as an object.</summary>
        object ISet.this[int index]
        {
            get { return this[index]; }
        }

        /// <summary>Converts the Set's contents to an Array of objects and returns it.</summary>
        object[] ISet.ToArray()
        {
            return (this.ToArray() as object []);
        }

        /// <summary>Generates and returns the contents of this set as an array of ItemAndSeqNum{object} elements.</summary>
        ItemAndSeqNum<object>[] ISet.ToItemAndSeqNumArray()
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                return itemContainerList.Select((itemContainer) => new ItemAndSeqNum<object>() { ItemSeqNum = itemContainer.SeqNum, Item = itemContainer.Item}).ToArray();
            }
        }

        #endregion

        /// <summary>
        /// Copies the entire set contents into the given array, starting at the given arrayIndex in the target array.
        /// </summary>
        public void CopyTo(Array array, int arrayIndex)
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                int listCount = itemContainerList.Count;
                int getIdx = 0, putIdx = arrayIndex;

                while (getIdx < listCount)
                {
                    array.SetValue(itemContainerList[getIdx++].Item, putIdx++);
                }
            }
        }

        /// <summary>
        /// Returns true if this set has a non-null content mutex object that is used to give exclusive access to its internal contents to one thread at a time.
        /// Returns false if no such mutex object has been created.
        /// </summary>
        public bool IsSynchronized 
        { 
            get { return (itemContainerListMutex != null); } 
        }

        /// <summary>
        /// Gets the mutex object, if any, that can be used to synchronize internal access to the set contents.  Returns null if no such mutex object has been created.
        /// </summary>
        public object SyncRoot 
        { 
            get { return itemContainerListMutex; } 
        }

        /// <summary>
        /// Returns an IEnumerator that can be used to enumerate the items that are currently in the set.  
        /// No changes to the set should be made while using this enumerator.
        /// The use of this method is only safe when the client knows that it is the only entity that has modify rights on this set.  
        /// As such it can safely use the returned enumerator without risk that some external entity will change the set contents unexpectedly and without
        /// needing to own the set mutex because other threads can safely make copies of this set while the owner is enumerating, but not changing, it.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            ThrowIfHasItemContainerListMutex(true);

            return itemContainerList.Select((itemContainer) => itemContainer.Item as object).GetEnumerator();
        }

        /// <summary>
        /// Returns true if the set contains the given item (compared by reference)
        /// </summary>
        public bool Contains(TObjectType item)
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                return itemContainerList.Any((itemContainer) => object.ReferenceEquals(itemContainer.Item, item));
            }
        }

        /// <summary>
        /// Copies the entire set contents into the given array, starting at the given arrayIndex in the target array.
        /// </summary>
        public void CopyTo(TObjectType[] array, int arrayIndex)
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                int listCount = itemContainerList.Count;
                int getIdx = 0, putIdx = arrayIndex;

                while (getIdx < listCount)
                {
                    array[putIdx++] = itemContainerList[getIdx++].Item;
                }
            }
        }

        /// <summary>
        /// Returns true if the set's Changeability IsFixed
        /// </summary>
        public virtual bool IsReadOnly 
        { 
            get { return !Changeability.IsFixed(); } 
        }

        /// <summary>
        /// Clears the set by removing all of its elements.
        /// </summary>
        public virtual void Clear()
        {
            // Precoditions for Clear:
            // TrackingAccumulator set type: none
            // Tracking set type: set is not fixed.
            // Others (Copy, Reference): set is not fixed and it has a mutex.
            switch (SetType)
            {
                case Interconnect.Sets.SetType.TrackingAccumulator: break;
                case Interconnect.Sets.SetType.Tracking: ThrowOnFixed(); break;
                default: ThrowOnFixedOrDoesNotHaveAMutex(); break;
            }

            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                InnerRemoveAll(null, true);

                AssignNewSetSeqNum();
            }

            InnerNoteCollectionHasBeenCleared();
        }

        /// <summary>
        /// Gives the currently configured capacity for this set.
        /// </summary>
        public virtual int Capacity 
        { 
            get { return _capacity; } 
            set 
            {
                using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
                {
                    ThrowOnFixed();

                    InnerSetCapacity(value);
                }
            } 
        }
        private volatile int _capacity;

        #endregion

        #region ICollection<TObjectType> methods

        /// <summary>
        /// Returns an IEnumerator that can be used to enumerate the items that are currently in the set.  
        /// No changes to the set should be made while using this enumerator.
        /// The use of this method is only safe when the client knows that it is the only entity that has modify rights on this set.  
        /// As such it can safely use the returned enumerator without risk that some external entity will change the set contents unexpectedly and without
        /// needing to own the set mutex because other threads can safely make copies of this set while the owner is enumerating, but not changing, it.
        /// </summary>
        IEnumerator<TObjectType> IEnumerable<TObjectType>.GetEnumerator()
        {
            ThrowIfHasItemContainerListMutex(true);

            return InnerGetEnumerable().GetEnumerator();
        }

        protected IEnumerable<TObjectType> InnerGetEnumerable()
        {
            return itemContainerList.Select((itemContainer) => itemContainer.Item);
        }

        /// <summary>
        /// Adds the given item to the set.
        /// </summary>
        public virtual void Add(TObjectType item)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                InnerAdd(item);

                AssignNewSetSeqNum();
            }
        }

        /// <summary>
        /// Attempts to find and remove the first element in the set that is the same object instance as the given item.
        /// Returns true if such an element was found and removed.  Returns false otherwise.
        /// </summary>
        public virtual bool Remove(TObjectType item)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                bool returnValue = InnerRemoveFirst((listItem) => Object.ReferenceEquals(listItem, item));

                AssignNewSetSeqNum();

                return returnValue;
            }
        }
        
        #endregion

        #region ISet<TObjectType> implementation

        /// <summary>Generic indexed getter that returns the indexed TObjectType item.</summary>
        public TObjectType this[int index]
        {
            get
            {
                using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
                {
                    return itemContainerList[index].Item;
                }
            }
        }

        /// <summary>Generates and returns the contents of this set as an array of TObjectType items.</summary>
        public TObjectType[] ToArray()
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                return itemContainerList.Select((itemContainer) => itemContainer.Item).ToArray();
            }
        }

        /// <summary>Generates and returns the contents of this set as an array of ItemAndSeqNum{TObjectType} elements.</summary>
        public ItemAndSeqNum<TObjectType>[] ToItemAndSeqNumArray()
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                return itemContainerList.Select((itemContainer) => new ItemAndSeqNum<TObjectType>() { ItemSeqNum = itemContainer.SeqNum, Item = itemContainer.Item }).ToArray();
            }
        }

        /// <summary>Generates and returns a Copy type set derived from the current set.</summary>
        public SetBase<TObjectType> CreateCopy()
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                return new SetBase<TObjectType>(this) { SetType = SetType.Copy };
            }
        }

        /// <summary>
        /// Returns the count of the number of items that are currently in the set.
        /// Shorthand for the ItemListSeqNumRangeInfo.Count</summary>
        public int Count
        {
            get
            {
                using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
                {
                    return itemListSeqNumRangeInfo.Count;
                }
            }
        }

        #endregion

        #region Internal Add/Remove implementation

        /// <summary>This method is used by derived classes to set or change the capacity of this set</summary>
        protected void InnerSetCapacity(int capacity)
        {
            if (_capacity != capacity)
            {
                // delete the oldest items until the list length is less than the desired capacity
                int removeCount = (itemContainerList.Count - capacity);

                if (removeCount > 0)
                {
                    InnerRemoveRange(0, removeCount, true);
                }

                _capacity = capacity;

                if (_capacity > 0)
                    itemContainerList.Capacity = _capacity;
            }
        }

        /// <summary>
        /// This method is used by derived classes to add/append one or more items to the set.  
        /// This method provides the common implementation of this operation and includes:
        /// <para/>- generation of ItemContainers for each of the items to add, including the generation of item sequence numbers for each one.
        /// <para/>- a loop that manages the addition of one or more items by 
        /// <para/>-- dividing the set of items into groups of no more than the set capacity of such items,
        /// <para/>-- making space for the items to fit in the set while honoring the sets capacity contstratins
        /// <para/>-- adding the items to the set and reporting the addition of the items to any attached collection change event subscribers.
        /// </summary>
        protected void InnerAdd(params TObjectType[] addItemArray)
        {
            addedItemContainerList = addedItemContainerList ?? new List<ItemContainer>();

            int addItemArrayLength = addItemArray.SafeLength();

            try
            {
                for (int startIndex = 0; startIndex < addItemArrayLength; )
                {
                    int iterationAddCount = (addItemArrayLength - startIndex);
                    if (_capacity > 0 && _capacity < iterationAddCount)
                        iterationAddCount = _capacity;

                    for (int index = 0; index < iterationAddCount; index++)
                    {
                        addedItemContainerList.Add(InnerCreateItemContainer(addItemArray[startIndex++]));
                    }

                    InnerMakeSpacePriorToAdd(iterationAddCount);

                    if (iterationAddCount > 0)
                    {
                        for (int idx = 0; idx < iterationAddCount; idx++)
                        {
                            ItemContainer itemContainer = addedItemContainerList[idx];

                            itemContainer.LastIndexOfItemInList = itemContainerList.Count;

                            itemContainerList.Add(itemContainer);
                        }

                        InnerNoteItemsAdded(addedItemContainerList);
                    }
                }
            }
            finally
            {
                if (addedItemContainerList.Count > 0)
                    InnerNoteItemsAdded(addedItemContainerList);
            }
        }

        /// <summary>
        /// This method is used by derived classes to make space in the set prior to adding one or more items.  
        /// This method provides the common implementation of this operation and includes:
        /// <para/>- determining how many items in the set must be removed to make space for the incoming ones, if any
        /// <para/>- removing the required number of items from set
        /// <para/>- reporting the removal of these items to any attached collection change event subscribers.
        /// <para/>This method does not report overall SetCount and/or Array property changed events.  The caller is required to do this when appropriate.
        /// </summary>
        protected void InnerMakeSpacePriorToAdd(int numItemsToAdd)
        {
            int projectedRemainingCapacity = (_capacity - (itemContainerList.Count + numItemsToAdd));
            if (projectedRemainingCapacity < 0 && _capacity > 0)
            {
                removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();
                int numItemsToRemove = Math.Min(itemContainerList.Count, -projectedRemainingCapacity);

                for (int idx = 0; idx < numItemsToRemove; idx++)
                {
                    ItemContainer itemContainer = itemContainerList[idx];

                    itemContainer.LastIndexOfItemInList = 0;
                    itemContainer.RemovedFromSetPosition = RemovedFromSetPosition.First;

                    removedItemContainerList.Add(itemContainer);
                }

                itemContainerList.RemoveRange(0, numItemsToRemove);

                InnerNoteItemsRemoved(removedItemContainerList);
            }
        }

        /// <summary>
        /// Attempts to remove, and report the removal, of the first item in the set that matches the given predicate (ie for which the predicate returns true).
        /// This method will report both the removal of any such maching item and will report the overall SetCount and Array property changed events as appropriate.
        /// </summary>
        protected bool InnerRemoveFirst(Func<TObjectType, bool> matchPredicate)
        {
            removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();

            int itemContainerListCount = itemContainerList.Count;
            for (int index = 0; index < itemContainerListCount; index++)
            {
                ItemContainer itemContainer = itemContainerList[index];

                if ((matchPredicate == null) || matchPredicate(itemContainer.Item))
                {
                    itemContainer.LastIndexOfItemInList = index;
                    itemContainer.RemovedFromSetPosition = InnerConvertToRemovedFromSetPosition(index);

                    removedItemContainerList.Add(itemContainer);

                    itemContainerList.RemoveAt(index);

                    break;
                }
            }

            if (removedItemContainerList.Count > 0)
            {
                InnerNoteItemsRemoved(removedItemContainerList);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// This method converts a given set index into a matching enumeration to indicate if this item was the first item in the set, the last item in the set or a middle item in the set.
        /// This information is recorded when items are being removed and is used later when generating collection changed events
        /// </summary>
        private RemovedFromSetPosition InnerConvertToRemovedFromSetPosition(int index)
        {
            if (index == 0)
                return RemovedFromSetPosition.First;
            else if (index == itemContainerList.Count - 1)
                return RemovedFromSetPosition.Last;
            else
                return RemovedFromSetPosition.Middle;
        }

        /// <summary>
        /// Attempts to remove, and report the removal, of all items in the set that matches the given predicate (ie for which the predicate returns true).
        /// If the predicate is given as null then this method will remove all items from the set.
        /// Returns true if any elements were removed from the set or false otherwise.
        /// This method will report both the removal of any such maching item and will report the overall SetCount and Array property changed events as appropriate.
        /// </summary>
        protected bool InnerRemoveAll(Func<TObjectType, bool> matchPredicate, bool processNoteItemsRemoved)
        {
            bool removeAll = (matchPredicate == null);
            removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();

            int index = 0;
            while (index < itemContainerList.Count)
            {
                ItemContainer itemContainer = itemContainerList[index];

                if (removeAll || matchPredicate(itemContainer.Item))
                {
                    itemContainer.LastIndexOfItemInList = index;
                    itemContainer.RemovedFromSetPosition = InnerConvertToRemovedFromSetPosition(index);

                    removedItemContainerList.Add(itemContainer);
                    itemContainerList.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }

            if (removedItemContainerList.Count > 0)
            {
                if (processNoteItemsRemoved)
                    InnerNoteItemsRemoved(removedItemContainerList);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to remove, and optionally report the removal, of the items in the set that are in the given range which includes the given count of items starting at the given index.
        /// This method will report both the removal of any such maching item and will report the overall SetCount and Array property changed events as appropriate.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the combination of starting index and count produces any list index that is not valid.</exception>
        protected void InnerRemoveRange(int index, int count, bool processNoteItemsRemoved)
        {
            removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();

            try
            {
                while (count > 0)
                {
                    ItemContainer itemContainer = itemContainerList[index];
                    itemContainer.LastIndexOfItemInList = index;
                    itemContainer.RemovedFromSetPosition = InnerConvertToRemovedFromSetPosition(index);

                    itemContainerList.RemoveAt(index);

                    removedItemContainerList.Add(itemContainer);

                    count--;
                }
            }
            finally
            {
                if (processNoteItemsRemoved && removedItemContainerList.Count > 0)
                    InnerNoteItemsRemoved(removedItemContainerList);
            }
        }

        /// <summary>
        /// Attempts to remove, and optionally report the removal, of the items in the set that are in the given range of item sequence numbers.
        /// This method will report both the removal of any such maching item and will report the overall SetCount and Array property changed events as appropriate.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the combination of starting index and count produces any list index that is not valid.</exception>
        protected void InnerRemoveSeqNumRange(Int64 rangeStartSeqNum, Int64 rangeEndSeqNum, bool processNoteItemsRemoved)
        {
            removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();

            try
            {
                int scanIndex = Math.Max(0, InnerFindIndexForSeqNum(rangeStartSeqNum));

                for (;;)
                {
                    if (scanIndex >= itemContainerList.Count)
                        break;

                    ItemContainer itemContainer = itemContainerList.SafeAccess(scanIndex, null);

                    if (itemContainer != null)
                    {
                        Int64 trialIndexSeqNum = itemContainer.SeqNum;

                        if (trialIndexSeqNum > rangeEndSeqNum)
                            break;

                        if (trialIndexSeqNum >= rangeStartSeqNum)
                        {
                            itemContainer.LastIndexOfItemInList = scanIndex;
                            itemContainer.RemovedFromSetPosition = InnerConvertToRemovedFromSetPosition(scanIndex);
                            removedItemContainerList.Add(itemContainer);

                            itemContainerList.RemoveAt(scanIndex);
                        }
                        else
                        {
                            scanIndex++;
                        }
                    }
                    else
                    {
                        scanIndex++;
                    }
                }
            }
            finally
            {
                if (processNoteItemsRemoved && removedItemContainerList.Count > 0)
                    InnerNoteItemsRemoved(removedItemContainerList);
            }
        }

        /// <summary>
        /// Attempts to remove, and optionally report the removal, of the items in the set that are in the given <paramref name="seqNumsToRemoveSetArray"/>.
        /// If requested, this method will report both the removal of any such maching item and will report the overall SetCount and Array property changed events as appropriate.
        /// </summary>
        protected void InnerRemoveSeqNumSet(Int64 [] seqNumsToRemoveSetArray, bool processNoteItemsRemoved)
        {
            seqNumsToRemoveSetArray = seqNumsToRemoveSetArray ?? Utils.Collections.EmptyArrayFactory<long>.Instance;

            if (seqNumsToRemoveSetArray.Length == 0 || itemContainerList.Count == 0)
                return;

            removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();

            try
            {
                int scanIndex = 0;
                ItemContainer scanItemContainer = itemContainerList.SafeAccess(scanIndex);
                long scanItemSeqNum = (scanItemContainer != null) ? scanItemContainer.SeqNum : 0;

                int itemContainerListCount = itemContainerList.Count;

                foreach (var seqNumToRemove in seqNumsToRemoveSetArray)
                {
                    // try advancing the scan one element to see if the next one is also the next to remove
                    if (seqNumToRemove != scanItemSeqNum)
                    {
                        bool goUp = (seqNumToRemove > scanItemSeqNum);
                        int tryNextIdx = goUp ? scanIndex + 1 : scanIndex - 1;
                        ItemContainer tryNextItemContainer = itemContainerList.SafeAccess(tryNextIdx);
                        if (tryNextItemContainer != null && (goUp ? (seqNumToRemove >= tryNextItemContainer.SeqNum) : (seqNumToRemove <= tryNextItemContainer.SeqNum)))
                        {
                            scanIndex = tryNextIdx;
                            scanItemContainer = tryNextItemContainer;
                            scanItemSeqNum = scanItemContainer.SeqNum;
                        }
                    }

                    // if moving one up or one down was not enough to find seqNumToRemove then do binary search from the current scanIdx toward the appropriate end of the set
                    if (seqNumToRemove != scanItemSeqNum)
                    {
                        bool goUp = (seqNumToRemove > scanItemSeqNum);
                        bool canMove = (goUp ? (scanIndex + 1 < itemContainerListCount) : (scanIndex > 0));

                        if (canMove)
                        {
                            int tryNextIdx = (goUp) ? InnerFindIndexForSeqNum(seqNumToRemove, searchRangeStartIndex: scanIndex) : InnerFindIndexForSeqNum(seqNumToRemove, searchRangeEndIndex: scanIndex);
                            ItemContainer tryNextItemContainer = itemContainerList.SafeAccess(tryNextIdx);
                            if (tryNextItemContainer != null)
                            {
                                scanIndex = tryNextIdx;
                                scanItemContainer = tryNextItemContainer;
                                scanItemSeqNum = scanItemContainer.SeqNum;
                            }
                        }
                    }

                    // if the scanItemContainer is valid and its seqNum matches the next one then just remove this element and continue
                    if (scanItemContainer != null && seqNumToRemove == scanItemSeqNum)
                    {
                        scanItemContainer.LastIndexOfItemInList = scanIndex;
                        scanItemContainer.RemovedFromSetPosition = InnerConvertToRemovedFromSetPosition(scanIndex);
                        removedItemContainerList.Add(scanItemContainer);

                        itemContainerList.RemoveAt(scanIndex);
                        itemContainerListCount--;

                        if (itemContainerListCount == 0)
                            break;

                        scanIndex = scanIndex.Clip(0, itemContainerListCount - 1);

                        scanItemContainer = itemContainerList.SafeAccess(scanIndex);
                        scanItemSeqNum = (scanItemContainer != null) ? scanItemContainer.SeqNum : 0;
                    }

                    // otherwise the given seqNumToRemove was not found.
                }
            }
            finally
            {
                if (processNoteItemsRemoved && removedItemContainerList.Count > 0)
                    InnerNoteItemsRemoved(removedItemContainerList);
            }
        }

        /// <summary>
        /// This method is given an itemSeqNum and attempts to find the index into the set's contents of the first item that has the given sequence number or which has sequence number that 
        /// is larger than the given one.
        /// If the list is empty this method returns -1.  
        /// If the given seq number is larger than the last sequence number in the set then this method returns the set count (one past the last valid index).
        /// </summary>
        protected int InnerFindIndexForSeqNum(Int64 findItemSeqNum, int searchRangeStartIndex = 0, int searchRangeEndIndex = int.MaxValue)
        {
            if (itemContainerList.Count == 0)
                return -1;

            int firstIndex = searchRangeStartIndex;
            Int64 firstSeqNum = itemContainerList[firstIndex].SeqNum;

            if (findItemSeqNum <= firstSeqNum)
                return firstIndex;

            int lastIndex = itemContainerList.Count - 1;
            if (lastIndex > searchRangeEndIndex)
                lastIndex = searchRangeEndIndex;
            Int64 lastSeqNum = itemContainerList[lastIndex].SeqNum;

            if (findItemSeqNum > lastSeqNum)
                return lastIndex + 1;

            if (findItemSeqNum == lastSeqNum)
                return lastIndex;

            // the given index is known to be between the first and last items in the array
            for (;;)
            {
                int testIndex = (firstIndex + lastIndex) >> 1;
                Int64 testIndexSeqNum = itemContainerList[testIndex].SeqNum;

                if (findItemSeqNum == testIndexSeqNum)
                    return testIndex;
                else if (lastIndex <= firstIndex + 1)
                    break;
                else if (findItemSeqNum < testIndexSeqNum)
                    lastIndex = testIndex;
                else
                    firstIndex = testIndex;
            }

            return lastIndex;
        }

        /// <summary>This list is used to accumulate ItemContainers of items that have been added to the set since the last time it was passed to the InnerNoteItemsAdded method.</summary>
        protected List<ItemContainer> addedItemContainerList = null;

        /// <summary>This list is used to accumulate ItemContainers of items that have been removed from the set since the last time it was passed to the InnerNoteItemsRemoved method.</summary>
        protected List<ItemContainer> removedItemContainerList = null;

        /// <summary>
        /// This method processes the contents of the given list of ItemContainers that have been added to the set and uses the InnerNoteItemAdded method to support reporting
        /// the appropriate CollectionChanged event for each one.  
        /// </summary>
        protected void InnerNoteItemsAdded(List<ItemContainer> addedItemContainerList)
        {
            foreach (ItemContainer itemContainer in addedItemContainerList)
                InnerNoteItemAdded(itemContainer);

            addedItemContainerList.Clear();
        }

        /// <summary>
        /// This method processes the contents of the given list of ItemContainers that have been removed from the set and uses the InnerNoteItemRemoved method to support reporting
        /// the appropriate CollectionChanged event for each one.
        /// </summary>
        protected void InnerNoteItemsRemoved(List<ItemContainer> removedItemContainerList)
        {
            foreach (ItemContainer itemContainer in removedItemContainerList)
                InnerNoteItemRemoved(itemContainer);

            removedItemContainerList.Clear();
        }

        #endregion

        #region ThrowOnFixed, ThrowOnFixedOrDoesNotHaveAMutex, ThrowIfHasItemContainerListMutex and ThrowNotSupportedException methods

        /// <summary>
        /// This method is used to confirm the correct use of specific SetBase's methods, namely the ones that can change the set.
        /// If the set's Changeability IsFixed then this method will throw a System.NotSupportedException.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        protected void ThrowOnFixed()
        {
            if (Changeability.IsFixed())
                ThrowNotSupportedException(1);
        }

        /// <summary>
        /// This method is used to confirm the correct use of specific SetBase's methods, namely the ones that can change the set.
        /// If the set's Changeability IsFixed or if the set does not have a non-null itemContainerListMutex, then this method will throw a System.NotSupportedException.
        /// It will acquire the name of the method that is not supported by traversing up the call stack to get the method name from the method that called this one.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        protected void ThrowOnFixedOrDoesNotHaveAMutex()
        {
            if (Changeability.IsFixed() || (itemContainerListMutex == null))
            {
                ThrowNotSupportedException(1);
            }
        }

        /// <summary>
        /// This method is used to confirm that this SetBase instance does not have an itemContainerListMutex and to throw a SetUseException if the set has such a mutex.
        /// If the caller passes allowOwningThreadIn = true then the method will not throw if the current thread is the thread that currently "owns" the set (typically the one that created the set).
        /// This method is used by the SetBase GetEnumerator methods as the result that they return is not threadsafe and thus cannot be safely use to traverse the set unless the set
        /// does not have a mutex or the calling thread "owns" the set and thus is in control if when its contents might actually change.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        protected void ThrowIfHasItemContainerListMutex(bool allowOwningThreadIn)
        {
            if (itemContainerListMutex != null)
            {
                if (!allowOwningThreadIn || (owningManagedThreadID != System.Threading.Thread.CurrentThread.ManagedThreadId))
                {
                    string methodName = new System.Diagnostics.StackFrame(1).GetMethod().Name;

                    new SetUseException("{0} cannot be used with this {1} ISet instance because it has mutex".CheckedFormat(methodName, SetType)).Throw();
                }
            }
        }

        /// <summary>
        /// This method throws a System.NotSupportedException with a message that includes the name of the method obtained from the indicated stack frame one or more levels above this one.
        /// When skipFramesExtraFrames is zero, this method obtains the calling method name from the stack frame from its direct caller.  
        /// When skipFramesExtraFrames is > 0, this method obtains the calling method name from the stack frame that is skipFramesExtraFrames above the direct caller's stack frame.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void ThrowNotSupportedException(int skipFramesExtraFrames)
        {
            string methodName = new System.Diagnostics.StackFrame(skipFramesExtraFrames + 1).GetMethod().Name;

            new System.NotSupportedException("Method {0} not supported on {1} Set because it is {2}".CheckedFormat(methodName, SetType, Changeability)).Throw();
        }

        #endregion

        #region PropertyChanged and CollectionChanged event implementation

        /// <summary>If CollectionChanged events are in use, this method generates a Reset type CollectionChanged event</summary>
        protected void InnerNoteCollectionHasBeenCleared()
        {
            if (itemListSeqNumRangeInfo.Count != 0 || itemListSeqNumRangeInfo.First != itemListSeqNumRangeInfo.Last)
            {
                itemListSeqNumRangeInfo.Count = 0;
                itemListSeqNumRangeInfo.First = itemListSeqNumRangeInfo.Last;
            }

            if (observableCollection != null)
            {
                try
                {
                    observableCollection.Clear();
                }
                catch { }
            }
        }

        /// <summary>
        /// This method starts by looking at the RemovedFromSetPostiion property in the given itemContainer and updates either the itemListSeqNumRangeInfo's First or List property from 
        /// SeqNum's from the corresponding possitions in the itemContainerList.
        /// Then, if CollectionChanged events are in use, this method generates a Remove type CollectionChanged event for the removed item.
        /// </summary>
        protected void InnerNoteItemRemoved(ItemContainer itemContainer)
        {
            if (itemContainerList.Count > 0)
            {
                if (itemContainer.RemovedFromSetPosition == RemovedFromSetPosition.First)
                    itemListSeqNumRangeInfo.First = itemContainerList[0].SeqNum;
                else if (itemContainer.RemovedFromSetPosition == RemovedFromSetPosition.Last)
                    itemListSeqNumRangeInfo.Last = itemContainerList[itemContainerList.Count - 1].SeqNum;
            }
            else
            {
                itemListSeqNumRangeInfo.First = itemListSeqNumRangeInfo.Last;
            }
            itemListSeqNumRangeInfo.Count = itemContainerList.Count;

            if (observableCollection != null)
            {
                try
                {
                    observableCollection.RemoveAt(itemContainer.LastIndexOfItemInList);
                }
                catch { }
            }
        }

        /// <summary>
        /// This method starts by updating itemListSeqNumRangeInfo's List, and possibly First, property from the SeqNum of the added itemContainer.
        /// Then, if CollectionChanged events are in use, this method generates a Add type CollectionChanged event for the added item.
        /// </summary>
        protected void InnerNoteItemAdded(ItemContainer itemContainer)
        {
            itemListSeqNumRangeInfo.Last = itemContainer.SeqNum;
            if (itemContainer.LastIndexOfItemInList == 0)
                itemListSeqNumRangeInfo.First = itemContainer.SeqNum;
            itemListSeqNumRangeInfo.Count = itemContainerList.Count;

            if (observableCollection != null)
            {
                try
                {
                    observableCollection.Add(itemContainer.Item);
                }
                catch { }
            }
        }

        /// <summary>
        /// Protected common implementation for PropertyChanged events when they are supported by the derived type (implementation must override this to expose a public version...)
        /// The adder method verifies that the target set is not using a mutex (PropertyChanged events are not thread safe) and then sets a flag that records that PropertyChanged events are in use so that they will actually be generated.
        /// </summary>
        protected event PropertyChangedEventHandler PropertyChanged
        {
            add 
            {
                ThrowIfHasItemContainerListMutex(true);
                CreateObservableCollectionIfNeeded();

                if (observableCollection != null)
                    ((INotifyPropertyChanged)observableCollection).PropertyChanged += value;
            }
            remove 
            {
                if (observableCollection != null)
                    ((INotifyPropertyChanged)observableCollection).PropertyChanged -= value;
            }
        }

        /// <summary>
        /// Protected common implementation for CollectionChanged events when they are supported by the derived type (implementation must override this to expose a public version...)
        /// The adder method verifies that the target set is not using a mutex (CollectionChanged events are not thread safe) and then sets a flag that records that CollectionChanged events are in use so that they will actually be generated.
        /// </summary>
        protected event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add 
            {
                ThrowIfHasItemContainerListMutex(true);
                CreateObservableCollectionIfNeeded();

                if (observableCollection != null)
                    observableCollection.CollectionChanged += value;
            }
            remove 
            { 
                if (observableCollection != null)
                    observableCollection.CollectionChanged -= value;
            }
        }

        private ObservableCollection<TObjectType> observableCollection = null;

        private void CreateObservableCollectionIfNeeded()
        {
            if (observableCollection == null)
                observableCollection = new ObservableCollection<TObjectType>(InnerGetEnumerable());
        }

        #endregion
    }

    #endregion

    #region ReferenceSet

    /// <summary>
    /// This is the main implementation class for IReferenceSet{TObjectType} type objects.  It is derived from SetBase{TObjectType}.
    /// It provides the additional methods that are required to support the IReferenceSet{TObjectType} interface.  
    /// Most of the internals for this class are actually defined in the SetBase{TObjectType} class.
    /// </summary>
    public class ReferenceSet<TObjectType> 
        : SetBase<TObjectType>, IReferenceSet<TObjectType>
    {
        /// <summary>
        /// Standard constructor.  Caller provides setID and capacity to use and passes a registerSelf boolean to indicate if this class should register itself with Interconnect.Sets.Sets.Instance.
        /// If registered, this class will automatically unregister itself when it is Disposed explicitly.
        /// </summary>
        public ReferenceSet(SetID setID, int capacity, bool registerSelf = true)
            : this(setID, capacity, (registerSelf ? Sets.Instance : null))
        {}

        /// <summary>
        /// Alternate standard constructor.  Caller provides the setID and capacity to use and provides the, possibly null, Sets instance that this set is to register itself with.
        /// </summary>
        public ReferenceSet(SetID setID, int capacity, ISetsInterconnection registerSelfWithSetsInstance)
            : base(setID, SetType.Reference, SetChangeability.Changeable, UpdateState.Initial, createMutex: true)
        {
            InnerSetCapacity(capacity);

            if (registerSelfWithSetsInstance != null)
            {
                registerSelfWithSetsInstance.RegisterSet(this);
                thisSetIsRegisteredWith = registerSelfWithSetsInstance;
            }

            AssignNewSetSeqNum();

            AddExplicitDisposeAction(() => 
                {
                    Clear();
                    SetFixed();
                    UnregisterSelf();
                });
        }

        /// <summary>this field holds the Sets instance that this Set registered itself with.  This is used when unregistering so that we know which Sets instance to Unregister from.</summary>
        private ISetsInterconnection thisSetIsRegisteredWith = null;

        /// <summary>
        /// This method unregisters this set from the previously stored Sets instance that this set was registered with, if any.  
        /// Once this set has successfully unregistered itself, it clears the reference to the set it had been registered with so that it cannot attempt to unregister twice.
        /// Calls to this method on a set that has not been registered or which is no longer registered will be ignored.
        /// </summary>
        public void UnregisterSelf()
        {
            if (thisSetIsRegisteredWith != null)
            {
                thisSetIsRegisteredWith.UnregisterSet(this);
                thisSetIsRegisteredWith = null;
            }
        }

        /// <summary>This method adds one or more given items to the set</summary>
        /// <exception cref="System.NotSupportedException">Thrown if the set's Changeability has been set to Fixed.</exception>
        public IReferenceSet<TObjectType> Add(params TObjectType[] addItemsArray)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            lock (itemContainerListMutex)
            {
                InnerAdd(addItemsArray);

                AssignNewSetSeqNum();
            }

            return this;
        }

        /// <summary>
        /// Removes the first item in the set for which the given matchPredicate returns true.  
        /// If the given matchPredicate is passed as null then this method removes the first element from the set.
        /// Returns true if an element was found and removed, or false otherwise.
        /// </summary>
        /// <exception cref="System.NotSupportedException">Thrown if the set's Changeability has been set to Fixed.</exception>
        public bool RemoveFirst(Func<TObjectType, bool> matchPredicate)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            lock (itemContainerListMutex)
            {
                bool removeResult = InnerRemoveFirst(matchPredicate);

                AssignNewSetSeqNum();

                return removeResult; 
            }
        }

        /// <summary>
        /// Removes each of the items in the set for which the given matchPredicate returns true.
        /// If the given matchPredicate is passed as null then this method removes all of the elements from the set.
        /// Returns true if any elements were removed from the set or false otherwise.
        /// </summary>
        /// <exception cref="System.NotSupportedException">Thrown if the set's Changeability has been set to Fixed.</exception>
        public bool RemoveAll(Func<TObjectType, bool> matchPredicate)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            lock (itemContainerListMutex)
            {
                bool removeResult = InnerRemoveAll(matchPredicate, true);

                AssignNewSetSeqNum();

                return removeResult;
            }
        }

        /// <summary>Removes the indicated item from the set (by its effective array index).</summary>
        /// <exception cref="System.NotSupportedException">Thrown if the set's Changeability has been set to Fixed.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the given index is not between 0 and the current Count - 1.</exception>
        public void RemoveAt(int index)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            lock (itemContainerListMutex)
            {
                InnerRemoveRange(index, 1, true);

                AssignNewSetSeqNum();
            }
        }

        /// <summary>
        /// Combination method.  If a non-null <paramref name="itemsToRemoveMatchPredicate"/> predicate is given then this method first removes all items from the set for which the given predicate returns true.  
        /// Then this method adds (appends) the given set of <paramref name="itemsToAdd"/> to the set, performing additional capacity limit triggered pruning as required.
        /// Pass null for the <paramref name="itemsToRemoveMatchPredicate"/> allows this method to be used as a form of AddRange
        /// </summary>
        /// <exception cref="System.NotSupportedException">Thrown if the set's Changeability has been set to Fixed.</exception>
        public void RemoveAndAdd(Func<TObjectType, bool> itemsToRemoveMatchPredicate, params TObjectType[] itemsToAdd)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            lock (itemContainerListMutex)
            {
                if (itemsToRemoveMatchPredicate != null)
                    InnerRemoveAll(itemsToRemoveMatchPredicate, true);

                InnerAdd(itemsToAdd);

                AssignNewSetSeqNum();
            }
        }

        /// <summary>
        /// Removes the given set of items (referenced by their sequence numbers) and adds the given set of <paramref name="itemsToAdd"/>.  
        /// If any items are added then this method returns the sequence number of the first added item.  
        /// Following added item sequence numbers are sequentially assigned.
        /// If no items are added then this method returns the sequence number that will be assigned to the next item that is added to the set.
        /// </summary>
        public Int64 RemoveBySeqNumsAndAddItems(Int64[] removeSeqNumSetArray, params TObjectType[] itemsToAdd)
        {
            ThrowOnFixedOrDoesNotHaveAMutex();

            lock (itemContainerListMutex)
            {
                InnerRemoveSeqNumSet(removeSeqNumSetArray, true);

                Int64 nextItemSeqNum = PeekNextItemSeqNum;

                InnerAdd(itemsToAdd);

                AssignNewSetSeqNum();

                return nextItemSeqNum;
            }
        }

        /// <summary>Changes the Changeability of the set to be SetChangeability.Fixed thus making the set read-only.</summary>
        public ISet<TObjectType> SetFixed()
        {
            lock (itemContainerListMutex)
            {
                if (Changeability != SetChangeability.Fixed)
                {
                    Changeability = SetChangeability.Fixed;

                    AssignNewSetSeqNum();
                }
            }

            return this;
        }

        /// <summary>Creates and returns a tracking set that can be used to track this reference set.</summary>
        ITrackingSet ITrackableSet.CreateTrackingSet()
        {
            return CreateTrackingSet();
        }

        /// <summary>Creates and returns a tracking set that can be used to track this reference set.</summary>
        public ITrackingSet<TObjectType> CreateTrackingSet()
        {
            lock (itemContainerListMutex)
            {
                return new TrackingSet<TObjectType>(this);
            }
        }
    }

    #endregion

    #region TrackingSet

    /// <summary>
    /// This is the main implementation class for ITrackingSet{TObjectType} type objects.  It is derived from SetBase{TObjectType}.
    /// It provides the additional methods that are required to support the ITrackingSet{TObjectType} interface.
    /// </summary>
    public class TrackingSet<TObjectType> 
        : SetBase<TObjectType>, ITrackingSet<TObjectType>
    {
        #region Construction and private fields

        // constructor used for WCF will need to be able to create a mutex since when a tracking set is used to track another tracking set, the source set will need a mutex so that
        //   other tracking sets can update themselves from the reference one without needing to know that they are using the same thread as it uses to update itself.

        /// <summary>Constructor used by the ReferenceSet{TObjectType}'s CreateTrackingSet method(s).  Constructs this tracking set to track the given IReferenceSet{TObjectType} as its source.</summary>
        internal TrackingSet(ReferenceSet<TObjectType> referenceSet)
            : base(referenceSet.SetID, SetType.Tracking, SetChangeability.Changeable, UpdateState.Initial, createMutex: false)
        {
            trackingSourceReferenceSet = referenceSet;
            trackingSourceSet = referenceSet;
            SourceSetType = trackingSourceSet.SetType;
            SourceChangeability = trackingSourceSet.Changeability;

            CreateDCAIfSupported();

            if (referenceSet.Capacity > 0)
                Capacity = referenceSet.Capacity;
        }

        /// <summary>Constructor that allows one tracking set to be constructed to track another tracking set.</summary>
        internal TrackingSet(TrackingSet<TObjectType> trackingSet)
            : base(trackingSet.SetID, SetType.Tracking, SetChangeability.Changeable, UpdateState.Initial, createMutex: false)
        {
            trackingSourceTrackingSet = trackingSet;
            trackingSourceSet = trackingSet;
            SourceSetType = trackingSourceSet.SetType;
            SourceChangeability = trackingSourceSet.Changeability;

            CreateDCAIfSupported();

            if (trackingSet.Capacity > 0)
                Capacity = trackingSet.Capacity;
        }

        /// <summary>Constructor to support derived classes.  Also used for remoting and unit tests.</summary>
        public TrackingSet(SetID setID, SetType setType, ISetsInterconnection registerSelfWithSetsInstance = null, bool createMutex = false, int initialCapacity = 0)
            : base(setID, setType, SetChangeability.Changeable, UpdateState.Initial, createMutex: createMutex)
        {
            CreateDCAIfSupported();

            if (initialCapacity > 0)
                Capacity = initialCapacity;

            if (registerSelfWithSetsInstance != null)
                registerSelfWithSetsInstance.RegisterSet(this);
        }

        /// <summary>When this field has been set to non-null, it gives the reference set that is the source set for this tracking set</summary>
        protected IReferenceSet<TObjectType> trackingSourceReferenceSet = null;

        /// <summary>When this field has been set to non-null, it gives the tracking set that is the source set for this tracking set</summary>
        protected ITrackingSet<TObjectType> trackingSourceTrackingSet = null;

        /// <summary>This gives the common representation of the source set that this set is tracking.  Will be the same instance as the trackingSourceReferenceSet or the trackingSourceTrackingSet.</summary>
        protected ISet<TObjectType> trackingSourceSet = null;

        /// <summary>This field contains the copy set that was last obtained by this set from its source set.</summary>
        private SetBase<TObjectType> lastSetCopy = null;

        /// <summary>Contains the SeqNum from the trackingSourceSet from the last time we obtained a set copy</summary>
        private Int64 lastSetCopySeqNum = 0;

        /// <summary>Gives a local copy of the lastSetCopy.SeqNumRangeInfo taken when the last set copy was made.</summary>
        private SeqNumRangeInfo lastSetCopySeqNumRangeInfo;

        /// <summary>shorthand version typeof(TObjectType)</summary>
        private static readonly Type TObjectTypeType = typeof(TObjectType);

        /// <summary>constructed as true, if and only if, TObjectType IsSerializable or it has a DataContractAttribute defined directly or inherited from a base class</summary>
        private static readonly bool TObjectTypeIsSerializable = (TObjectTypeType.IsSerializable || TObjectTypeType.IsDefined(typeof(DataContractAttribute), true));

        #endregion

        #region interface implementation methods

        /// <summary>Returns true if the source set for this set has been updated since this set last finished updating, or if the last update iteration was only partially complete.</summary>
        public virtual bool IsUpdateNeeded
        {
            get 
            {
                if (trackingSourceSet == null)      // case for use of a TrackingSet as a reference set (remoting)
                    return false;

                if (lastSetCopySeqNum != trackingSourceSet.SeqNum)
                    return true;

                using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
                {
                    return ((lastSetCopy == null) || !(itemListSeqNumRangeInfo.IsEqualTo(lastSetCopy.ItemListSeqNumRangeInfo)));
                }
            }
        }

        /// <summary>
        /// Performs an update iteration.  When non-zero maxDeltaItemCount is used to specify the maximum number of incremental set changes that can be applied per iteration.
        /// Supports call chaining.
        /// </summary>
        public virtual ITrackingSet PerformUpdateIteration(int maxDeltaItemCount = 0)
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                InnerPerformUpdateIteration(maxDeltaItemCount, false);
            }

            return this;
        }

        ISetDelta ITrackingSet.PerformUpdateIteration(int maxDeltaItemCount, bool generateSetDelta)
        {
            return PerformUpdateIteration(maxDeltaItemCount: maxDeltaItemCount, generateSetDelta: generateSetDelta);
        }

        /// <summary>
        /// Performs an update iteration and optionally generates the corresponding ISetDelta object.
        /// When non-zero <paramref name="maxDeltaItemCount"/> is used to specify the maximum number of incremental set changes that can be applied per iteration.
        /// When <paramref name="generateSetDelta"/> is false the method will return null.  When <paramref name="generateSetDelta"/> is true, the method will attempt to create and return an ISetDelta to represent the incremental update deltas.  If there are no such changes the method returns null.
        /// </summary>
        public virtual ISetDelta<TObjectType> PerformUpdateIteration(int maxDeltaItemCount, bool generateSetDelta)
        {
            SetDelta<TObjectType> setDelta = null;

            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                setDelta = InnerPerformUpdateIteration(maxDeltaItemCount, createSetDelta: generateSetDelta);
            }

            return setDelta;
        }

        /// <summary>
        /// This is the common method that is used to perform an update iteration.  The caller may provide a non-zero maxDeltaItemCount which then limits the number of items 
        /// that this iteration can remove from the set or add to the set so that together they does not exceed the given number.  Contiguous ranges of removed items are treated as a single
        /// delta item in the count, while each added item counts as one delta item.
        /// When createSetDelta is passed as true, this method will also create and return a SetDelta which records the updates that were applied to this tracking set during this iteration.
        /// </summary>
        private SetDelta<TObjectType> InnerPerformUpdateIteration(int maxDeltaItemCount, bool createSetDelta)
        {
            int deltaItemCount = 0;

            addedItemContainerList = addedItemContainerList ?? new List<ItemContainer>();
            removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();

            if (lastSetCopySeqNum != trackingSourceSet.SeqNum)
            {
                lastSetCopy = trackingSourceSet.CreateCopy();
                lastSetCopySeqNum = lastSetCopy.SeqNum;
                lastSetCopySeqNumRangeInfo = lastSetCopy.ItemListSeqNumRangeInfo;

                SourceChangeability = lastSetCopy.SourceChangeability;
            }

            UpdateState = Interconnect.Sets.UpdateState.InProgress;

            // do work here
            SetDelta<TObjectType> setDelta = null;

            if (lastSetCopy != null && !itemListSeqNumRangeInfo.IsEqualTo(lastSetCopySeqNumRangeInfo))
            {
                if (createSetDelta)
                    setDelta = new SetDelta<TObjectType>() { SetID = SetID, SourceUpdateState = lastSetCopy.UpdateState, SourceSetCapacity = Capacity };

                // detect and handle ClearSetAtStart conditions
                if ((lastSetCopySeqNumRangeInfo.Count == 0 && itemListSeqNumRangeInfo.Count != 0) || (lastSetCopySeqNumRangeInfo.First > itemListSeqNumRangeInfo.Last))
                {
                    if (itemContainerList.Count > 0)
                    {
                        if (setDelta != null)
                            setDelta.ClearSetAtStart = true;

                        InnerRemoveAll(null, true);
                    }
                }

                // determine which items need to be removed, remove them, and record them in the setDelta
                int removalScanIndex = 0;
                while (removalScanIndex < itemContainerList.Count && (maxDeltaItemCount == 0 || deltaItemCount < maxDeltaItemCount))
                {
                    InnerFindAndHandleNextRemovedRange(ref removalScanIndex, ref deltaItemCount, setDelta);
                }

                // determine which items need to be added, add as many items as needed and/or we are allowed to, and record them in the setDelta
                int addScanIndex = itemContainerList.Count;
                while (addScanIndex < lastSetCopy.itemContainerList.Count && (maxDeltaItemCount == 0 || deltaItemCount < maxDeltaItemCount))
                {
                    InnerAddNextContiguousRangeItem(maxDeltaItemCount, ref deltaItemCount, setDelta, ref addScanIndex);
                }

                // if needed serialize the items placed into the setDelta.

                bool haveRemovedItems = (removedItemContainerList.Count > 0);
                bool haveAddedItems = (addedItemContainerList.Count > 0);

                if (haveRemovedItems)
                    InnerNoteItemsRemoved(removedItemContainerList);

                if (haveAddedItems)
                    InnerNoteItemsAdded(addedItemContainerList);
            }

            AssignNewSetSeqNum();

            if (!IsUpdateNeeded)
            {
                UpdateState = Interconnect.Sets.UpdateState.Complete.MergeWith(trackingSourceSet.UpdateState);
            }

            return setDelta;
        }

        /// <summary>
        /// This method is used iteratively to find, record and remove the next block if contiguous items that are currently in the tracking set but which are no longer in the
        /// lastSetCopy.  If setDelta is non-null, a new SetDeltaRemoveRangeItem will be added to it.
        /// </summary>
        private void InnerFindAndHandleNextRemovedRange(ref int removalScanIndex, ref int deltaItemCount, SetDelta<TObjectType> setDelta)
        {
            // First start at the given removalSweepIndex and look in both the lastSetCopy and this item list for the first item that does not agree
            // WARNING: we cannot use the local itemListSeqNumRangeInfo.Count because it is not updated in the middle of doing a series of non-sequential removals
            int regionStartIdx = removalScanIndex;
            int regionEndIdx = Math.Min(itemContainerList.Count, lastSetCopySeqNumRangeInfo.Count) - 1;

            if (regionEndIdx < 0)
            {
                // there is nothing that can be removed
            }
            else if (regionStartIdx >= lastSetCopy.itemContainerList.Count)
            {
                // The first item already comes after the last item in the lastSetCopy - remove to end
            }
            else if (itemContainerList[regionStartIdx].SeqNum != lastSetCopy.itemContainerList[regionStartIdx].SeqNum)
            {
                // The first items differ. start removal at the current scan index
            }
            else if (itemContainerList[regionEndIdx].SeqNum != lastSetCopy.itemContainerList[regionEndIdx].SeqNum)
            {
                // The first items are the same but the last items are different.  
                // use binary search to find the last item pair index where the item sequence numbers match then start removing at the next location.
                for (; ; )
                {
                    if (regionStartIdx >= (regionEndIdx - 1))       // when the two positions are next to each other then we are done.
                        break;

                    int nextTestIdx = (regionStartIdx + regionEndIdx) >> 1;

                    if (itemContainerList[nextTestIdx].SeqNum == lastSetCopy.itemContainerList[nextTestIdx].SeqNum)
                        regionStartIdx = nextTestIdx;
                    else
                        regionEndIdx = nextTestIdx;
                }

                removalScanIndex = regionStartIdx + 1;
            }
            else
            {
                // nothing needs to be removed at all from the current set begining at the given scan index.  Advance the sweep index past the end of the shorter set.
                removalScanIndex = regionEndIdx + 1;
            }

            int removalCount = 0;
            long removalStartSeqNum = itemContainerList[regionStartIdx].SeqNum;

            // Find a run of elements at the removalScanIndex, until we find one tht has the same sequence number as the starting index position in the lastSetCopy's list.
            //  Any element in the current list that comes after the end of the lastSetCopy list needs to be removed.
            while ((removalScanIndex + removalCount) < itemContainerList.Count
                   && (itemContainerList[removalScanIndex + removalCount].SeqNum == (removalStartSeqNum + removalCount))
                   && ((removalScanIndex >= lastSetCopy.itemContainerList.Count) || (itemContainerList[removalScanIndex + removalCount].SeqNum != lastSetCopy.itemContainerList[removalScanIndex].SeqNum)))
            {
                removalCount++;
            }

            if (removalCount > 0)
            {
                if (setDelta != null)
                {
                    Interconnect.Sets.SetDeltaRemoveRangeItem removeRangeItem = new Interconnect.Sets.SetDeltaRemoveRangeItem() { RangeStartSeqNum = itemContainerList[removalScanIndex].SeqNum, RangeStartIndex = removalScanIndex, Count = removalCount };
                    setDelta.removeRangeItemList.Add(removeRangeItem);
                }

                InnerRemoveRange(removalScanIndex, removalCount, false);

                deltaItemCount++;
            }
        }

        /// <summary>
        /// This method is used to find and add the next (size limited) set of contiguous items that are in the lastSetCopy but which are not yet in the tracking set.
        /// If setDelta is non-null, a new SetDeltaAddContiguousRangeItem will be added to it.
        /// </summary>
        private void InnerAddNextContiguousRangeItem(int maxDeltaItemCount, ref int deltaItemCount, SetDelta<TObjectType> setDelta, ref int addScanIndex, int maxItemsToAdd = 100)
        {
            ItemContainer itemContainer = lastSetCopy.itemContainerList[addScanIndex];
            Int64 seqNum = itemContainer.SeqNum;
            SetDeltaAddContiguousRangeItem<TObjectType> addContigRangeItem = null;

            if (setDelta != null)
                addContigRangeItem = new SetDeltaAddContiguousRangeItem<TObjectType>() { RangeStartIndex = addScanIndex, RangeStartSeqNum = seqNum };

            itemContainer.LastIndexOfItemInList = itemContainerList.Count;

            itemContainerList.Add(itemContainer);
            addedItemContainerList.Add(itemContainer);

            if (addContigRangeItem != null)
                addContigRangeItem.rangeObjectList.Add(itemContainer.Item);

            addScanIndex++;
            seqNum++;
            deltaItemCount += 1;

            int addedItemCount = 1;

            while (addScanIndex < lastSetCopy.itemContainerList.Count && (maxDeltaItemCount == 0 || deltaItemCount < maxDeltaItemCount) && (maxItemsToAdd == 0 || addedItemCount < maxItemsToAdd))
            {
                itemContainer = lastSetCopy.itemContainerList[addScanIndex];

                if (seqNum == itemContainer.SeqNum)
                {
                    itemContainer.LastIndexOfItemInList = itemContainerList.Count;

                    itemContainerList.Add(itemContainer);
                    addedItemContainerList.Add(itemContainer);

                    if (addContigRangeItem != null)
                        addContigRangeItem.rangeObjectList.Add(itemContainer.Item);

                    addScanIndex++;
                    seqNum++;
                    deltaItemCount++;
                    addedItemCount++;
                }
                else
                {
                    // the next item is no longer contiguous
                    break;
                }
            }

            if (setDelta != null)
                setDelta.addRangeItemList.Add(addContigRangeItem);
        }

        /// <summary>
        /// ApplyDeltasConfig set only property for use by derived classes (AdjustableTrackingSet in particular).
        /// </summary>
        protected ApplyDeltasConfig<TObjectType> ApplyDeltasConfig { set { applyDeltasConfig = value ?? defaultApplyDeltasConfig; } }

        private volatile ApplyDeltasConfig<TObjectType> applyDeltasConfig = defaultApplyDeltasConfig;
        private static readonly ApplyDeltasConfig<TObjectType> defaultApplyDeltasConfig = new ApplyDeltasConfig<TObjectType>(true, null);

        /// <summary>
        /// Accepts an ISetDelta object that was generated by an external ITrackingSet of the same object type as this one and applies the delta to this set.
        /// Supports call chaining.
        /// </summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or the given setDelta does not already contain deserialized items and TObjectType is not known to support use with DataContract serialization</exception>
        public virtual ITrackingSet ApplyDeltas(ISetDelta setDelta)
        {
            return ApplyDeltas(setDelta, checkIfSetDeltaIsFromExpectedSetID: true);
        }

        /// <summary>
        /// Accepts an ISetDelta object and applies it to the set.  If <paramref name="checkIfSetDeltaIsFromExpectedSetID"/> then this method enforces that the given <paramref name="setDelta"/> appears to have come from the expected tracking or reference set.
        /// Supports call chaining.
        /// </summary>
        /// <exception cref="SetUseException">Optionally thrown if the SetID in the given setDelta does not match this set's SetID or the given setDelta does not already contain deserialized items and TObjectType is not known to support use with DataContract serialization</exception>
        protected virtual ITrackingSet ApplyDeltas(ISetDelta setDelta, bool checkIfSetDeltaIsFromExpectedSetID)
        {
            if (checkIfSetDeltaIsFromExpectedSetID)
                ThrowIfDeltaSetIDDoesNotMatch(setDelta);

            InnerApplySetDelta(setDelta, applyDeltasConfig);

            return this;
        }

        ISetDelta ITrackingSet.GenerateInitializerSetDelta(int maxItemsPerRangeItem)
        {
            return GenerateInitializerSetDelta(maxItemsPerRangeItem: maxItemsPerRangeItem);
        }

        /// <summary>
        /// Generate and return a SetDelta that gives the entire current contents of the tracking set in a single set delta.  
        /// This is used for initializing new derived sets that are updated incrementally using set deltas if the root tracking set is already in use (and thus cannot be replaced)
        /// </summary>
        public ISetDelta<TObjectType> GenerateInitializerSetDelta(int maxItemsPerRangeItem = 100)
        {
            // the following code needs to be moved to the TrackingSet where it can generate a synthetic initialization SetDelta of the correct form.
            //  using the actual set contents and items.  
            SetDelta<TObjectType> setDelta = new SetDelta<TObjectType>()
            {
                SetID = SetID,
                ClearSetAtStart = true,
                SourceSetCapacity = Capacity,
                SourceUpdateState = UpdateState,
            };

            int currentSetCount = itemContainerList.Count;

            for (int addScanIndex = 0; addScanIndex < currentSetCount; )
            {
                ItemContainer itemContainer = itemContainerList[addScanIndex];
                Int64 seqNum = itemContainer.SeqNum;
                SetDeltaAddContiguousRangeItem<TObjectType> addContigRangeItem = new SetDeltaAddContiguousRangeItem<TObjectType>() { RangeStartIndex = addScanIndex, RangeStartSeqNum = seqNum };

                addContigRangeItem.rangeObjectList.Add(itemContainer.Item);

                setDelta.addRangeItemList.Add(addContigRangeItem);

                addScanIndex++;
                seqNum++;

                while (addScanIndex < currentSetCount && (maxItemsPerRangeItem == 0 || addContigRangeItem.rangeObjectList.Count < maxItemsPerRangeItem))
                {
                    itemContainer = lastSetCopy.itemContainerList[addScanIndex];

                    if (seqNum == itemContainer.SeqNum)
                    {
                        addContigRangeItem.rangeObjectList.Add(itemContainer.Item);
                        addScanIndex++;
                        seqNum++;
                    }
                    else
                    {
                        // the next item is no longer contiguous
                        break;
                    }
                }
            }

            return setDelta;
        }

        /// <summary>
        /// This method processes the given setDelta removes each of the ISetDeltaRemoveRangeItem from the set (if allowRemove is true) 
        /// and then adds each of the ISetDeltaAddContiguousRangeItem to it.
        /// </summary>
        protected ISetDelta InnerApplySetDelta(ISetDelta setDelta, ApplyDeltasConfig<TObjectType> applyDeltasConfig = null)
        {
            applyDeltasConfig = applyDeltasConfig ?? this.applyDeltasConfig;

            bool allowRemove = applyDeltasConfig.AllowItemsToBeRemoved;
            Func<TObjectType, bool> addItemFilter = applyDeltasConfig.AddItemFilter;

            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                addedItemContainerList = addedItemContainerList ?? new List<ItemContainer>();
                removedItemContainerList = removedItemContainerList ?? new List<ItemContainer>();

                if (allowRemove)
                {
                    if (setDelta.ClearSetAtStart)
                    {
                        InnerRemoveAll(null, false);
                    }

                    if (setDelta.RemoveRangeItems != null)
                    {
                        foreach (ISetDeltaRemoveRangeItem removeRangeItem in setDelta.RemoveRangeItems)
                        {
                            InnerRemoveSeqNumRange(removeRangeItem.RangeStartSeqNum, removeRangeItem.RangeStartSeqNum + removeRangeItem.Count - 1, false);
                        }
                    }
                }

                if (setDelta.AddRangeItems != null)
                {
                    foreach (ISetDeltaAddContiguousRangeItem addRangeItem in setDelta.AddRangeItems)
                    {
                        Int64 itemSeqNum = addRangeItem.RangeStartSeqNum;

                        List<ItemContainer> itemsContainersToAddList = new List<ItemContainer>();

                        if (addRangeItem.RangeObjects != null)
                        {
                            foreach (object o in addRangeItem.RangeObjects)
                            {
                                TObjectType item = (o is TObjectType) ? ((TObjectType)o) : default(TObjectType);

                                if (ApplyFilterToItem(addItemFilter, item))
                                {
                                    ItemContainer itemContainer = new ItemContainer(itemSeqNum++, item);

                                    itemsContainersToAddList.Add(itemContainer);
                                }
                                else
                                {
                                    itemSeqNum++;       //  record the skipped sequence numbers but do not create or retain the actual ItemContainers for them.
                                }
                            }
                        }

                        InnerMakeSpacePriorToAdd(itemsContainersToAddList.Count);

                        foreach (ItemContainer itemContainer in itemsContainersToAddList)
                        {
                            itemContainer.LastIndexOfItemInList = itemContainerList.Count;
                            itemContainer.RemovedFromSetPosition = RemovedFromSetPosition.Last;

                            itemContainerList.Add(itemContainer);
                            addedItemContainerList.Add(itemContainer);
                        }
                    }
                }

                bool haveRemovedItems = (removedItemContainerList.Count > 0);
                bool haveAddedItems = (addedItemContainerList.Count > 0);

                if (haveRemovedItems)
                    InnerNoteItemsRemoved(removedItemContainerList);

                if (haveAddedItems)
                    InnerNoteItemsAdded(addedItemContainerList);

                AssignNewSetSeqNum();
            }

            return setDelta;
        }

        /// <summary>
        /// Evaluates the filter and returns the value it returned.
        /// If defined, the filter delegate is invoked in a try/catch body that returns false if the filter threw any recognized exception.
        /// </summary>
        private bool ApplyFilterToItem(Func<TObjectType, bool> filter, TObjectType item)
        {
            bool result = false;

            try
            {
                result = (filter == null || filter(item));
            }
            catch { }

            return result;
        }

        /// <summary>Creates and returns a tracking set that can be used to track this tracking set set.</summary>
        ITrackingSet ITrackableSet.CreateTrackingSet()
        {
            return CreateTrackingSet();
        }

        /// <summary>Creates and returns a tracking set that can be used to track this tracking set set.</summary>
        public ITrackingSet<TObjectType> CreateTrackingSet()
        {
            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                return new TrackingSet<TObjectType>(this);
            }
        }

        /// <summary>Provides a public version of the protected PropertyChanged event that is implemented at the SetBase level</summary>
        public new event PropertyChangedEventHandler PropertyChanged 
        {
            add { base.PropertyChanged += value; }
            remove { base.PropertyChanged -= value; }
        }

        /// <summary>Provides a public version of the protected CollectionChanged event that is implemented at the SetBase level</summary>
        public new event NotifyCollectionChangedEventHandler CollectionChanged 
        {
            add { base.CollectionChanged += value; }
            remove { base.CollectionChanged -= value; }
        }

        #endregion

        #region SetDelta serialization support

        /// <summary>Accepts an ISetDelta object and serializes it into the given <paramref name="intoStream"/>.  Currently uses JSON DataContract serialization.</summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or if TObjectType is not known to support use with DataContract serialization</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's WriteObject method that is used here (aka There is a problem with the instance being serialized).</exception>
        /// <exception cref="System.Runtime.Serialization.InvalidDataContractException">The type being serialized does not conform to data contract rules. For example, the System.Runtime.Serialization.DataContractAttribute attribute has not been applied to the type.</exception>
        /// <exception cref="System.ServiceModel.QuotaExceededException">The maximum number of objects to serialize has been exceeded. Check the System.Runtime.Serialization.DataContractSerializer.MaxItemsInObjectGraph property.</exception>
        public void Serialize(ISetDelta setDelta, System.IO.Stream intoStream)
        {
            ThrowIfDeltaSetIDDoesNotMatch(setDelta);
            ThrowIfTObjectTypeIsNotUsableWithDataContractSerialization();

            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                if (setDelta is SetDelta<TObjectType>)
                    dca.WriteObject((SetDelta<TObjectType>)setDelta, intoStream);
                else
                    dca.WriteObject(new SetDelta<TObjectType>(setDelta), intoStream);
            }
        }

        /// <summary>Deserializes, and returns, an ISetDelta object from the given <paramref name="fromStream"/> and returns it.  Uses the internally constructed JSON DataContract deserialization object.</summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or if TObjectType is not known to support use with DataContract serialization</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">May be thrown by the underlying Data contract serialization object's ReadObject method that is used here.</exception>
        public ISetDelta Deserialize(System.IO.Stream fromStream)
        {
            ThrowIfTObjectTypeIsNotUsableWithDataContractSerialization();

            using (ScopedLock sc = new ScopedLock(itemContainerListMutex))
            {
                SetDelta<TObjectType> setDelta = dca.ReadObject(fromStream);
                return setDelta;
            }
        }

        /// <summary>IDataContractAdapter created at construction time that may be used by this set to perform serialization and deserialization.</summary>
        private IDataContractAdapter<SetDelta<TObjectType>> dca = null;

        /// <summary>This method creates a JSON DataContract Adapter if TObjectTypeIsSerializable is true (aka if TObjectType IsSerializable or it has a DataContractAttribute defined directly or inherited from a base class)</summary>
        private void CreateDCAIfSupported()
        {
            if (TObjectTypeIsSerializable)
            {
                dca = new DataContractJsonAdapter<SetDelta<TObjectType>>();
            }
        }

        /// <summary>Throws a SetUseException if no IDataContractAdapter was constructed for these tracking set, typically because TObjectType is not known to support use with DataContract serialization.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        protected void ThrowIfTObjectTypeIsNotUsableWithDataContractSerialization()
        {
            if (dca == null)
            {
                string methodName = new System.Diagnostics.StackFrame(1).GetMethod().Name;
                new SetUseException("{0}/{1}: {2} cannot be used with this set: TObjectType:{3} is not compatible with DataContract serialization".CheckedFormat(SetID, SetType, methodName, TObjectTypeType)).Throw();
            }
        }

        #endregion

        #region SetDelta and related implementation classes, methods

        /// <summary>Throws a SetUseException if the SetID in the given setDelta does not match this set's SetID</summary>
        protected void ThrowIfDeltaSetIDDoesNotMatch(ISetDelta setDelta)
        {
            if (!SetID.IsEqualTo(setDelta.SetID))
            {
                new SetUseException("{0} {1} cannot be used with an ISetDelta from {2}".CheckedFormat(SetID, SetType, setDelta.SetID)).Throw();
            }
        }

        #endregion
    }

    #endregion

    #region SetDelta related implementation classes (SetDelta, SetDeltaRemoveRangeItem, SetDeltaAddContiguousRangeItem)

    /// <summary>Internal storage object for ISetDelta</summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public class SetDelta<TObjectType> : ISetDelta<TObjectType>
    {
        /// <summary>Default constructor.</summary>
        public SetDelta()
        { }

        /// <summary>Copy constructor.</summary>
        public SetDelta(ISetDelta other)
        {
            SetID = other.SetID;
            SourceUpdateState = other.SourceUpdateState;
            ClearSetAtStart = other.ClearSetAtStart;
            SourceSetCapacity = other.SourceSetCapacity;

            var otherRemoveRangeItems = other.RemoveRangeItems;
            var otherAddRangeItems = other.AddRangeItems;

            if (otherRemoveRangeItems != null)
                removeRangeItemList.AddRange(otherRemoveRangeItems.Select(item => new SetDeltaRemoveRangeItem(item)));

            if (otherAddRangeItems != null)
                addRangeItemList.AddRange(otherAddRangeItems.Select(item => new SetDeltaAddContiguousRangeItem<TObjectType>(item)));
        }

        /// <summary>Gives the SetID of the source IReferenceSet that is being tracked and for which this delta object has been generated.</summary>
        [DataMember(Order = 100)]
        public SetID SetID { get; set; }

        /// <summary>Gives the UpdateState of the source state as last captured by the ITrackingSet that generated this delta object.</summary>
        [DataMember(Order = 200, IsRequired = false, EmitDefaultValue = false)]
        public UpdateState SourceUpdateState { get; set; }

        /// <summary>True if the ITrackingSet that generated this delta had been reset at the start of its corresponding update cycle.</summary>
        [DataMember(Order = 300, IsRequired = false, EmitDefaultValue = false)]
        public bool ClearSetAtStart { get; set; }

        /// <summary>Returns a sequence of ISetDeltaRemoveRangeItem objects that represent the set items to remove when applying this delta</summary>
        IEnumerable<ISetDeltaRemoveRangeItem> ISetDelta.RemoveRangeItems { get { return removeRangeItemList; } }

        /// <summary>Returns a sequence of ISetDeltaAddContiguousRangeItem objects that represent the set of items to append to this set when applying this delta.</summary>
        IEnumerable<ISetDeltaAddContiguousRangeItem> ISetDelta.AddRangeItems { get { return addRangeItemList != null ? addRangeItemList.Select(item => item as ISetDeltaAddContiguousRangeItem).ToArray() : null; } }

        /// <summary>Returns the total number of items that are reported as being removed in this set delta</summary>
        public int TotalRemovedItemCount { get { return (removeRangeItemList != null) ? removeRangeItemList.Sum(removeRangeItem => removeRangeItem.Count) : 0; } }

        /// <summary>Returns the total number of items that are reported as being added in this set delta</summary>
        public int TotalAddedItemCount { get { return (addRangeItemList != null) ? addRangeItemList.Sum(addRangeItem => addRangeItem.rangeObjectList.SafeCount()) : 0; } }

        /// <summary>Returns the source set's Capacity at the time that this set detla was created</summary>
        [DataMember(Order = 400, IsRequired = false, EmitDefaultValue = false)]
        public int SourceSetCapacity { get; set; }

        /// <summary>Returns a sequence of ISetDeltaAddContiguousRangeItem objects that represent the set of items to append to this set when applying this delta.</summary>
        IEnumerable<ISetDeltaAddContiguousRangeItem<TObjectType>> ISetDelta<TObjectType>.AddRangeItems { get { return addRangeItemList; } }

        /// <summary>gives internal access to the actual list of ISetDeltaRemoveRangeItems in this SetDelta instance</summary>
        [DataMember(Order = 500)]
        public List<SetDeltaRemoveRangeItem> removeRangeItemList = new List<SetDeltaRemoveRangeItem>();

        /// <summary>gives internal access to the actual list of ISetDeltaAddContiguousRangeItem in this SetDelta instance</summary>
        [DataMember(Order = 600)]
        public List<SetDeltaAddContiguousRangeItem<TObjectType>> addRangeItemList = new List<SetDeltaAddContiguousRangeItem<TObjectType>>();
    }

    /// <summary>Implementation class for ISetDeltaRemoveRangeItem</summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public class SetDeltaRemoveRangeItem : ISetDeltaRemoveRangeItem
    {
        public SetDeltaRemoveRangeItem() { }
        public SetDeltaRemoveRangeItem(ISetDeltaRemoveRangeItem other)
        {
            RangeStartSeqNum = other.RangeStartSeqNum;
            RangeStartIndex = other.RangeStartIndex;
            Count = other.Count;
        }

        /// <summary>Gives the item SeqNum of the first item in the contiguous range that is to be removed when processing this range.</summary>
        [DataMember(Order = 100)]
        public Int64 RangeStartSeqNum { get; set; }

        /// <summary>Gives the index into the source set of the first item in this contiguous range just before it was removed from the source set..</summary>
        [DataMember(Order = 200)]
        public Int32 RangeStartIndex { get; set; }

        /// <summary>Gives the count of the number of items in this range that are to be removed</summary>
        [DataMember(Order = 300)]
        public Int32 Count { get; set; }
    }

    /// <summary>Implementation object for ISetDeltaAddContiguousRangeItem</summary>
    [DataContract(Namespace = Constants.ModularInterconnectNameSpace)]
    public class SetDeltaAddContiguousRangeItem<TObjectType> : ISetDeltaAddContiguousRangeItem<TObjectType>
    {
        public SetDeltaAddContiguousRangeItem() { }
        public SetDeltaAddContiguousRangeItem(ISetDeltaAddContiguousRangeItem other)
        {
            RangeStartSeqNum = other.RangeStartSeqNum;
            RangeStartIndex = other.RangeStartIndex;

            if (other is SetDeltaAddContiguousRangeItem<TObjectType>)
            {
                SetDeltaAddContiguousRangeItem<TObjectType> otherAsThis = (SetDeltaAddContiguousRangeItem<TObjectType>)other;
                if (otherAsThis.rangeObjectList != null)
                    rangeObjectList = new List<TObjectType>(otherAsThis.rangeObjectList);
            }
            else
            {
                object[] otherRangeObjectArray = other.RangeObjects.SafeToArray(mapNullToEmpty: false);
                if (otherRangeObjectArray != null)
                    rangeObjectList = new List<TObjectType>(otherRangeObjectArray.Select(item => (item is TObjectType) ? (TObjectType)item : default(TObjectType)));
            }
        }

        /// <summary>Gives the item SeqNum of the first item in this contiguous range to be added to the set when processing this range</summary>
        [DataMember(Order = 100)]
        public Int64 RangeStartSeqNum { get; set; }

        /// <summary>Gives the index in the source set of the first item in this contiguous range at which to add this range of items</summary>
        [DataMember(Order = 200)]
        public Int32 RangeStartIndex { get; set; }

        /// <summary>gives internal access to the actual list of TObjectTypes items to add in this SetDelta instance (assuming that it contains deserialized objects)</summary>
        [DataMember(Order = 300)]
        public List<TObjectType> rangeObjectList = new List<TObjectType>();

        /// <summary>Gives the, possibly null or empty, sequence of deserialized objects that are to be added when processing this range</summary>
        IEnumerable<object> ISetDeltaAddContiguousRangeItem.RangeObjects { get { return ((rangeObjectList != null) ? rangeObjectList.Select((item) => item as object) : null); } }

        /// <summary>Gives the, possibly null or empty, sequence of deserialized objects that are to be added when processing this range</summary>
        IEnumerable<TObjectType> ISetDeltaAddContiguousRangeItem<TObjectType>.RangeObjects { get { return rangeObjectList; } }
    }

    #endregion

    #region ApplyDeltaConfig, AdjustableTrackingSet

    /// <summary>
    /// Configuration class used by AdjustableTrackingSet's ApplyDeltas method to configure some of its custom behavior
    /// </summary>
    public class ApplyDeltasConfig<TObjectType>
    {
        /// <summary>
        /// Constructor.  Caller must provide allowItemsToBeRemoved and addItemsFilter values.
        /// </summary>
        public ApplyDeltasConfig(bool allowItemsToBeRemoved, Func<TObjectType, bool> addItemFilter)
        {
            AllowItemsToBeRemoved = allowItemsToBeRemoved;
            AddItemFilter = addItemFilter;
        }

        /// <summary>
        /// If this boolean is set to false, it will prevent the AdjustableTrackingSet from removing items when applying a delta.  
        /// This causes the tracking set to become an accumulating tracking set.
        /// </summary>
        public bool AllowItemsToBeRemoved { get; private set; }

        /// <summary>
        /// When this filter is set to a non-null delegate, it will be used by the AdjustableTrackingSet to filter each item before actually ading it to the set.
        /// If the filter is null then all items will be added to the set.
        /// If the filter is non-null, the boolena value that it returns for each item will be used to determing this.  
        /// true means to accept and add the item while false means to skip the item (and thus not add it).
        /// <para/>NOTE: it is essential that any client provided filter is non-blocking.  
        /// This filter may be used on any thread that has permitted access to the tracking set's ApplyDeltas method and the tracking set may obtain ownership of one or more
        /// thread synchroniztaion primitives while using the filter.  
        /// As such any resulting use of non-thread safe data by the filter may cause an unexpected exception or other software fault,
        /// and any use of thread synchronization primitives by the filter may cause deadlock.
        /// </summary>
        public Func<TObjectType, bool> AddItemFilter { get; private set; }
    }

    /// <summary>
    /// This is a special type of ITrckingSet{TObjectType} that is directly derived from TrackingSet{TObjectType}
    /// This set gives the client control of the following construction time adjustable settings:
    /// <para/>The set's Capacity.  This must be defined during construction.
    /// <para/>The set's ApplyDeltasConfig.  This determines if items are removed from the set and/or allows the caller to define a filter for which items are added to the set durring each ApplyDeltas call.
    /// The ApplyDeltasConfig property can be changed after the set has been created and will be applied to ApplyDeltas calls that are performed after the propery has been changed.
    /// <para/>To refilter the entire set it is expected that the client will create a new AdjustableTrackingSet from the original source set.
    /// </summary>
    public class AdjustableTrackingSet<TObjectType> : TrackingSet<TObjectType>
    {
        /// <summary>Construtor variant.  caller must provide the set to track, the capacity of this set and the initail applyDeltasConfig instance to use (may be null).</summary>
        public AdjustableTrackingSet(ITrackableSet setToTrack, int capacity, ApplyDeltasConfig<TObjectType> applyDeltasConfig)
            : base(setToTrack.SetID, SetType.TrackingAccumulator)
        {
            trackingSourceReferenceSet = setToTrack as ReferenceSet<TObjectType>;
            trackingSourceTrackingSet = setToTrack as TrackingSet<TObjectType>;

            trackingSourceSet = (trackingSourceReferenceSet as ISet<TObjectType>) ?? (trackingSourceTrackingSet as ISet<TObjectType>);

            if (trackingSourceSet == null)
                new SetUseException("{0} type cannot be used with set {1}: {2] is not a supported type to track".CheckedFormat(this.GetType(), setToTrack.SetID, setToTrack.GetType())).Throw();

            SourceSetType = trackingSourceSet.SetType;
            SourceChangeability = trackingSourceSet.Changeability;

            Capacity = capacity;

            ApplyDeltasConfig = applyDeltasConfig;
        }

        /// <summary>Returns the set's UpdateState.  When using the two step update use model this returns the intermediate tracking set's UpdateState merged with this sets base one.</summary>
        public override UpdateState UpdateState
        {
            get { return (innerTrackingSet == null) ? base.UpdateState : innerTrackingSet.UpdateState.MergeWith(base.UpdateState); }
        }

        /// <summary>When using two step updates this returns the IsUpdateNeeded flag from the intermediate tracking set.  Otherwise this returns this sets base IsUpdateNeeded flag.</summary>
        public override bool IsUpdateNeeded
        {
            get { return (innerTrackingSet != null) ? innerTrackingSet.IsUpdateNeeded : base.IsUpdateNeeded; }
        }

        /// <summary>
        /// Performs an update iteration.  When non-zero maxDeltaItemCount is used to specify the maximum number of incremental set changes that can be applied per iteration.
        /// Supports call chaining.
        /// <para/>Use of this method is only permitted if the ApplyDeltas method has not already been used and use of this method makes future use of ApplyDeltas invalid.
        /// </summary>
        /// <exception cref="SetUseException">This method may throw a SetUseException if the ApplyDeltas method has been used already or if the underlying set to track is not of a supported type.</exception>
        public override ITrackingSet PerformUpdateIteration(int maxDeltaItemCount = 0)
        {
            if (useModel != UseModel.UseTwoStepUpdate)
            {
                switch (useModel)
                {
                    case UseModel.NotSet:
                        useModel = UseModel.UseTwoStepUpdate;
                        if (trackingSourceSet is ReferenceSet<TObjectType>)
                            innerTrackingSet = new TrackingSet<TObjectType>(trackingSourceSet as ReferenceSet<TObjectType>);
                        else if (trackingSourceSet is TrackingSet<TObjectType>)
                            innerTrackingSet = new TrackingSet<TObjectType>(trackingSourceSet as TrackingSet<TObjectType>);
                        else
                            new SetUseException("The underlying source set [{0}] set type is not supported with this object".CheckedFormat(trackingSourceSet)).Throw();
                        break;

                    case UseModel.ApplyDeltasExplicitly:
                    default:
                        new SetUseException("{0} cannot be used after its use has been set to {1}".CheckedFormat(Fcns.CurrentMethodName, useModel)).Throw();
                        break;
                }
            }

            if (innerTrackingSet != null)
            {
                ISetDelta setDelta = innerTrackingSet.PerformUpdateIteration(maxDeltaItemCount, generateSetDelta: true);

                if (setDelta != null)
                    base.ApplyDeltas(setDelta);
            }

            return this;
        }

        /// <summary>
        /// This method is not usable on AdjustableTrackingSet instances.  AdjustableTrackingSets only support use of ApplyDeltas on IDeltaSet instances that have been generated by some other ITrackingSet instance.
        /// </summary>
        /// <exception cref="SetUseException">This method always throws a SetUseException</exception>
        public override ISetDelta<TObjectType> PerformUpdateIteration(int maxDeltaItemCount, bool includeSerializedItems)
        {
            throw new SetUseException("The AdjustableTrackingSet does not support use of this part of the underlying ITrackingSet interface");
        }

        /// <summary>
        /// Accepts an ISetDelta object that was generated by an external ITrackingSet of the same object type as this one and applies the delta to this set.
        /// Supports call chaining.
        /// <para/>Use of this method is only permitted if the PerformUpdateIteration method has not already been used and use of this method makes future use of PerformUpdateIteration invalid.
        /// </summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or the given setDelta does not already contain deserialized items and TObjectType is not known to support use with DataContract serialization</exception>
        public override ITrackingSet ApplyDeltas(ISetDelta setDelta)
        {
            return ApplyDeltas(setDelta, checkIfSetDeltaIsFromExpectedSetID: true);
        }

        /// <summary>
        /// Accepts an ISetDelta object that was generated by an external ITrackingSet of the same object type as this one and applies the delta to this set.  Optinally enforces that the setDelta came from an expected source.
        /// Supports call chaining.
        /// <para/>Use of this method is only permitted if the PerformUpdateIteration method has not already been used and use of this method makes future use of PerformUpdateIteration invalid.
        /// </summary>
        /// <exception cref="SetUseException">Thrown if the SetID in the given setDelta does not match this set's SetID or the given setDelta does not already contain deserialized items and TObjectType is not known to support use with DataContract serialization</exception>
        public new ITrackingSet ApplyDeltas(ISetDelta setDelta, bool checkIfSetDeltaIsFromExpectedSetID)
        {
            if (useModel != UseModel.ApplyDeltasExplicitly)
            {
                switch (useModel)
                {
                    case UseModel.NotSet:
                        useModel = UseModel.ApplyDeltasExplicitly;
                        break;

                    case UseModel.UseTwoStepUpdate:
                    default:
                        new SetUseException("{0} cannot be used after its use has been set to {1}".CheckedFormat(Fcns.CurrentMethodName, useModel)).Throw();
                        break;
                }
            }

            return base.ApplyDeltas(setDelta, checkIfSetDeltaIsFromExpectedSetID: checkIfSetDeltaIsFromExpectedSetID);
        }

        /// <summary>
        /// Defines which update use model will be used with an Adjustable Tracking Set.
        /// <para/>NotSet (0), ApplyDeltasExplicitly, UseTwoStepUpdate
        /// </summary>
        private enum UseModel : int
        {
            /// <summary>Default value - client has not indicated which model they will use (either explicitly or implicitly)</summary>
            NotSet = 0,

            /// <summary>Client is using the ApplyDeltas based use model.</summary>
            ApplyDeltasExplicitly,

            /// <summary>Client is using the PerformUpdateIteration based use model.</summary>
            UseTwoStepUpdate,
        }

        /// <summary>Gives the current use model for this object.  NotSet, ApplyDeltasExplicitly or UseTwoStepUpdate</summary>
        private UseModel useModel = UseModel.NotSet;

        /// <summary>When UseTwoStepUpdate is selected, this is set to a new internal tracking set that is used to convert the originally given reference or tracking set into ISetDelta values so that they can be applied using the locally defined ApplyDeltasConfig value.</summary>
        private ITrackingSet<TObjectType> innerTrackingSet = null;

        /// <summary>
        /// Get/set property for changing the ApplyDeltasConfig{TObjectType} instance that this adjustable tracking set will use for its optional filtering and range removal control.
        /// </summary>
        public new ApplyDeltasConfig<TObjectType> ApplyDeltasConfig
        {
            get { return localApplyDeltasConfigStore; }
            set { localApplyDeltasConfigStore = base.ApplyDeltasConfig = value; }
        }

        private ApplyDeltasConfig<TObjectType> localApplyDeltasConfigStore = null;
    }

    #endregion

    #region ExtensionMethods

    /// <summary>partial static class contains (more) Extension Methods</summary>
    public static partial class ExtensionMethods
    {
        /// <summary>Returns true if the given changeability value == SetChangeability.Fixed</summary>
        public static bool IsFixed(this SetChangeability changeability)
        {
            return (changeability == SetChangeability.Fixed);
        }

        /// <summary>
        /// Merges the given lhs UpdateState with the given rhs one.
        /// For lhs values of Initial, Empty, or Complete, this method returns the rhs value.
        /// For lhs values of Failed, InProgress, or any other value, this method returns the lhs value.
        /// Generally this method is used to merge the lhs UpdateState from a source set with the current overlay UpdateState from a tracking set to produce the UpdateState that is reported by the tracking set.
        /// </summary>
        public static UpdateState MergeWith(this UpdateState lhs, UpdateState rhs)
        {
            switch (lhs)
            {
                case UpdateState.Failed:
                case UpdateState.InProgress:
                    return lhs;
                case UpdateState.Initial:
                case UpdateState.Empty:
                case UpdateState.Complete:
                    return rhs;
                default:
                    return lhs;
            }
        }
    }

    #endregion

    #endregion
}

//-------------------------------------------------------------------
