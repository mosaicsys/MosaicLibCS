//-------------------------------------------------------------------
/*! @file Interconnect/Values.cs
 *  @brief Defines a set of classes that are used to supported interconnecting value adapters.
 * 
 * Copyright (c) Mosaic Systems Inc.,  All rights reserved.
 * Copyright (c) 2015 Mosaic Systems Inc., All rights reserved.
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

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MosaicLib.Utils;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Reflection.Attributes;

// Modular.Interconnect is the general namespace for tools that help interconnect Modular Parts without requiring that that have preexisting knowledge of each-other's classes.
// This file contains the definitions for the underlying Modular.Interconnect.Values portion of Modular.Interconnect.
//  
//  Modular.Interconnect.Values provides a set of tools that are used to define one or more table spaces for interconnected values.  
//  These are implementations of the IValuesInterconnection interface that defines what capabilities such objects expose to client code.
//  
//  The Values static class contains a singleton Instance pattern so that there can always be a main value interconnection instance that other objects can use.  However all of the
//  helper classes that are used with IValueInterconnection objects support use with any externally provided instance so the client process can support multiple separate
//  interconnections if needed.
//
//  IValuesInterconnection objects serve as the factory for untyped and typed (using generics) IValueAccessor objects.  These objects represent a clients access to observe and/or set
//  a named value in the interconnection table space.  

namespace MosaicLib.Modular.Interconnect.Values
{
    #region IValuesIneterconnection

    /// <summary>
    /// This interface defines the basic externally visible methods and properties that are exposed by Values Interconnection implementation objects.
    /// Clients are generally expected to make most use of the GetValueAdapter factory methods methods.  
    /// ValueSetAdapter classes are generally expected to use the IValueAccessor array versions of the Set and Update methods.
    /// </summary>
    public interface IValuesIneterconnection
    {
        /// <summary>Returns the name of this values interconnection (table space) object.</summary>
        string Name { get; }

        /// <summary>
        /// IValueAdpater Factory method.  
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAdapter that can be used to observe and/or set the value of the named table entry.
        /// <para/>If the given name is null or empty then this method returns a stub IValueAdpater that is not attached to anything.
        /// </summary>
        IValueAccessor GetValueAccessor(string name);

        /// <summary>
        /// Typed (using generics) IValueAdpater{TValueType} Factory method.
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAdapter{TValueType} that can be used to observe and/or set the value of the named table entry.
        /// <para/>If the given name is null or empty then this method returns a stub IValueAdpater{TValueType} that is not attached to anything.
        /// </summary>
        IValueAccessor<TValueType> GetValueAccessor<TValueType>(string name);

        /// <summary>Returns an array of the names of all of the values in this interconnection table instance.</summary>
        string[] ValueNamesArray { get; }

        /// <summary>
        /// Returns a subset list of the overarll ValueNamesArray.  Allows the caller to get names that have been added since they last obtained a full array.
        /// If maxNumItems is passed as zero then the full set of names starting at the given startIdx will be returned.
        /// </summary>
        string [] GetValueNamesRange(int startIdx, int maxNumItems);

        /// <summary>Provides a property that returns the current total number of named values in this values interconnect table space</summary>
        int ValueNamesArrayLength { get; }

        /// <summary>
        /// This method is used to Set the table entry values for an array of IValueAdapter instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        void Set(IValueAccessor[] accessorArray, bool optimize);

        /// <summary>
        /// This method is used to Set the table entry values for a portion of an array of IValueAdapter instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="numEntriesToSet">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        void Set(IValueAccessor[] accessorArray, int numEntriesToSet, bool optimize);

        /// <summary>
        /// This method is used to Update a set/array of IValueAdapter instances from the corresponding set of interconnection table entry values.  
        /// This arrayed update operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Updated.</param>
        void Update(IValueAccessor[] accessorArray);

        /// <summary>
        /// This method is used to Update a set/array of IValueAdapter instances from the corresponding set of interconnection table entry values.  
        /// This arrayed update operation is performed atomically across the table entries referred to by the non-null adapters in the array up to the given maximum item index to update.
        /// <para/>This method is specifically intended for use by Custom update scanner instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Updated.</param>
        /// <param name="numEntriesToUpdate">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.</param>
        void Update(IValueAccessor[] accessorArray, int numEntriesToUpdate);

        /// <summary>Provides an IBasicNotificationList instance that will be Notified after each Set operation has been completed.</summary>
        IBasicNotificationList NotificationList { get; }

        /// <summary>Proides a sequence number that counts the total number of table-wide Set operations that have been performed.</summary>
        UInt32 GlobalSeqNum { get; }
    }

    #endregion

    #region IValueAccessor and IValueAccessor<TValueType>

    /// <summary>
    /// This interface defines the publicly available behavior for a ValueAdapter object, namely an object that is used to access a specific value in
    /// an interconnection table space.  This version provides access to the value as a System.Object.  
    /// </summary>
    public interface IValueAccessor
    {
        /// <summary>Gives the Name of the Value that this accessor is used to access.</summary>
        string Name { get; }

        /// <summary>Gives the ID number assigned to this Named value's table entry in the interconnection table space.</summary>
        int ID { get; }

        /// <summary>
        /// get/set property gives access to the value object that the accessor uses to contain the current value.
        /// Setter sets IsSetPending if the given value is not equal to the current one.
        /// <para/>This is a value object and as such this property can only be assigned an entire ValueContainer.
        /// </summary>
        ValueContainer ValueContainer { get; set; }

        /// <summary>
        /// get/set Property gives access to the value contained in the ValueContainer as a System.Object.
        /// getter returns the container's currently selected value field casted as an object.
        /// setter attempts to exctract the actual type of the given object and, if the type is recognized, convert and save the value in one the container's type specific fields
        /// otherwise the value is saved in the container's object field.
        /// Setter sets IsSetPending to true if the given value is not equal to the current one.
        /// </summary>
        object ValueAsObject { get; set; }

        /// <summary>This property is set whenever the ValueContainer is changed and is cleared whenver the update has been delievered to the ValueInterconnection instance or when Update is called.</summary>
        bool IsSetPending { get; }

        /// <summary>Sets the corresponding interconnection table space entry's value from this accessors current ValueContainer contents.  This method supports call chaining.</summary>
        IValueAccessor Set();
        /// <summary>Sets ValueContainer to the given value and then Sets the corresponding interconnection table space entry's value from the given one.  This method supports call chaining.</summary>
        IValueAccessor Set(ValueContainer valueContainer);
        /// <summary>Converts valueAsObject to a ValueContainer and then calls Set(valueContainer) to set the access and the corresponding interconnect table entry from it.  This method supports call chaining.</summary>
        IValueAccessor Set(object valueAsObject);
        /// <summary>Checks if the current ValueContainer is different than the given one.  If the are not Equal then calls Set(valueContainer) to set the local and interconnect values from the given one.  This method supports call chaining.</summary>
        IValueAccessor SetIfDifferent(ValueContainer valueContainer);
        /// <summary>Converts valueAsObject to a ValueContainer and then calls SetIfDifferent(valueContainer) to set the access and the corresponding interconnect table entry from it if needed.  This method supports call chaining.</summary>
        IValueAccessor SetIfDifferent(object valueAsObject);

        /// <summary>This property returns true if the LastUpdateSeqNun is not the same as the CurrentSeqNum.</summary>
        bool IsUpdateNeeded { get; }
        /// <summary>This method updates the locally stored value and seqNum from the interconnection table space's corresponding table entry.  This method supports call chaining.</summary>
        IValueAccessor Update();
        /// <summary>Gives the current sequence number of the value that is currently in the interconnection table.</summary>
        UInt32 CurrentSeqNum { get; }
        /// <summary>Gives the sequence number of the value that was last Set to, or updated from, the interconnection table.  The accessor may be updated if this value is not equal to CurrentSeqNum.</summary>
        UInt32 ValueSeqNum { get; }
        /// <summary>True if the corresponding table entry has been explicitly set to a value and this object has been Updated from it.  This is a synonym for ((ValueSeqNum != 0) || IsSetPending)</summary>
        bool HasValueBeenSet { get; }
    }

    /// <summary>
    /// Provides a type specific version of the underlying IValueAccessor which already includes the required casting operations.
    /// </summary>
    /// <typeparam name="TValueType">Defines the specific type that is to be used with the Value property.</typeparam>
    public interface IValueAccessor<TValueType> : IValueAccessor
    {
        /// <summary>
        /// Getter attempts to extract/convert the contents of the ValueContainer to the accessor's TValueType and return it. 
        /// Setter set the ValueContainer to contain the given value.
        /// </summary>
        TValueType Value { get; set; }

        /// <summary>
        /// property is updated each time the Value property is read.  null indicates that the conversion was successfull while any other value indicates why it was not.
        /// </summary>
        System.Exception LastValueGetException { get; }

        /// <summary>
        /// Attempts to extract/convert the contents of the ValueContainer to the accessor's TValueType and return it.  Set rethrow to true to rethrow any exception encountered during the conversion.
        /// </summary>
        TValueType GetValue(bool rethrow);

        /// <summary>Updates the ValueContainer to contain the given value and then transfers the container to the corresponding interconnection table space entry.  This method supports call chaining.</summary>
        IValueAccessor<TValueType> Set(TValueType value);

        /// <summary>If the given value is not Object.Equals Value then Updates both the local copy and the corresponding interconnection table space entry's value from the given one.  This method supports call chaining.</summary>
        IValueAccessor<TValueType> SetIfDifferent(TValueType value);

        /// <summary>
        /// This method updates the locally stored value and seqNum from the interconnection table space's corresponding table entry.  
        /// This method supports call chaining.
        /// </summary>
        new IValueAccessor<TValueType> Update();
    }

    #endregion

    #region static Values namespace for Interconnection singleton Instance

    /// <summary>
    /// This is a static class that serves as the namespace within which to place the IValuesInterconnection singleton that is commonly used as the main values interconnection table space instance.
    /// </summary>
    public static class Values
    {
        #region Singleton instance

        /// <summary>Gives the caller get and set access to the singleton IValueInterconnection instance that is used to provide application wide value interconnection.</summary>
        public static IValuesIneterconnection Instance
        {
            get { return singletonInstanceHelper.Instance; }
            set { singletonInstanceHelper.Instance = value; }
        }

        private static SingletonHelperBase<IValuesIneterconnection> singletonInstanceHelper = new SingletonHelperBase<IValuesIneterconnection>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new ValuesInterconnection("MainInterconnect"));

        #endregion
    }

    #endregion

    #region ValuesInterconnection implementation

    /// <summary>
    /// This is the primary implementation class for the IValuesInterconnection interface.
    /// Instances of this class implement a basic Name to ID mapping and ID indexed table of values.  These value table entries are accessed using IValueAccessor objects constructed
    /// using the GetValueAccessor factory methods that are implemented here.  These methods create the required table entry if none already exists for the given name and then
    /// create and return an internal IValueAccessor implementation object.  Access (read or write) to this table, and the dictionary related meta-data are all guarded using a single
    /// mutex.  This gives first come, first served semantics to external entities that are calling Set or Update so as to guaranty that all single calls to Set or Update methods produce
    /// atomically consistent updates to the table and accessors.  The use of the array versions of Set and Update allow this atomicity to span across updates from and sets to arbitrary sets of
    /// named value table entries.
    /// </summary>
    public class ValuesInterconnection : IValuesIneterconnection
    {
        #region construction

        /// <summary>Constructor.  Requires an instance name.</summary>
        public ValuesInterconnection(string name)
        {
            Name = name;
        }

        #endregion

        #region IValuesInterconnection interface

        /// <summary>Returns the name of this values interconnection (table space) object.</summary>
        public string Name { get; private set; }

        /// <summary>
        /// IValueAdpater Factory method.  
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAdapter that can be used to observe and/or set the value of the named table entry.
        /// </summary>
        public IValueAccessor GetValueAccessor(string name)
        {
            if (String.IsNullOrEmpty(name))
                return new ValueAccessor(null, null);

            ValueTableEntry tableEntry = InnerGetValueTableEntry(name);

            IValueAccessor adapter = new ValueAccessor(this, tableEntry);
            return adapter.Update();
        }

        /// <summary>
        /// Typed (using generics) IValueAdpater{TValueType} Factory method.
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAdapter{TValueType} that can be used to observe and/or set the value of the named table entry.
        /// </summary>
        public IValueAccessor<TValueType> GetValueAccessor<TValueType>(string name)
        {
            if (String.IsNullOrEmpty(name))
                return new ValueAccessor<TValueType>(null, null);

            ValueTableEntry tableEntry = InnerGetValueTableEntry(name);

            IValueAccessor<TValueType> adapter = new ValueAccessor<TValueType>(this, tableEntry);
            return adapter.Update();
        }

        /// <summary>Returns an array of the names of all of the values in this interconnection table instance.</summary>
        public string[] ValueNamesArray 
        { 
            get 
            {
                return GetValueNamesRange(0, 0);
            } 
        }

        /// <summary>
        /// Returns a subset list of the overarll ValueNamesArray.  Allows the caller to get names that have been added since they last obtained a full array.
        /// If maxNumItems is passed as zero then the full set of names starting at the given startIdx will be returned.
        /// </summary>
        public string [] GetValueNamesRange(int startIdx, int maxNumItems)
        {
            lock (mutex)
            {
                int numItems = Math.Max(0, tableItemNamesList.Count - startIdx);
                if (numItems > maxNumItems && maxNumItems > 0)
                    numItems = maxNumItems;

                if (numItems > 0 && startIdx >= 0)
                    return tableItemNamesList.GetRange(startIdx, numItems).ToArray();
                else
                    return emptyStringArray;
            }
        }

        private static readonly string[] emptyStringArray = new string[0];

        /// <summary>
        /// Provides a property that returns the current total number of named values in this values interconnect table space.
        /// <para/>Please note:  this value is updated after items have actually been added to the array.  
        /// It is possible that this value will be smaller than the ValueNamesArray length for a brief period of time while another thread is adding a new value accessor.
        /// </summary>
        public int ValueNamesArrayLength { get { return volatileTableItemNamesListCount; } }

        /// <summary>
        /// This method is used internally by IValueAccessor instances to lock the table space and set the corresponding table entry from the calling/given IValueAccessor instance.
        /// </summary>
        internal void Set(IValueAccessor accessor)
        {
            if (accessor != null)
            {
                lock (mutex)
                {
                    ((accessor as ValueAccessor) ?? emptyValueAccessor).InnerGuardedSetTableEntryFromValue();
                    SynchrounousCustomPostSetTableEntryFromValueHandler(accessor);

                    globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
                }

                notificationList.Notify();
            }
        }

        /// <summary>
        /// This method is used to Set the table entry values for an array of IValueAdapter instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        public void Set(IValueAccessor[] accessorArray, bool optimize)
        {
            accessorArray = accessorArray ?? emptyValueAccessorArray;

            Set(accessorArray, accessorArray.Length, optimize);
        }

        /// <summary>
        /// This method is used to Set the table entry values for a portion of an array of IValueAdapter instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="numEntriesToSet">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        public void Set(IValueAccessor[] accessorArray, int numEntriesToSet, bool optimize)
        {
            accessorArray = accessorArray ?? emptyValueAccessorArray;
            numEntriesToSet = Math.Min(numEntriesToSet, accessorArray.Length);

            int numEntriesSet = 0;

            lock (mutex)
            {
                for (int idx = 0; idx < numEntriesToSet; idx++)
                {
                    IValueAccessor accessor = accessorArray[idx];

                    if (accessor != null && (!optimize || accessor.IsSetPending))
                    {
                        ((accessor as ValueAccessor) ?? emptyValueAccessor).InnerGuardedSetTableEntryFromValue();

                        SynchrounousCustomPostSetTableEntryFromValueHandler(accessor);

                        numEntriesSet++;
                    }
                }

                if (numEntriesSet != 0)
                    globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
            }

            if (numEntriesSet != 0)
                notificationList.Notify();
        }


        /// <summary>
        /// This method is used internally by IValueAccessor instances to lock the table space and update the accessor's copy of the value and sequence number from the corresponding table entry.
        /// </summary>
        internal void Update(IValueAccessor accessor)
        {
            if (accessor != null)
            {
                lock (mutex)
                {
                    ((accessor as ValueAccessor) ?? emptyValueAccessor).InnerGuardedUpdateValueFromTableEntry();
                }
            }
        }

        /// <summary>
        /// This method is used to Update a set/array of IValueAdapter instances from the corresponding set of interconnection table entry values.  
        /// This arrayed update operation is performed atomically across all of the table entries refereed to by the non-null adapters in the array.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Updated.</param>
        public void Update(IValueAccessor[] accessorArray)
        {
            accessorArray = accessorArray ?? emptyValueAccessorArray;

            Update(accessorArray, accessorArray.Length);
        }

        /// <summary>
        /// This method is used to Update a set/array of IValueAdapter instances from the corresponding set of interconnection table entry values.  
        /// This arrayed update operation is performed atomically across the table entries referred to by the non-null adapters in the array up to the given maximum item index to update.
        /// <para/>This method is specifically intended for use by Custom update scanner instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Updated.</param>
        /// <param name="numEntriesToUpdate">Limits how much of the array will be used.  Maximum index of items that are looked at will be &lt; this value.</param>
        public void Update(IValueAccessor[] accessorArray, int numEntriesToUpdate)
        {
            accessorArray = accessorArray ?? emptyValueAccessorArray;
            numEntriesToUpdate = Math.Min(numEntriesToUpdate, accessorArray.Length);

            lock (mutex)
            {
                for (int idx = 0; idx < numEntriesToUpdate; idx++)
                {
                    IValueAccessor accessor = accessorArray[idx];

                    ((accessor as ValueAccessor) ?? emptyValueAccessor).InnerGuardedUpdateValueFromTableEntry();
                }
            }
        }


        /// <summary>Provides an IBasicNotificationList instance that will be Notified after each Set operation has been completed.</summary>
        public IBasicNotificationList NotificationList { get { return notificationList; } }

        /// <summary>Proides a sequence number that counts the total number of table-wide Set operations that have been performed.</summary>
        public UInt32 GlobalSeqNum { get { return globalSeqNum; } }

        #endregion

        #region functionality extension delegates to be used by derived classes

        /// <summary>
        /// This property may be used by a derived class to specify a custom handler that is given each IValueAccessor that has been used to perform an InnerGaugedSetTableEntryFromValue method.
        /// <para/>Please note: this call is made synchronously while owning the table's mutex.  
        /// Care must be exersized to minimize the time consumed in this method and to avoid creating any possiblity of a deadlock by attempting to acquire mutiple lock objects within the
        /// call lifetime of this delegate.  This delegate may be combined with the use of the notificationList for combinations of synchronous and asynchronous signaling.
        /// </summary>
        protected Action<IValueAccessor> SynchrounousCustomPostSetTableEntryFromValueHandler
        {
            get { return synchronousCustomPostSetTableEntryFromValueHandler ?? emptyPostSetTableEntryFromValueHandler; }
            set { synchronousCustomPostSetTableEntryFromValueHandler = null; }
        }
        private Action<IValueAccessor> synchronousCustomPostSetTableEntryFromValueHandler = null;
        private static readonly Action<IValueAccessor> emptyPostSetTableEntryFromValueHandler = (iva) => { };

        #endregion

        #region Inner methods

        /// <summary>
        /// Common method used by the two IValueAccessor factory methods.  This method takes a given name and returns the ValueTableEntry instance for that name, 
        /// adding the name and a new ValueTableEntry instance if needed.
        /// </summary>
        private ValueTableEntry InnerGetValueTableEntry(string name)
        {
            name = name ?? String.Empty;
            ValueTableEntry tableEntry = null;

            lock (mutex)
            {
                if (!tableEntryDictionary.TryGetValue(name, out tableEntry) || tableEntry == null)
                {
                    tableEntry = new ValueTableEntry(name, table.Count + 1);
                    table.Add(tableEntry);
                    tableItemNamesList.Add(name);
                    tableEntryDictionary[name] = tableEntry;

                    volatileTableItemNamesListCount++;
                }

                globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
            }

            return tableEntry;
        }

        #endregion

        #region private Table and table space related implementation fields

        /// <summary>
        /// table space and dictionary mutex.  
        /// Note: this entity does not use reader/writer locks in expectation that methods used here are sufficiently fast so that there will not be
        /// a large amount of contention for the use of the table when performing activities here and based on the expectation that a simple mutex has a faster internal implementation
        /// than would be produced by using a reader writer lock, with its additional overhead in total number of interlocked operations even when there is little contention.  
        /// </summary>
        private object mutex = new object();

        /// <summary>Basic list of the names of all of the items in the table</summary>
        private List<string> tableItemNamesList = new List<string>();
        /// <summary>Volatile count of the number of items that have been added to the tableItemNamesList.  This is updated after the itesm have been added to the list.</summary>
        private volatile int volatileTableItemNamesListCount = 0;

        /// <summary>This is the dictionary that is used to convert value names into the corresponding ValueTableEntry that is used for that name.</summary>
        private Dictionary<string, ValueTableEntry> tableEntryDictionary = new Dictionary<string, ValueTableEntry>();

        /// <summary>This is the "table".  It consists of a list of the created ValueTableEntry items and can be indexed by their IDs as needed.</summary>
        private List<ValueTableEntry> table = new List<ValueTableEntry>();

        /// <summary>empty array of accessor objects to avoid needing to do null pointer checks in foreach iterators.</summary>
        private static readonly IValueAccessor[] emptyValueAccessorArray = new IValueAccessor[0];
        /// <summary>empty ValueAccessor to simplify safe invocation of down-casted method using ?? operator.</summary>
        private static readonly ValueAccessor emptyValueAccessor = new ValueAccessor(null, null);

        /// <summary>Backing store for NotificationList</summary>
        private BasicNotificationList notificationList = new BasicNotificationList();

        /// <summary>Backing store for GlobalSeqNum</summary>
        private volatile UInt32 globalSeqNum = 0;

        #endregion

        #region InnerGuardedIncrementSkipZero

        /// <summary>
        /// This method acts to increment a sequence number as it flows through this method without using interlock increment (since this method is only used to increment seq nums while owning the appropriate lock).
        /// </summary>
        private static UInt32 InnerGuardedIncrementSkipZero(UInt32 seqNumIn)
        {
            UInt32 nextSeqNum = unchecked(seqNumIn + 1);
            if (nextSeqNum != 0)
                return nextSeqNum;
            else
                return 1;
        }

        #endregion

        #region ValueTableEntry and ValueAdapterImpl implementation classes

        /// <summary>
        /// Table entry implementation class.  Contains the parts that are required to be stored per named value in the interconnect table space.
        /// </summary>
        private class ValueTableEntry
        {
            /// <summary>Constructor.  requires a name and an id</summary>
            public ValueTableEntry(string name, int id)
            {
                Name = name;
                ID = id;
            }

            /// <summary>Backing store for the table entries name.</summary>
            public string Name { get; private set; }
            /// <summary>Backing store for the table entries ID.</summary>
            public int ID { get; private set; }

            /// <summary>Contains the most recently assigned sequence number that goes with the current ValueAsObject value.</summary>
            public UInt32 SeqNum { get { return seqNum; } }
            /// <summary>Contains the most recently assigned value in a container</summary>
            public ValueContainer ValueContainer { get; private set; }

            /// <summary>used by one or more attached accessors to Set the value of the table entry.  Also increments the SeqNum, skipping zero which is reserved as the initial unwritten state.</summary>
            public void Set(ref ValueContainer valueContainerIn)
            {
                ValueContainer = valueContainerIn;

                seqNum = InnerGuardedIncrementSkipZero(seqNum);
            }

            /// <summary>Backing storeage for the SeqNum.  seqNum is defined to be volatile because it is observed without owning the table lock, even though it is only updated while owning the table lock.</summary>
            private volatile UInt32 seqNum = 0;
        }

        /// <summary>Implementation class for the IValueAccessor interface.  Provides pure ValueAsObject based use of the Value Interconnection instance that created this object</summary>
        private class ValueAccessor : IValueAccessor
        {
            /// <summary>Internal constructor.  Requires parent ValuesIterconnection instance and ValueTableEntry instance to which this accessor is attached.</summary>
            internal ValueAccessor(ValuesInterconnection interconnectInstance, ValueTableEntry tableEntry)
            {
                InterconnectInstance = interconnectInstance;
                TableEntry = tableEntry;
            }

            /// <summary>Retains the ValuesInterconnection instance to which this accessor belongs, or null if there is none.</summary>
            private ValuesInterconnection InterconnectInstance { get; set; }
            /// <summary>Retains the ValueTableEntry to which this accessor is attached, or null if there is none.</summary>
            private ValueTableEntry TableEntry { get; set; }

            /// <summary>Gives the Name of the Value that this accessor is used to access.</summary>
            public string Name { get { return ((TableEntry != null) ? TableEntry.Name : String.Empty); } }

            /// <summary>Gives the ID number assigned to this Named value's table entry in the interconnection table space.</summary>
            public int ID { get { return ((TableEntry != null) ? TableEntry.ID : 0); } }

            /// <summary>
            /// get/set property gives access to the value object that the accessor uses to contain the current value.
            /// Setter sets IsSetPending if the given value is not equal to the current one.
            /// <para/>This is a value object and as such this property can only be assigned an entire ValueContainer.
            /// </summary>
            public ValueContainer ValueContainer 
            { 
                get { return valueContainer; } 
                set 
                {
                    IsSetPending |= !valueContainer.IsEqualTo(value);
                    valueContainer = value; 
                } 
            }
            protected ValueContainer valueContainer;

            /// <summary>
            /// get/set property contains the last set or the last updated value for this accessor.  
            /// The setter just updates the value that can be used later with the Set method.
            /// </summary>
            public object ValueAsObject 
            { 
                get { return valueContainer.ValueAsObject; } 
                set 
                {
                    ValueContainer = new ValueContainer().SetFromObject(value);
                } 
            }

            /// <summary>This property is set whenever the ValueContainer is changed and is cleared whenver the update has been delievered to the ValueInterconnection instance or when Update is called.</summary>
            public bool IsSetPending { get; protected set; }

            /// <summary>Sets the corresponding interconnection table space entry's value from the current ValueAsObject value.  This method supports call chaining.</summary>
            public IValueAccessor Set()
            {
                if (InterconnectInstance != null)
                {
                    InterconnectInstance.Set(this);
                    IsSetPending = false;
                }

                return this;
            }

            /// <summary>Sets ValueContainer to the given value and then Sets the corresponding interconnection table space entry's value from the given one.  This method supports call chaining.</summary>
            public IValueAccessor Set(ValueContainer valueContainerIn)
            {
                valueContainer = valueContainerIn;

                return Set();
            }

            /// <summary>Sets ValueAsObject to the given value and then Sets the corresponding interconnection table space entry's value from the given one.  This method supports call chaining.</summary>
            public IValueAccessor Set(object valueAsObject)
            {
                ValueAsObject = valueAsObject;

                return Set();
            }

            /// <summary>Checks if the current ValueContainer is different than the given one.  If the are not Equal then calls Set(valueContainer) to set the local and interconnect values from the given one.  This method supports call chaining.</summary>
            public IValueAccessor SetIfDifferent(ValueContainer valueContainer)
            {
                ValueContainer = valueContainer;

                if (IsSetPending)
                    Set();

                return this;
            }

            /// <summary>Converts valueAsObject to a ValueContainer and then calls SetIfDifferent(valueContainer) to set the access and the corresponding interconnect table entry from it if needed.  This method supports call chaining.</summary>
            public IValueAccessor SetIfDifferent(object valueAsObject)
            {
                return SetIfDifferent(new ValueContainer().SetFromObject(valueAsObject));
            }

            /// <summary>This property returns true if the LastUpdateSeqNun is not the same as the CurrentSeqNum.</summary>
            public bool IsUpdateNeeded { get { return (ValueSeqNum != CurrentSeqNum); } }
            /// <summary>This method updates the locally stored value and seqNum from the interconnection table space's corresponding table entry.  This method supports call chaining.</summary>
            public IValueAccessor Update()
            {
                if (IsUpdateNeeded && InterconnectInstance != null)
                    InterconnectInstance.Update(this);

                return this;
            }
            /// <summary>Gives the current sequence number of the value that is currently in the interconnection table.</summary>
            public UInt32 CurrentSeqNum { get { return ((TableEntry != null) ? TableEntry.SeqNum : 0); } }
            /// <summary>Gives the sequence number of the value that was last Set to, or updated from, the interconnection table.  The accessor may be updated if this value is not equal to CurrentSeqNum.</summary>
            public UInt32 ValueSeqNum { get; set; }
            /// <summary>True if the corresponding table entry has been explicitly set to a value and this object has been Updated from it.  This is a synonym for ((ValueSeqNum != 0) || IsSetPending)</summary>
            public bool HasValueBeenSet { get { return ((ValueSeqNum != 0) || IsSetPending); } }

            /// <summary>
            /// This method is used to support typed (generics) accessors by informing them when the backing store for the ValueAsObject property may have changed so that any typed Value, or similar,
            /// property can be updated.
            /// </summary>
            public virtual bool HandlePostUpdateValueConversion() 
            {
                return true;
            }

            /// <summary>
            /// Internal method used to Update the accessor's ValueAsObject and ValueSeqNum from the corresponding table entry.
            /// <para/>This method may only be called from the ValueInterconnect internal logic while the current thread has exclusive access to the TableEntry in question.
            /// </summary>
            internal void InnerGuardedUpdateValueFromTableEntry()
            {
                if (IsUpdateNeeded)
                {
                    valueContainer = ((TableEntry != null) ? TableEntry.ValueContainer : emptyValueContainer);
                    ValueSeqNum = CurrentSeqNum;

                    IsSetPending = false;
                }
            }

            private static readonly ValueContainer emptyValueContainer = new ValueContainer();

            /// <summary>
            /// Internal method used to Set the TableEntry's value from the accessors current ValueAsObject value using the TableEntry.Set(value) method.  Also updates
            /// the ValueSeqNum from the TableEntries SeqNum (via CurrentSeqNum).
            /// <para/>This method may only be called from the ValueInterconnect internal logic while the current thread has exclusive access to the TableEntry in question.
            /// </summary>
            internal void InnerGuardedSetTableEntryFromValue()
            {
                if (TableEntry != null)
                    TableEntry.Set(ref valueContainer);

                ValueSeqNum = CurrentSeqNum;
            }

            public override string ToString()
            {
                if (!IsUpdateNeeded)
                    return "Accessor '{0}'@{1} {2} [vSeq:{3}]".CheckedFormat(Name, ID, ValueContainer, ValueSeqNum);
                else
                    return "Accessor '{0}'@{1} {2} [UpdateNeeded vSeq:{3} current:{4}]".CheckedFormat(Name, ID, ValueContainer, ValueSeqNum, CurrentSeqNum);
            }
        }

        /// <summary>Implementation class for the IValueAccessor{TValueType} interface to provide type converted access to an Interconnection table space value.  Extends the ValueAccessor class.</summary>
        private class ValueAccessor<TValueType> : ValueAccessor, IValueAccessor<TValueType>
        {
            /// <summary>Internal constructor.  Requires parent ValuesIterconnection instance and ValueTableEntry instance to which this accessor is attached.</summary>
            internal ValueAccessor(ValuesInterconnection interconnectInstance, ValueTableEntry tableEntry)
                : base(interconnectInstance, tableEntry)
            {
                typeOfTValueType = typeof(TValueType);
                ValueContainer.DecodeType(typeOfTValueType, out decodedValueType, out decodedTypeIsNullable);
            }

            Type typeOfTValueType;
            ContainerStorageType decodedValueType;
            bool decodedTypeIsNullable;

            /// <summary>
            /// Getter gives the last updated or set value using the desired TValueType.  
            /// During Update calls, LastValueGetException is set to reflect the success of the value conversion from
            /// ValueAsObject to the given TValueType type.
            /// Setter sets the ValueContainer to contain the given value and sets IsSetPending to true.
            /// </summary>
            public TValueType Value 
            { 
                get 
                {
                    LastValueGetException = null;
                    try
                    {
                        TValueType value = GetValue(true);
                        LastValueGetException = null;
                        return value;
                    }
                    catch (System.Exception ex)
                    {
                        LastValueGetException = ex;
                        return default(TValueType);
                    }
                }
                set 
                {
                    ValueContainer = new ValueContainer().SetValue<TValueType>(value, decodedValueType, decodedTypeIsNullable);
                } 
            }

            /// <summary>
            /// property is updated each time the Value property is read.  null indicates that the conversion was successfull while any other value indicates why it was not.
            /// </summary>
            public System.Exception LastValueGetException { get; protected set; }

            /// <summary>
            /// Attempts to extract/convert the contents of the ValueContainer to the accessor's TValueType and return it.  Set rethrow to true to rethrow any exception encountered during the conversion.
            /// </summary>
            public TValueType GetValue(bool rethrow)
            {
                return valueContainer.GetValue<TValueType>(decodedValueType, decodedTypeIsNullable, rethrow);
            }

            /// <summary>Updates both the local copy and the corresponding interconnection table space entry's value from the given one.  This method supports call chaining.</summary>
            public IValueAccessor<TValueType> Set(TValueType value)
            {
                Set(new ValueContainer().SetValue(value, decodedValueType, decodedTypeIsNullable));

                return this;
            }

            /// <summary>If the given value is not Object.Equals Value then Updates both the local copy and the corresponding interconnection table space entry's value from the given one.  This method supports call chaining.</summary>
            public IValueAccessor<TValueType> SetIfDifferent(TValueType value)
            {
                SetIfDifferent(new ValueContainer().SetValue(value, decodedValueType, decodedTypeIsNullable));
               
                return this;
            }

            /// <summary>
            /// This method updates the locally stored value and seqNum from the interconnection table space's corresponding table entry.  
            /// IsValueValid will be set to true if the ValueAsObject value obtained from the interconnection table space could be successfully casted to the given TValueType.
            /// This method supports call chaining.
            /// </summary>
            public new IValueAccessor<TValueType> Update()
            {
                base.Update();

                return this;
            }
        }

        #endregion
    }

    #endregion

    #region ValueSetItemAttribute and ValueSetAdatper

    namespace Attributes
    {
        /// <summary>
        /// This attribute is used to annotate public get/set properties and fields in a class in order that the class can be used as the ValueSet for
        /// a ValueSetAdapter adapter.  Each such property or field in the ValueSet class specifies a specific property and value source that 
		/// will receive the values from Update calls and which is used as the value source for Set calls on the Adapter.
        /// <para/>Name = null, NameAdjust = NameAdjust.Prefix0, SilenceIssues = false
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
        public class ValueSetItemAttribute : AnnotatedItemAttributeBase
        {
            /// <summary>
            /// Default constructor.
            /// <para/>Name = null, NameAdjust = NameAdjust.Prefix0, SilenceIssues = false
            /// </summary>
            public ValueSetItemAttribute() 
                : base()
            {
            }

            /// <summary>
            /// When an item is marked to SilenceIssues, no issue messages will be emitted if the value cannot be accessed.  Value messages will still be emitted.
            /// </summary>
            public bool SilenceIssues { get; set; }
        }
    }

    /// <summary>
    ///  This adapter class provides a client with a ValueSet style tool that supports getting and setting sets of values in a IValueInterconnect instance.
    /// </summary>
    /// <typeparam name="TValueSet">
    /// Specifies the class type on which this adapter will operate.  
    /// Adapter harvests the list of <see cref="MosaicLib.Modular.Interconnect.Values.Attributes.ValueSetItemAttribute"/> annotated public member items from this type.
    /// </typeparam>
    /// <remarks>
    /// The primary methods/properties used on this adapter are: Construction, ValueSet, Setup, Set, Update, IsUpdateNeeded
    /// </remarks>
    public class ValueSetAdapter<TValueSet> : DisposableBase where TValueSet : class
    {
        #region Ctor

        /// <summary>
        /// Default constructor.  Assigns adapter to use default Config.Instance IConfig service instance.  
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// Setup method is used to generate final derived item names and to bind and make the initial update to the ValueSet contents.
        /// Setup method may also specify/override the config instance that is to be used.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public ValueSetAdapter()
            : this(null)
        {
        }

        /// <summary>
        /// Config instance constructor.  Assigns adapter to use given configInstance IConfig service instance.  This may be overridden during the Setup call.
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public ValueSetAdapter(IValuesIneterconnection valueInterconnect)
        {
            ValueInterconnect = valueInterconnect;

            valueSetItemInfoList = AnnotatedClassItemAccessHelper<Attributes.ValueSetItemAttribute>.ExtractItemInfoAccessListFrom(typeof(TValueSet), ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems);
            NumItems = valueSetItemInfoList.Count;

            itemAccessSetupInfoArray = new ItemAccessSetupInfo[NumItems];
            valueAccessorArray = new IValueAccessor[NumItems];

            OptimizeSets = true;
        }

        #endregion

        #region public methods and properies

        /// <summary>
        /// Contains the ValueSet object that is used as the value source for Set calls and receives updated values during Update.
        /// </summary>
        public TValueSet ValueSet { get; set; }

        /// <summary>Defines the emitter used to emit Setup, Set, and Update related errors.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter IssueEmitter { get { return FixupEmitterRef(ref issueEmitter); } set { issueEmitter = value; } }
        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter ValueNoteEmitter { get { return FixupEmitterRef(ref valueNoteEmitter); } set { valueNoteEmitter = value; } }

        private IValuesIneterconnection ValueInterconnect{ get; set; }        // delay making default ConfigInstance assignment until Setup method.

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Will use previously defined IValueInterconnect instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public ValueSetAdapter<TValueSet> Setup(params string[] baseNames)
        {
            return Setup(null, baseNames);
        }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>If a non-null valueInterconnect instnace is given then it will be used otherwise this method will use any previously defined IValueInterconnect instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="valueInterconnect">Allows the caller to (re)specifiy the IValueInterconnect instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public ValueSetAdapter<TValueSet> Setup(IValuesIneterconnection valueInterconnect, params string[] baseNames)
        {
            if (valueInterconnect != null || ValueInterconnect == null)
                ValueInterconnect = valueInterconnect ?? Values.Instance;

            if (ValueSet == null)
                throw new System.NullReferenceException("ValueSet property must be non-null before Setup can be called");

            // setup all of the static information

            for (int idx = 0; idx < NumItems; idx++)
            {
                ItemInfo<Attributes.ValueSetItemAttribute> itemInfo = valueSetItemInfoList[idx];
                Attributes.ValueSetItemAttribute itemAttribute = itemInfo.ItemAttribute;

                string memberName = itemInfo.MemberInfo.Name;
                string itemName = (!string.IsNullOrEmpty(itemAttribute.Name) ? itemAttribute.Name : itemInfo.MemberInfo.Name);
                string fullValueName = itemInfo.GenerateFullName(baseNames);

                if (!itemInfo.CanGetValue || !itemInfo.CanSetValue)
                {
                    if (!itemAttribute.SilenceIssues)
                        IssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: Member must provide public getter and setter, in ValueSet type '{2}'", memberName, fullValueName, TValueSetTypeStr);
                    continue;
                }

                IValueAccessor valueAccessor = ValueInterconnect.GetValueAccessor(fullValueName);

                ItemAccessSetupInfo itemAccessSetupInfo = new ItemAccessSetupInfo()
                {
                    ValueAccessor = valueAccessor,
                    ItemInfo = itemInfo,
                };

                GenerateMemberToFromValueAccessFuncs(itemAccessSetupInfo);

                Logging.IMesgEmitter slectedIssueEmitter = IssueEmitter;

                if (itemAccessSetupInfo.MemberFromValueAccessAction == null || itemAccessSetupInfo.MemberToValueAccessAction == null)
                {
                    if (!itemAttribute.SilenceIssues)
                        slectedIssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: no valid accessor delegate could be generated for its ValueSet type:'{3}'", memberName, fullValueName, itemInfo.ItemType, TValueSetTypeStr);

                    continue;
                }

                itemAccessSetupInfoArray[idx] = itemAccessSetupInfo;
                valueAccessorArray[idx] = valueAccessor;
            }

            return this;
        }

        /// <summary>
        /// Transfer the values from the ValueSet's annotated members to the corresponding set of IValueAccessors and then tell the IValueInterconnect instance
        /// to Set all of the IValueAccessors.
        /// <para/>Supports call chaining.
        /// </summary>
        public ValueSetAdapter<TValueSet> Set()
        {
            if (ValueSet == null)
            {
                IssueEmitter.Emit("ValueSet property must be non-null before Set can be called");
                return this;
            }

            foreach (ItemAccessSetupInfo iasi in itemAccessSetupInfoArray)
            {
                if (iasi != null && iasi.MemberFromValueAccessAction != null)
                {
                    iasi.MemberToValueAccessAction(ValueSet, IssueEmitter, ValueNoteEmitter);
                }
            }

            ValueInterconnect.Set(valueAccessorArray, NumItems, optimizeSets);

            return this;
        }

        /// <summary>
        /// This property determines if the Set method uses ValueContainer equality testing to determine which IValueAccessor objects to actually write to the table.
        /// When this property is true (the default), equality testing will be used to prevent updating table entires for IValueAccessors that do not have a set pending (due to change in container value).
        /// When this property is false, all value table entries will be Set, without regard to whether their value might have changed.
        /// </summary>
        public bool OptimizeSets { get { return optimizeSets; } set { optimizeSets = value; } }
        private bool optimizeSets = false;

        /// <summary>
        /// Returns true if any of the IValueAccessors to which this adapter is attached indicate that there is a pending update 
        /// (because the accessed value has been set elsewhere so there may be a new value to update that accessor from).
        /// </summary>
        public bool IsUpdateNeeded
        {
            get
            {
                foreach (IValueAccessor iva in valueAccessorArray)
                {
                    if (iva.IsUpdateNeeded)
                        return true;
                }

                return false;
            }
        }


        /// <summary>
        /// Requests the IValueInterconnect instance to update all of the adapter's IValueAccessor objects and then transfers the updated values
        /// from those accessor objects to the corresponding annotated ValueSet members.
        /// <para/>Supports call chaining.
        /// </summary>
        public ValueSetAdapter<TValueSet> Update()
        {
            if (ValueSet == null)
                throw new System.NullReferenceException("ValueSet property must be non-null before Update can be called");

            ValueInterconnect.Update(valueAccessorArray, NumItems);

            foreach (ItemAccessSetupInfo iasi in itemAccessSetupInfoArray)
            {
                if (iasi != null && iasi.MemberFromValueAccessAction != null)
                {
                    iasi.MemberFromValueAccessAction(ValueSet, IssueEmitter, ValueNoteEmitter);
                }
            }

            return this;
        }

        private bool optimizeUpdates = false;

        #endregion

        #region private methods

        void GenerateMemberToFromValueAccessFuncs(ItemAccessSetupInfo itemAccessSetupInfo)
        {
            ItemInfo<Attributes.ValueSetItemAttribute> itemInfo = itemAccessSetupInfo.ItemInfo;

            Action<TValueSet, IValueAccessor> innerBoundGetter = null;
            Action<TValueSet, IValueAccessor> innerBoundSetter = null;

            // we only support the legal data types for config key's values here
            if (itemInfo.ItemType == typeof(bool))
            {
                Action<TValueSet, bool> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, bool>(itemInfo);
                Func<TValueSet, bool> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, bool>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<bool>(pfGetter(valueSetObj), ContainerStorageType.Boolean, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<bool>(ContainerStorageType.Boolean, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(sbyte))
            {
                Action<TValueSet, sbyte> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, sbyte>(itemInfo);
                Func<TValueSet, sbyte> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, sbyte>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<sbyte>(pfGetter(valueSetObj), ContainerStorageType.SByte, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<sbyte>(ContainerStorageType.SByte, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(short))
            {
                Action<TValueSet, short> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, short>(itemInfo);
                Func<TValueSet, short> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, short>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<short>(pfGetter(valueSetObj), ContainerStorageType.Int16, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<short>(ContainerStorageType.Int16, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(int))
            {
                Action<TValueSet, int> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, int>(itemInfo);
                Func<TValueSet, int> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, int>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<int>(pfGetter(valueSetObj), ContainerStorageType.Int32, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<int>(ContainerStorageType.Int32, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(long))
            {
                Action<TValueSet, long> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, long>(itemInfo);
                Func<TValueSet, long> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, long>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<long>(pfGetter(valueSetObj), ContainerStorageType.Int64, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<long>(ContainerStorageType.Int64, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(byte))
            {
                Action<TValueSet, byte> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, byte>(itemInfo);
                Func<TValueSet, byte> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, byte>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<byte>(pfGetter(valueSetObj), ContainerStorageType.Byte, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<byte>(ContainerStorageType.Byte, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(ushort))
            {
                Action<TValueSet, ushort> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, ushort>(itemInfo);
                Func<TValueSet, ushort> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, ushort>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<ushort>(pfGetter(valueSetObj), ContainerStorageType.UInt16, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<ushort>(ContainerStorageType.UInt16, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(uint))
            {
                Action<TValueSet, uint> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, uint>(itemInfo);
                Func<TValueSet, uint> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, uint>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<uint>(pfGetter(valueSetObj), ContainerStorageType.UInt32, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<uint>(ContainerStorageType.UInt32, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(ulong))
            {
                Action<TValueSet, ulong> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, ulong>(itemInfo);
                Func<TValueSet, ulong> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, ulong>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<ulong>(pfGetter(valueSetObj), ContainerStorageType.UInt64, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<ulong>(ContainerStorageType.UInt64, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(float))
            {
                Action<TValueSet, float> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, float>(itemInfo);
                Func<TValueSet, float> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, float>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<float>(pfGetter(valueSetObj), ContainerStorageType.Single, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<float>(ContainerStorageType.Single, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(double))
            {
                Action<TValueSet, double> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, double>(itemInfo);
                Func<TValueSet, double> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, double>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<double>(pfGetter(valueSetObj), ContainerStorageType.Double, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<double>(ContainerStorageType.Double, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(bool?))
            {
                Action<TValueSet, bool?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, bool?>(itemInfo);
                Func<TValueSet, bool?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, bool?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<bool?>(pfGetter(valueSetObj), ContainerStorageType.Boolean, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<bool?>(ContainerStorageType.Boolean, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(sbyte?))
            {
                Action<TValueSet, sbyte?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, sbyte?>(itemInfo);
                Func<TValueSet, sbyte?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, sbyte?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<sbyte?>(pfGetter(valueSetObj), ContainerStorageType.SByte, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<sbyte?>(ContainerStorageType.SByte, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(short?))
            {
                Action<TValueSet, short?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, short?>(itemInfo);
                Func<TValueSet, short?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, short?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<short?>(pfGetter(valueSetObj), ContainerStorageType.Int16, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<short?>(ContainerStorageType.Int16, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(int?))
            {
                Action<TValueSet, int?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, int?>(itemInfo);
                Func<TValueSet, int?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, int?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<int?>(pfGetter(valueSetObj), ContainerStorageType.Int32, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<int?>(ContainerStorageType.Int32, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(long?))
            {
                Action<TValueSet, long?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, long?>(itemInfo);
                Func<TValueSet, long?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, long?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<long?>(pfGetter(valueSetObj), ContainerStorageType.Int64, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<long?>(ContainerStorageType.Int64, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(byte?))
            {
                Action<TValueSet, byte?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, byte?>(itemInfo);
                Func<TValueSet, byte?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, byte?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<byte?>(pfGetter(valueSetObj), ContainerStorageType.Byte, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<byte?>(ContainerStorageType.Byte, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(ushort?))
            {
                Action<TValueSet, ushort?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, ushort?>(itemInfo);
                Func<TValueSet, ushort?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, ushort?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<ushort?>(pfGetter(valueSetObj), ContainerStorageType.UInt16, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<ushort?>(ContainerStorageType.UInt16, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(uint?))
            {
                Action<TValueSet, uint?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, uint?>(itemInfo);
                Func<TValueSet, uint?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, uint?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<uint?>(pfGetter(valueSetObj), ContainerStorageType.UInt32, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<uint?>(ContainerStorageType.UInt32, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(ulong?))
            {
                Action<TValueSet, ulong?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, ulong?>(itemInfo);
                Func<TValueSet, ulong?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, ulong?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<ulong?>(pfGetter(valueSetObj), ContainerStorageType.UInt64, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<ulong?>(ContainerStorageType.UInt64, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(float?))
            {
                Action<TValueSet, float?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, float?>(itemInfo);
                Func<TValueSet, float?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, float?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<float?>(pfGetter(valueSetObj), ContainerStorageType.Single, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<float?>(ContainerStorageType.Single, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(double?))
            {
                Action<TValueSet, double?> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, double?>(itemInfo);
                Func<TValueSet, double?> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, double?>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<double?>(pfGetter(valueSetObj), ContainerStorageType.Double, true); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<double?>(ContainerStorageType.Double, true, true)); };
            }
            else if (itemInfo.ItemType == typeof(string))
            {
                Action<TValueSet, string> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, string>(itemInfo);
                Func<TValueSet, string> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, string>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<string>(pfGetter(valueSetObj), ContainerStorageType.Object, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<string>(ContainerStorageType.Object, false, true)); };
            }
            else if (itemInfo.ItemType == typeof(object))
            {
                Action<TValueSet, object> pfSetter = AnnotatedClassItemAccessHelper.GenerateSetter<TValueSet, object>(itemInfo);
                Func<TValueSet, object> pfGetter = AnnotatedClassItemAccessHelper.GenerateGetter<TValueSet, object>(itemInfo);
                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { iva.ValueContainer = new ValueContainer().SetValue<object>(pfGetter(valueSetObj), ContainerStorageType.Object, false); };
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva) { pfSetter(valueSetObj, iva.ValueContainer.GetValue<object>(ContainerStorageType.Object, false, true)); };
            }
            else if (itemInfo.ItemType.IsEnum)
            {
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva)
                {
                    // this is less efficient but will work
                    object valueAsObject = iva.ValueAsObject;
                    object parsedValueAsObject = null;

                    string valueAsStr = valueAsObject as string;
                    if (valueAsStr != null)
                        parsedValueAsObject = System.Enum.Parse(itemInfo.ItemType, valueAsStr);
                    else
                        parsedValueAsObject = valueAsObject;

                    if (itemInfo.IsProperty)
                        itemInfo.PropertyInfo.SetValue(valueSetObj, parsedValueAsObject, null);
                    else
                        itemInfo.FieldInfo.SetValue(valueSetObj, parsedValueAsObject);
                };

                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva)
                {
                    if (itemInfo.IsProperty)
                        iva.ValueAsObject = itemInfo.PropertyInfo.GetValue(valueSetObj, emptyObjectArray);
                    else
                        iva.ValueAsObject = itemInfo.FieldInfo.GetValue(valueSetObj);
                };
            }
            else
            {
                innerBoundSetter = delegate(TValueSet valueSetObj, IValueAccessor iva)
                {
                    // this is less efficient but will work
                    object valueAsObject = iva.ValueAsObject;

                    if (itemInfo.IsProperty)
                        itemInfo.PropertyInfo.SetValue(valueSetObj, valueAsObject, null);
                    else
                        itemInfo.FieldInfo.SetValue(valueSetObj, valueAsObject);
                };

                innerBoundGetter = delegate(TValueSet valueSetObj, IValueAccessor iva)
                {
                    if (itemInfo.IsProperty)
                        iva.ValueAsObject = itemInfo.PropertyInfo.GetValue(valueSetObj, emptyObjectArray);
                    else
                        iva.ValueAsObject = itemInfo.FieldInfo.GetValue(valueSetObj);
                };
            }
 
            if (innerBoundSetter != null)
            {
                itemAccessSetupInfo.MemberFromValueAccessAction = delegate(TValueSet valueSetObj, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter)
                {
                    IValueAccessor valueAccessor = itemAccessSetupInfo.ValueAccessor;

                    try
                    {
                        if (!optimizeUpdates || itemAccessSetupInfo.LastTransferedValueSeqNum != valueAccessor.ValueSeqNum)
                        {
                            innerBoundSetter(valueSetObj, valueAccessor);
                            itemAccessSetupInfo.LastTransferedValueSeqNum = valueAccessor.ValueSeqNum;
                        }

                        if (valueUpdateEmitter.IsEnabled)
                            valueUpdateEmitter.Emit("Member:'{0}' transfered from Name:'{1}' value:'{2}' [type:'{3}']", itemAccessSetupInfo.MemberName, itemAccessSetupInfo.ValueName, valueAccessor.ValueAsObject, TValueSetTypeStr);
                    }
                    catch (System.Exception ex)
                    {
                        if (!itemInfo.ItemAttribute.SilenceIssues)
                            updateIssueEmitter.Emit("Member:'{0}' transfer from Name:'{1}' in type '{2}' could not be performed: {3}", itemAccessSetupInfo.MemberName, itemAccessSetupInfo.ValueName, TValueSetTypeStr, ex);
                    }
                };
            }

            if (innerBoundGetter != null)
            {
                itemAccessSetupInfo.MemberToValueAccessAction = delegate(TValueSet valueSetObj, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter)
                {
                    IValueAccessor valueAccessor = itemAccessSetupInfo.ValueAccessor;

                    try
                    {
                        innerBoundGetter(valueSetObj, valueAccessor);
                        itemAccessSetupInfo.LastTransferedValueSeqNum = 0;      // trigger that next update needs to retransfer the value.

                        if (valueUpdateEmitter.IsEnabled)
                            valueUpdateEmitter.Emit("Member:'{0}' transfered to Name:'{1}' value:'{2}' [type:'{3}']", itemAccessSetupInfo.MemberName, itemAccessSetupInfo.ValueName, valueAccessor.ValueAsObject, TValueSetTypeStr);
                    }
                    catch (System.Exception ex)
                    {
                        if (!itemInfo.ItemAttribute.SilenceIssues)
                            updateIssueEmitter.Emit("Member'{0}' tranfer to Name:'{1}' in type '{2}' could not be performed: {3}", itemAccessSetupInfo.MemberName, itemAccessSetupInfo.ValueName, TValueSetTypeStr, ex);
                    }
                };
            }
        }

        private static readonly object[] emptyObjectArray = new object[0];

        #endregion

        #region private fields, properties

        Type TValueSetType = typeof(TValueSet);
        string TValueSetTypeStr = typeof(TValueSet).Name;

        List<ItemInfo<Attributes.ValueSetItemAttribute>> valueSetItemInfoList = null;       // gets built by the AnnotatedClassItemAccessHelper.
        int NumItems { get; set; }

        /// <summary>
        /// Internal class used to capture the key specific setup information for a given annotated property in the ValueSet.
        /// </summary>
        private class ItemAccessSetupInfo
        {
            /// <summary>The value's corresponding IValueAccessor object.</summary>
            public IValueAccessor ValueAccessor { get; set; }

            /// <summary>
            /// Retains access to the ItemInfo for the corresponding item in the value set
            /// </summary>
            public ItemInfo<Attributes.ValueSetItemAttribute> ItemInfo { get; set; }

            /// <summary>
            /// Returns the ItemAttribute from the contained ItemInfo
            /// </summary>
            public Attributes.ValueSetItemAttribute ItemAttribute { get { return ItemInfo.ItemAttribute; } }

            /// <summary>
            /// Returns the symbol name of the Property or Field to which this item is attached.
            /// </summary>
            public string MemberName { get { return ItemInfo.MemberInfo.Name; } }

            /// <summary>
            /// Returns the full Name of the value that the value accessor is attached to.
            /// </summary>
            public string ValueName { get { return ((ValueAccessor != null) ? ValueAccessor.Name : String.Empty); } }

            /// <summary>delegate that is used to set a specific member's value from a given config key's value object's stored value.</summary>
            /// <remarks>this item will be null for static items and for items that failed to be setup correctly.</remarks>
            public Action<TValueSet, Logging.IMesgEmitter, Logging.IMesgEmitter> MemberToValueAccessAction { get; set; }

            /// <summary>delegate that is used to set a specific member's value from a given config key's value object's stored value.</summary>
            /// <remarks>this item will be null for static items and for items that failed to be setup correctly.</remarks>
            public Action<TValueSet, Logging.IMesgEmitter, Logging.IMesgEmitter> MemberFromValueAccessAction { get; set; }

            /// <summary>
            /// Carries the ValueSeqNum at the end of the last inbound or outbound transfer.  Allows Update to skip transfering values repeatedly.
            /// </summary>
            public UInt32 LastTransferedValueSeqNum { get; set; }
        }

        /// <remarks>Non-null elements in this array correspond to fully vetted gettable and settable ValueSet items.</remarks>
        ItemAccessSetupInfo[] itemAccessSetupInfoArray = null;

        IValueAccessor [] valueAccessorArray = null;

        #endregion

        #region message emitter glue

        private Logging.IMesgEmitter issueEmitter = null, valueNoteEmitter = null;
        private Logging.IMesgEmitter FixupEmitterRef(ref Logging.IMesgEmitter emitterRef)
        {
            if (emitterRef == null)
                emitterRef = Logging.NullEmitter;
            return emitterRef;
        }

        #endregion
    }

    #endregion
}

//-------------------------------------------------------------------
