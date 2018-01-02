//-------------------------------------------------------------------
/*! @file Interconnect/Values.cs
 *  @brief Defines a set of classes that are used to supported interconnecting value adapters.
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
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

using MosaicLib.Modular.Common;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.Utils;

// Modular.Interconnect is the general namespace for tools that help interconnect Modular Parts without requiring that that have pre-existing knowledge of each-other's classes.
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
    #region IValuesInterconnection

    /// <summary>
    /// This interface defines the basic externally visible methods and properties that are exposed by Values Interconnection implementation objects.
    /// Clients are generally expected to make most use of the GetValueAdapter factory methods methods.  
    /// ValueSetAdapter classes are generally expected to use the IValueAccessor array versions of the Set and Update methods.
    /// </summary>
    public interface IValuesInterconnection
    {
        /// <summary>Returns the name of this values interconnection (table space) object.</summary>
        string Name { get; }

        /// <summary>
        /// Gets/Sets the entire set of name mappings as an enumeration.  
        /// This set of mappings is used by GetValueAccessor to support mapping from the given name to an alternate name on a case by case basis.  
        /// This mapping table may be used to allow two (or more) entities to end up using the same table entry even if they do not know about each other in advance.
        /// </summary>
        IEnumerable<Modular.Common.IMapNameFromTo> MapNameFromToSet { get; set; }

        /// <summary>
        /// Adds the given set of MapNameFromTo items to the current mapping MapNameFromToSet.
        /// </summary>
        IValuesInterconnection AddRange(IEnumerable<Modular.Common.IMapNameFromTo> addMapNameFromToSet);

        /// <summary>
        /// IValueAccessor Factory method.  
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAccessor that can be used to observe and/or set the value of the named table entry.
        /// <para/>If the given name is null or empty then this method returns a stub IValueAccessor that is not attached to anything.
        /// </summary>
        IValueAccessor GetValueAccessor(string name, INamedValueSet metaDataIn = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate);

        /// <summary>
        /// Typed (using generics) IValueAccessor{TValueType} Factory method.
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAccessor{TValueType} that can be used to observe and/or set the value of the named table entry.
        /// <para/>If the given name is null or empty then this method returns a stub IValueAccessor{TValueType} that is not attached to anything.
        /// </summary>
        IValueAccessor<TValueType> GetValueAccessor<TValueType>(string name, INamedValueSet metaDataIn = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate);

        /// <summary>Returns an array of the names of all of the values in this interconnection table instance.</summary>
        string[] ValueNamesArray { get; }

        /// <summary>
        /// Returns a subset list of the overall ValueNamesArray.  Allows the caller to get names that have been added since they last obtained a full array.
        /// If <paramref name="maxNumItems"/> is passed as zero then the full set of names starting at the given <paramref name="startAtIndex"/> will be returned.
        /// </summary>
        string[] GetValueNamesRange(int startAtIndex = 0, int maxNumItems = 0);

        /// <summary>
        /// Returns a filtered subset of the names of the overall table of named items.
        /// <param name="tableItemFilter">gives the caller provided predicate used to determine which table entries (by name and metadata) shall be included in the resulting set</param>
        /// <param name="startAtIndex">Indicates the number of table entries to skip before applying the given filter</param>
        /// <param name="maxNumItems">Gives the maximum number of items to pass to the filter during this call, or 0 to indicate that there is no limit</param>
        /// </summary>
        string[] GetFilteredNames(Func<string, INamedValueSet, bool> tableItemFilter, int startAtIndex = 0, int maxNumItems = 0);

        /// <summary>
        /// Returns a set of the IValueAccessors from table of named items filtered by using the given <paramref name="tableItemFilter"/>.
        /// <param name="tableItemFilter">gives the caller provided predicate used to determine which table entries (by name and metadata) shall be included in the resulting set</param>
        /// <param name="startAtIndex">Indicates the number of table entries to skip before applying the given filter</param>
        /// <param name="maxNumItems">Gives the maximum number of items to pass to the filter during this call, or 0 to indicate that there is no limit</param>
        /// </summary>
        IValueAccessor[] GetFilteredValueAccessors(Func<string, INamedValueSet, bool> tableItemFilter, int startAtIndex = 0, int maxNumItems = 0);

        /// <summary>
        /// Provides a property that returns the current total number of named values in this values interconnect table space.
        /// This property is implemented so that it can be efficiently, and frequently checked, to detect the addition of new value names to the table.
        /// <para/>Please note:  this value is updated after items have actually been added to the table.  
        /// As such, it is possible that this value will be smaller than the ValueNamesArray length for a brief period of time while another thread is adding a new value accessor.
        /// </summary>
        int ValueNamesArrayLength { get; }

        /// <summary>
        /// This method is used to Set the table entry values for an array of IValueAccessor instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        /// <remarks>This signature is being retained for backward comptability</remarks>
        void Set(IValueAccessor[] accessorArray, bool optimize);

        /// <summary>
        /// This method is used to Set the table entry values for a portion of an array of IValueAccessor instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="numEntriesToSet">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.  When negative value is passed, this parameter's value will be replaced with accessorArray.Length</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        void Set(IValueAccessor[] accessorArray, int numEntriesToSet, bool optimize);

        /// <summary>
        /// This method is used to Set the table entry values for a portion of an array of IValueAccessor instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="numEntriesToSet">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.  When negative value is passed, this parameter's value will be replaced with accessorArray.Length</param>
        /// <param name="setMode">Defines the mode of set that will be used</param>
        void Set(IValueAccessor[] accessorArray, SetMode setMode = SetMode.Optimize, int numEntriesToSet = -1);

        /// <summary>
        /// This method is used to Update a set/array of IValueAccessor instances from the corresponding set of interconnection table entry values.  
        /// This arrayed update operation is performed atomically across the table entries referred to by the non-null adapters in the array up to the given maximum item index to update.
        /// <para/>This method is specifically intended for use by Custom update scanner instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Updated.</param>
        /// <param name="numEntriesToUpdate">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.  When negative value is passed, this parameter's value will be replaced with accessorArray.Length</param>
        void Update(IValueAccessor[] accessorArray, int numEntriesToUpdate = -1);

        /// <summary>
        /// This method is used to Set (or Update/Merge) the given set of table items meta data from the given <paramref name="metaDataIn"/>.  
        /// <paramref name="mergeBehavior"/> may be used to control how the given <paramref name="metaDataIn"/> is combined with the existing meta data for each such selected item.
        /// </summary>
        /// <param name="accessorArray">Gives the set of table entry item names that are to be updated</param>
        /// <param name="metaDataIn">Gives the meta date that is to be used to update the selected items (as determined by the <paramref name="mergeBehavior"/> parameter)</param>
        /// <param name="mergeBehavior">Defines how the selected items existing meta data will be combined with the given value.</param>
        void SetMetaData(IValueAccessor[] accessorArray, INamedValueSet metaDataIn, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate);

        /// <summary>Provides an IBasicNotificationList instance that will be Notified after each Set operation has been completed.</summary>
        IBasicNotificationList NotificationList { get; }

        /// <summary>Proides a sequence number that counts the total number of table-wide Set operations that have been performed.</summary>
        UInt32 GlobalSeqNum { get; }
    }


    /// <summary>
    /// Defines the supported modes for setting a table entry.  
    /// <para/>Default (0x00), Optimize (0x01), IfValueSeqNumIsZero (0x02)
    /// </summary>
    [Flags]
    public enum SetMode
    {
        /// <summary>Always Set the corresponding table entry even if the given value accessor's value has not been set since it was last updated from the table. (0x00)</summary>
        Default = 0x00,

        /// <summary>Only Set the corresponding table entry if the given value accessor's value has been set since it was last updated from the table. (0x01)</summary>
        Optimize = 0x01,

        /// <summary>Only set any given table entry if the table entry is in its initial state (aka its value sequence number is zero).  (0x02)</summary>
        IfValueSeqNumIsZero = 0x02,
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

        /// <summary>Gives access to the meta data stored for this named item in the corresponding IValuesInterconnection table.</summary>
        INamedValueSet MetaData { get; }

        /// <summary>
        /// get/set property gives access to the value object that the accessor uses to contain the current value.
        /// Setter sets IsSetPending if the given value is not equal to the current one.
        /// <para/>This is a value object and as such this property can only be assigned an entire ValueContainer.
        /// </summary>
        ValueContainer VC { get; set; }

        /// <summary>
        /// get/set property is an alternative name for the VC property
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
        bool IsSetPending { get; set; }

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

        /// <summary>Resets this value accessor and the corresponding IVI table entry to be empty with sequence number zero.</summary>
        IValueAccessor Reset();

        /// <summary>This property returns true if the ValueSeqNum is not the same as the CurrentSeqNum or MetaDataSeqNum is not the same as CurrentMetaDataSeqNum.</summary>
        bool IsUpdateNeeded { get; }

        /// <summary>This method updates the locally stored value, metadata, and seqNums from the interconnection table space's corresponding table entry.  If the value IsSetPending then its value will be replaced by the table entry's value.  This method supports call chaining.</summary>
        IValueAccessor Update();

        /// <summary>Gives the current sequence number of the value that is currently in the interconnection table.  The value zero is only used when the table entry has never been changed from its initial empty state.</summary>
        UInt32 CurrentSeqNum { get; }

        /// <summary>Gives the sequence number of the value that was last Set to, or updated from, the interconnection table.  The accessor may be updated if this value is not equal to CurrentSeqNum.</summary>
        UInt32 ValueSeqNum { get; }

        /// <summary>Gives the current sequence number of the value/md that is currently in the interconnection table.  The value zero is only used when the table entry has never been changed from its initial empty state.</summary>
        UInt32 CurrentMetaDataSeqNum { get; }

        /// <summary>Gives the sequence number of the value/md that was last Set to, or updated from, the interconnection table.  The accessor may be updated if this value is not equal to CurrentMetaDataSeqNum.</summary>
        UInt32 MetaDataSeqNum { get; }

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
        /// This method updates the locally stored value, metadata, and seqNums from the interconnection table space's corresponding table entry.  
        /// If the value IsSetPending then its value will be replaced by the table entry's value.
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
        public static IValuesInterconnection Instance
        {
            get { return singletonInstanceHelper.Instance; }
            set { singletonInstanceHelper.Instance = value; }
        }

        private static SingletonHelperBase<IValuesInterconnection> singletonInstanceHelper = new SingletonHelperBase<IValuesInterconnection>(SingletonInstanceBehavior.AutoConstructIfNeeded, () => new ValuesInterconnection("ValuesInterconnectSingleton"));

        #endregion

        #region Global Values Interconnection table dictionary

        /// <summary>
        /// Attempts to add the given interconnectionTable to the static table dictionary maintained here.
        /// if rethrow is true and any of the listed exception conditions (below) are encountered then they will be rethrown to the caller.
        /// if rethrow is false and any of the listed exception conditions are encountered then the method will have no effect.
        /// <para/>supports call chaining by returning given interconnectionTable instance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown if the given interconnectionTable is null</exception>
        /// <exception cref="System.ArgumentException">Thrown if the given interconnectionTable's Name is null or empty, or if the dictionary already contains a table with the given name.</exception>
        public static IValuesInterconnection AddTable(IValuesInterconnection interconnectionTable, bool rethrow)
        {
            try
            {
                if (interconnectionTable == null)
                    throw new System.ArgumentNullException("interconnectionTable");
                else if (interconnectionTable.Name.IsNullOrEmpty())
                    throw new System.ArgumentException("given table Name cannot be null or empty", "interconnectTable", null);

                lock (interconnectionTableDictionaryMutex)
                {
                    interconnectionTableDictionary.Add(interconnectionTable.Name, interconnectionTable);
                }
            }
            catch (System.Exception ex)
            {
                if (rethrow)
                    throw ex;
            }

            return interconnectionTable;
        }

        /// <summary>
        /// Gets the desired IValueInterconnection table form the static table dictionary mantained here (if the name is found in the dectionary) and returns is.
        /// If the given interconnectionTableName is null or Empty then this method returns the Values.Instance singleton.
        /// If the given name is not null, not Empty, and is not found in the table then this method returns null unless the addNewTableIfMissing parameter is true, in which case
        /// this method creates a new table of the given name, adds it to the table, and returns it.
        /// </summary>
        public static IValuesInterconnection GetTable(string interconnectionTableName, bool addNewTableIfMissing = true)
        {
            if (interconnectionTableName.IsNullOrEmpty())
                return Instance;

            lock (interconnectionTableDictionaryMutex)
            {
                IValuesInterconnection interconnectionTable = null;

                if (interconnectionTableDictionary.TryGetValue(interconnectionTableName, out interconnectionTable) && interconnectionTable != null)
                    return interconnectionTable;

                if (addNewTableIfMissing)
                {
                    interconnectionTable = new ValuesInterconnection(interconnectionTableName, false);
                    interconnectionTableDictionary[interconnectionTableName] = interconnectionTable;
                }

                return interconnectionTable;
            }
        }

        private static readonly object interconnectionTableDictionaryMutex = new object();
        private static Dictionary<string, IValuesInterconnection> interconnectionTableDictionary = new Dictionary<string, IValuesInterconnection>();

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
    public class ValuesInterconnection : IValuesInterconnection, IEnumerable
    {
        #region construction

        /// <summary>
        /// Constructor.  
        /// Allows the caller to determine if/when this interconnection table will be added to the static Global dictionary mantained in this class (provided that the given name is neither null nor empty)
        /// Allows caller to determine if this interconnection table's API needs to be thread safe.  
        /// This instance will use a mutex for thread safety if either of the makeAPIThreadSafe or the registerSelfInDictionary parameters are true.  
        /// Otherwise this instance will not make use of a mutex to enforce thread safety of its API and as such the client must either use only one thread or enforce non-renterant use on their own.
        /// </summary>
        public ValuesInterconnection(string name, bool registerSelfInDictionary = true, bool makeAPIThreadSafe = true)
        {
            Name = name;

            if (registerSelfInDictionary && !Name.IsNullOrEmpty())
                Values.AddTable(this, false);

            // assign the mutex to a new object (lock handle) or to null.  the mutex field is readonly so it can only be assigned in the constructor.
            mutex  = ((registerSelfInDictionary || makeAPIThreadSafe) ? new object() : null);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                return tableEntryDictionary.Values.ToArray().GetEnumerator();
            }
        }

        #endregion

        #region IValuesInterconnection interface

        /// <summary>Returns the name of this values interconnection (table space) object.</summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets/Sets the entire set of name mappings as an enumeration.  
        /// This set of mappings is used by GetValueAccessor to support mapping from the given name to an alternate name on a case by case basis.  
        /// This mapping table may be used to allow two (or more) entities to end up using the same table entry even if they do not know about each other in advance.
        /// </summary>
        public IEnumerable<Modular.Common.IMapNameFromTo> MapNameFromToSet 
        {
            get { using (var scopedLock = new ScopedLock(mutex)) { return nameMappingSet.ToArray(); } }
            set { using (var scopedLock = new ScopedLock(mutex)) { InnerSetMappingArray(value); } }
        }

        /// <summary>
        /// Adds the given set of MapNameFromTo items to the current mapping MapNameFromToSet.
        /// </summary>
        public IValuesInterconnection AddRange(IEnumerable<Modular.Common.IMapNameFromTo> addMapNameFromToSet)
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                InnerAddRange(addMapNameFromToSet);
            }

            return this;
        }

        /// <summary>
        /// IValueAccessor Factory method.  
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAccessor that can be used to observe and/or set the value of the named table entry.
        /// </summary>
        public IValueAccessor GetValueAccessor(string name, INamedValueSet metaDataIn = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
        {
            if (String.IsNullOrEmpty(name))
                return new ValueAccessorImpl(null, null);

            ValueTableEntry tableEntry = InnerGetValueTableEntry(name, metaDataIn, mergeBehavior);

            IValueAccessor adapter = new ValueAccessorImpl(this, tableEntry);

            return adapter.Update();
        }

        /// <summary>
        /// Typed (using generics) IValueAccessor{TValueType} Factory method.
        /// Finds or creates a new table entry, assigns and ID and then creates an IValueAccessor{TValueType} that can be used to observe and/or set the value of the named table entry.
        /// </summary>
        public IValueAccessor<TValueType> GetValueAccessor<TValueType>(string name, INamedValueSet metaDataIn = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
        {
            if (String.IsNullOrEmpty(name))
                return new ValueAccessorImpl<TValueType>(null, null);

            ValueTableEntry tableEntry = InnerGetValueTableEntry(name, metaDataIn, mergeBehavior);

            IValueAccessor<TValueType> adapter = new ValueAccessorImpl<TValueType>(this, tableEntry);

            return adapter.Update();
        }

        /// <summary>Returns an array of the names of all of the values in this interconnection table instance.</summary>
        public string[] ValueNamesArray 
        { 
            get 
            {
                return GetValueNamesRange();
            } 
        }

        /// <summary>
        /// Returns a subset list of the overall ValueNamesArray.  Allows the caller to get names that have been added since they last obtained a full array.
        /// If <paramref name="maxNumItems"/> is passed as zero then the full set of names starting at the given <paramref name="startAtIndex"/> will be returned.
        /// </summary>
        public string [] GetValueNamesRange(int startAtIndex = 0, int maxNumItems = 0)
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                int numItems = Math.Max(0, tableItemNamesList.Count - startAtIndex);
                if (numItems > maxNumItems && maxNumItems > 0)
                    numItems = maxNumItems;

                if (numItems > 0 && startAtIndex >= 0)
                    return tableItemNamesList.GetRange(startAtIndex, numItems).ToArray();
                else
                    return emptyStringArray;
            }
        }

        /// <summary>
        /// Returns a filtered subset of the names of the overall table of named items.
        /// <param name="tableItemFilter">gives the caller provided predicate used to determine which table entries (by name and metadata) shall be included in the resulting set</param>
        /// <param name="startAtIndex">Indicates the number of table entries to skip before applying the given filter</param>
        /// <param name="maxNumItems">Gives the maximum number of items to pass to the filter during this call, or 0 to indicate that there is no limit</param>
        /// </summary>
        public string[] GetFilteredNames(Func<string, INamedValueSet, bool> tableItemFilter, int startAtIndex = 0, int maxNumItems = 0)
        {
            tableItemFilter = tableItemFilter ?? fallbackTableItemFilter;

            using (var scopedLock = new ScopedLock(mutex))
            {
                return TableArray.Skip(startAtIndex).Take(maxNumItems.MapDefaultTo(int.MaxValue)).Where(tableEntry => tableItemFilter(tableEntry.Name, tableEntry.MetaData)).Select(tableEntry => tableEntry.Name).ToArray();
            }
        }

        /// <summary>
        /// Returns a set of the IValueAccessors from table of named items filtered by using the given <paramref name="tableItemFilter"/>.
        /// <param name="tableItemFilter">gives the caller provided predicate used to determine which table entries (by name and metadata) shall be included in the resulting set</param>
        /// <param name="startAtIndex">Indicates the number of table entries to skip before applying the given filter</param>
        /// <param name="maxNumItems">Gives the maximum number of items to pass to the filter during this call, or 0 to indicate that there is no limit</param>
        /// </summary>
        public IValueAccessor[] GetFilteredValueAccessors(Func<string, INamedValueSet, bool> tableItemFilter, int startAtIndex = 0, int maxNumItems = 0)
        {
            tableItemFilter = tableItemFilter ?? fallbackTableItemFilter;

            using (var scopedLock = new ScopedLock(mutex))
            {
                return TableArray.Skip(startAtIndex).Take(maxNumItems.MapDefaultTo(int.MaxValue)).Where(tableEntry => tableItemFilter(tableEntry.Name, tableEntry.MetaData)).Select(tableEntry => new ValueAccessorImpl(this, tableEntry)).ToArray();
            }
        }

        /// <summary>Default filter accepts all items that are given to it</summary>
        private static readonly Func<string, INamedValueSet, bool> fallbackTableItemFilter = (name, nvs) => true;
        private static readonly string[] emptyStringArray = new string[0];

        /// <summary>
        /// Provides a property that returns the current total number of named values in this values interconnect table space.
        /// This property is implemented so that it can be efficiently, and frequently checked, to detect the addition of new value names to the table.
        /// <para/>Please note:  this value is updated after items have actually been added to the table.  
        /// As such, it is possible that this value will be smaller than the ValueNamesArray length for a brief period of time while another thread is adding a new value accessor.
        /// </summary>
        public int ValueNamesArrayLength { get { return volatileTableItemNamesListCount; } }

        /// <summary>
        /// This method is used internally by IValueAccessor instances to lock the table space and set the corresponding table entry from the calling/given IValueAccessor instance's value.
        /// </summary>
        internal void Set(IValueAccessor accessor)
        {
            if (accessor != null)
            {
                using (var scopedLock = new ScopedLock(mutex))
                {
                    ((accessor as ValueAccessorImpl) ?? emptyValueAccessor).InnerGuardedSetTableEntryFromValue();
                    SynchrounousCustomPostSetTableEntryFromValueHandler(accessor);

                    globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
                }

                notificationList.Notify();
            }
        }

        /// <summary>
        /// This method is used internally by IValueAccessor instances to lock the table space and set/update the corresponding table entry's meta data.
        /// </summary>
        internal void SetMetaData(IValueAccessor accessor, INamedValueSet metaDataIn, NamedValueMergeBehavior mergeBehavior)
        {
            using (var scopedLock = new ScopedLock(mutex))
            {
                bool mergeBehaviorIsReplace = ((mergeBehavior & NamedValueMergeBehavior.Replace) != 0);
                INamedValueSet metaDataNVS_replaceAsRO = mergeBehaviorIsReplace ? metaDataIn.ConvertToReadOnly(mapNullToEmpty: true).BuildDictionary() : null;

                ((accessor as ValueAccessorImpl) ?? emptyValueAccessor).InnerGuardedSetMetaData(metaDataIn, mergeBehavior, metaDataNVS_replaceAsRO);

                SynchrounousCustomPostSetTableEntryFromValueHandler(accessor);

                globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
            }

            notificationList.Notify();
        }


        /// <summary>
        /// This method is used internally by IValueAccessor instances to lock the table space and reset the corresponding table entry from the calling/given IValueAccessor instance.
        /// </summary>
        internal void Reset(IValueAccessor accessor)
        {
            if (accessor != null)
            {
                using (var scopedLock = new ScopedLock(mutex))
                {
                    ((accessor as ValueAccessorImpl) ?? emptyValueAccessor).InnerGuardedResetTableEntry();
                    SynchrounousCustomPostSetTableEntryFromValueHandler(accessor);

                    globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
                }

                notificationList.Notify();
            }
        }

        /// <summary>
        /// This method is used to Set the table entry values for an array of IValueAccessor instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        /// <remarks>This signature is being retained for backward comptability</remarks>
        public void Set(IValueAccessor[] accessorArray, bool optimize)
        {
            Set(accessorArray, setMode: optimize ? SetMode.Optimize : SetMode.Default, numEntriesToSet: -1);
        }

        /// <summary>
        /// This method is used to Set the table entry values for a portion of an array of IValueAccessor instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="numEntriesToSet">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.  When negative value is passed, this parameter's value will be replaced with accessorArray.Length</param>
        /// <param name="optimize">When false all accessors will have their current value pushed into the corresonding table entries.  When true, only accessors that have their IsSetPending property true will have their value pushed in to the corresponding table entries</param>
        public void Set(IValueAccessor[] accessorArray, int numEntriesToSet, bool optimize)
        {
            Set(accessorArray, setMode: optimize ? SetMode.Optimize : SetMode.Default, numEntriesToSet: numEntriesToSet);
        }

        /// <summary>
        /// This method is used to Set the table entry values for a portion of an array of IValueAccessor instances.  
        /// This arrayed set operation is performed atomically across all of the table entries referred to by the non-null adapters in the array.
        /// The optimize flag indicates if the caller would like all accessors to be set or just those that have their IsSetPending flag set.
        /// <para/>This method is specifically intended for use by ValueSetAdapter instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Set.</param>
        /// <param name="setMode">Defines the mode of set that will be used</param>
        /// <param name="numEntriesToSet">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.  When negative value is passed, this parameter's value will be replaced with accessorArray.Length</param>
        public void Set(IValueAccessor[] accessorArray, SetMode setMode, int numEntriesToSet = -1)
        {
            accessorArray = accessorArray ?? emptyValueAccessorArray;

            bool optimize = ((setMode & SetMode.Optimize) != 0);
            bool ifValueSeqNumIsZero = ((setMode & SetMode.IfValueSeqNumIsZero) != 0);

            int accessorArrayLength = accessorArray.Length;
            if (numEntriesToSet < 0 || numEntriesToSet > accessorArrayLength)
                numEntriesToSet = accessorArrayLength;

            int numEntriesSet = 0;

            if (optimize || ifValueSeqNumIsZero)
            {
                bool areAnySetsPending = false;

                if (optimize)
                    areAnySetsPending |= accessorArray.Any(iva => iva != null && iva.IsSetPending);

                if (ifValueSeqNumIsZero)
                    areAnySetsPending |= accessorArray.Any(iva => iva != null && iva.CurrentSeqNum == 0);

                if (!areAnySetsPending)
                    return;
            }

            using (var scopedLock = new ScopedLock(mutex))
            {
                for (int idx = 0; idx < numEntriesToSet; idx++)
                {
                    IValueAccessor accessor = accessorArray[idx];
                    ValueAccessorImpl valueAccessor = ((accessor as ValueAccessorImpl) ?? emptyValueAccessor);

                    if (accessor != null && (accessor.IsSetPending || !optimize) && (!ifValueSeqNumIsZero || accessor.CurrentSeqNum == 0) && valueAccessor.IVI == this)
                    {
                        valueAccessor.InnerGuardedSetTableEntryFromValue();

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
        /// This method is used to Set (or Update/Merge) the given set of table items meta data from the given <paramref name="metaDataIn"/>.  
        /// <paramref name="mergeBehavior"/> may be used to control how the given <paramref name="metaDataIn"/> is combined with the existing meta data for each such selected item.
        /// </summary>
        /// <param name="accessorArray">Gives the set of table entry item names that are to be updated</param>
        /// <param name="metaDataIn">Gives the meta date that is to be used to update the selected items (as determined by the <paramref name="mergeBehavior"/> parameter)</param>
        /// <param name="mergeBehavior">Defines how the selected items existing meta data will be combined with the given value.</param>
        public void SetMetaData(IValueAccessor[] accessorArray, INamedValueSet metaDataIn, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
        {
            int numEntriesUpdated = 0;

            using (var scopedLock = new ScopedLock(mutex))
            {
                bool mergeBehaviorIsReplace = ((mergeBehavior & NamedValueMergeBehavior.Replace) != 0);
                INamedValueSet metaDataNVS_replaceAsRO = mergeBehaviorIsReplace ? metaDataIn.ConvertToReadOnly(mapNullToEmpty: true).BuildDictionary() : null;

                foreach (IValueAccessor accessor in accessorArray ?? emptyValueAccessorArray)
                {
                    ValueAccessorImpl valueAccessor = ((accessor as ValueAccessorImpl) ?? emptyValueAccessor);

                    if (accessor != null && valueAccessor.IVI == this)
                    {
                        valueAccessor.InnerGuardedSetMetaData(metaDataIn, mergeBehavior, metaDataNVS_replaceAsRO);

                        SynchrounousCustomPostSetTableEntryFromValueHandler(accessor);

                        numEntriesUpdated++;
                    }
                }

                if (numEntriesUpdated != 0)
                    globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
            }

            if (numEntriesUpdated != 0)
                notificationList.Notify();
        }

        /// <summary>
        /// This method is used internally by IValueAccessor instances to lock the table space and update the accessor's copy of the value and sequence number from the corresponding table entry.
        /// </summary>
        /// <remarks>We do not need to verify that this IVI instance is the correct one since this method is only called by the ValueAccessorImpl objects created by this instance.</remarks>
        protected void Update(IValueAccessor accessor)
        {
            if (accessor != null)
            {
                ValueAccessorImpl valueAccessor = ((accessor as ValueAccessorImpl) ?? emptyValueAccessor);

                using (var scopedLock = new ScopedLock(mutex))
                {
                    if (valueAccessor.IVI == this)
                        valueAccessor.InnerGuardedUpdateValueFromTableEntry();
                }
            }
        }

        /// <summary>
        /// This method is used to Update a set/array of IValueAccessor instances from the corresponding set of interconnection table entry values.  
        /// This arrayed update operation is performed atomically across the table entries referred to by the non-null adapters in the array up to the given maximum item index to update.
        /// <para/>This method is specifically intended for use by Custom update scanner instances.
        /// </summary>
        /// <param name="accessorArray">Gives an array of items.  Only non-null items will be Updated.</param>
        /// <param name="numEntriesToUpdate">Limits how much of the array will be used.  Maximum index of itesm that are looked at will be &lt; this value.  When negative value is passed, this parameter's value will be replaced with accessorArray.Length</param>
        public void Update(IValueAccessor[] accessorArray, int numEntriesToUpdate = -1)
        {
            accessorArray = accessorArray ?? emptyValueAccessorArray;

            int accessorArrayLength = accessorArray.Length;
            if (numEntriesToUpdate < 0 || numEntriesToUpdate > accessorArrayLength)
                numEntriesToUpdate = accessorArrayLength;

            using (var scopedLock = new ScopedLock(mutex))
            {
                for (int idx = 0; idx < numEntriesToUpdate; idx++)
                {
                    IValueAccessor accessor = accessorArray[idx];
                    ValueAccessorImpl valueAccessor = ((accessor as ValueAccessorImpl) ?? emptyValueAccessor);

                    if (valueAccessor.IVI == this)
                        valueAccessor.InnerGuardedUpdateValueFromTableEntry();
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
        /// This property may be used by a derived class to specify a custom handler that is given each IValueAccessor that has been used to perform an InnerGuardedSetTableEntryFromValue method.
        /// <para/>Please note: this call is made synchronously while owning the table's mutex.  
        /// Care must be exersized to minimize the time consumed in this method and to avoid creating any possiblity of a deadlock by attempting to acquire mutiple lock objects within the
        /// call lifetime of this delegate.  This delegate may be combined with the use of the notificationList for combinations of synchronous and asynchronous signaling.
        /// </summary>
        public Action<IValueAccessor> SynchrounousCustomPostSetTableEntryFromValueHandler
        {
            get { return synchronousCustomPostSetTableEntryFromValueHandler ?? emptyPostSetTableEntryFromValueHandler; }
            set { synchronousCustomPostSetTableEntryFromValueHandler = value; }
        }
        private Action<IValueAccessor> synchronousCustomPostSetTableEntryFromValueHandler = null;
        private static readonly Action<IValueAccessor> emptyPostSetTableEntryFromValueHandler = (iva) => { };

        #endregion

        #region Inner methods (name mapping: InnerGetValueTableEntry, InnerSetMappingArray, InnerMapSanitizedName, InnerResetNameMapping, InnerAddRange1

        /// <summary>
        /// Common method used by the two IValueAccessor factory methods.  This method takes a given name and returns the ValueTableEntry instance for that name, 
        /// adding the name and a new ValueTableEntry instance if needed.
        /// </summary>
        private ValueTableEntry InnerGetValueTableEntry(string name, INamedValueSet metaDataIn = null, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
        {
            ValueTableEntry tableEntry = null;

            using (var scopedLock = new ScopedLock(mutex))
            {
                name = InnerMapSanitizedName(name.Sanitize());

                if (!tableEntryDictionary.TryGetValue(name, out tableEntry) || tableEntry == null)
                {
                    tableEntry = new ValueTableEntry(name, table.Count + 1);
                    table.Add(tableEntry);
                    tableArray = null;
                    tableItemNamesList.Add(name);
                    tableEntryDictionary[name] = tableEntry;

                    volatileTableItemNamesListCount++;
                }

                if (tableEntry != null && !metaDataIn.IsNullOrEmpty())
                {
                    tableEntry.SetMetaData(metaDataIn, mergeBehavior, (metaDataIn.IsReadOnly ? metaDataIn : null));
                }

                globalSeqNum = InnerGuardedIncrementSkipZero(globalSeqNum);
            }

            return tableEntry;
        }

        private Modular.Common.MapNameFromToList nameMappingSet = new Modular.Common.MapNameFromToList();
        private volatile Dictionary<string, string> nameMappingDictionary = new Dictionary<string, string>();

        protected void InnerSetMappingArray(IEnumerable<Modular.Common.IMapNameFromTo> nameMappingSet)
        {
            InnerResetNameMapping();
            InnerAddRange(nameMappingSet);
        }

        protected string InnerMapSanitizedName(string name)
        {
            Dictionary<string, string> nmp = nameMappingDictionary;

            string mappedName = null;

            if (nmp.TryGetValue(name, out mappedName) && mappedName != null)
                return mappedName;

            if (nameMappingSet.Map(name, ref mappedName) && mappedName != null)
                return mappedName;

            return name;
        }

        protected void InnerResetNameMapping()
        {
            nameMappingSet = new Modular.Common.MapNameFromToList();
            nameMappingDictionary = new Dictionary<string, string>();
        }

        private void InnerAddRange(IEnumerable<Modular.Common.IMapNameFromTo> addMapNameFromToSet)
        {
            foreach (Modular.Common.IMapNameFromTo mapItem in addMapNameFromToSet ?? emptyMapFromToArray)
            {
                nameMappingSet.Add(mapItem);
                if (mapItem.IsSimpleMap)
                    nameMappingDictionary[mapItem.From.Sanitize()] = mapItem.To.Sanitize();
                else if (mapItem is Modular.Common.MapNameFromToList)
                    InnerAddRange(mapItem as IEnumerable<Modular.Common.IMapNameFromTo>);
            }
        }

        private Modular.Common.IMapNameFromTo[] emptyMapFromToArray = new Common.IMapNameFromTo[0];

        #endregion

        #region private Table and table space related implementation fields

        /// <summary>
        /// table space and dictionary mutex (now optional - may be null for clients that do not need the API to be thread safe).  
        /// Note: this entity does not use reader/writer locks in expectation that methods used here are sufficiently fast so that there will not be
        /// a large amount of contention for the use of the table when performing activities here and based on the expectation that a simple mutex has a faster internal implementation
        /// than would be produced by using a reader writer lock, with its additional overhead in total number of interlocked operations even when there is little contention.  
        /// </summary>
        /// <remarks>
        /// Annotate this as readonly since the value is only assigned once in the constructor (to either new object() or null).
        /// </remarks>
        private readonly object mutex;

        /// <summary>Basic list of the names of all of the items in the table</summary>
        private List<string> tableItemNamesList = new List<string>();

        /// <summary>Volatile count of the number of items that have been added to the tableItemNamesList.  This is updated after the itesm have been added to the list.</summary>
        private volatile int volatileTableItemNamesListCount = 0;

        /// <summary>This is the dictionary that is used to convert value names into the corresponding ValueTableEntry that is used for that name.</summary>
        private Dictionary<string, ValueTableEntry> tableEntryDictionary = new Dictionary<string, ValueTableEntry>();

        /// <summary>This is the "table".  It consists of a list of the created ValueTableEntry items and can be indexed by their IDs as needed.</summary>
        private List<ValueTableEntry> table = new List<ValueTableEntry>();

        /// <summary>cached array version of table list object</summary>
        private ValueTableEntry[] tableArray = null;

        /// <summary>getter Property for tableArray cached array.  Will create the table array from the table list if needed.</summary>
        private ValueTableEntry[] TableArray { get { return tableArray ?? (tableArray = table.ToArray()); } }

        /// <summary>empty array of accessor objects to avoid needing to do null pointer checks in foreach iterators.</summary>
        private static readonly IValueAccessor[] emptyValueAccessorArray = new IValueAccessor[0];
        /// <summary>empty ValueAccessor to simplify safe invocation of down-casted method using ?? operator.</summary>
        private static readonly ValueAccessorImpl emptyValueAccessor = new ValueAccessorImpl(null, null);

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
        internal class ValueTableEntry
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

            /// <summary>Contains the most recently assigned sequence number that goes with the current ValueContainer value.  This value is only zero when the table entry has never been assigned a value.</summary>
            public UInt32 ValueSeqNum { get { return valueSeqNum; } }

            /// <summary>Contains the most recently assigned sequence number that goes with the current MetaData value.  This value is only zero when the table entry has never been assigned a meta data value.</summary>
            public UInt32 MetaDataSeqNum { get { return metaDataSeqNum; } }

            /// <summary>Returns the most recently assigned value for this table entry.</summary>
            public ValueContainer VC { get { return vc; } }

            /// <summary>Returns the most recently updated meta data for that table entry.</summary>
            public INamedValueSet MetaData { get { return publishedMetaData; } }

            /// <summary>Backing store for VC propety</summary>
            private ValueContainer vc;

            public NamedValueSet workingMetaData = null;
            public INamedValueSet publishedMetaData = NamedValueSet.Empty;

            /// <summary>used by one or more attached accessors to Set the value of the table entry.  Also increments the SeqNum, skipping zero which is reserved as the initial unwritten state.</summary>
            /// <remarks>
            /// Added special case for creating explicit copy of a IList{String} in if the given list is not equal to the current contents of the 
            /// </remarks>
            public void Set(ref ValueContainer valueContainerIn)
            {
                vc.DeepCopyFrom(valueContainerIn);

                valueSeqNum = InnerGuardedIncrementSkipZero(valueSeqNum);
            }

            /// <summary>used by one or more attached accessors to allow them to complete IVI triggered meta data changes.</summary>
            public void SetMetaData(INamedValueSet metaDataIn, NamedValueMergeBehavior mergeBehavior, INamedValueSet metaDataNVS_replaceAsRO)
            {
                workingMetaData = workingMetaData.MergeWith(metaDataIn, mergeBehavior: mergeBehavior);

                if (((mergeBehavior & NamedValueMergeBehavior.Replace) != 0) && metaDataNVS_replaceAsRO != null)
                    publishedMetaData = metaDataNVS_replaceAsRO;
                else
                    publishedMetaData = workingMetaData.ConvertToReadOnly(mapNullToEmpty: true);

                metaDataSeqNum = InnerGuardedIncrementSkipZero(metaDataSeqNum);
            }

            /// <summary>
            /// Sets the contained ValueContainer to be empty and resets the SeqNum to zero
            /// </summary>
            public void Reset()
            {
                vc.SetToEmpty();

                workingMetaData = null;
                publishedMetaData = NamedValueSet.Empty;

                valueSeqNum = 0;
                metaDataSeqNum = 0;
            }

            /// <summary>Backing storeage for the SeqNum.  seqNum is defined to be volatile because it is observed without owning the table lock, even though it is only updated while owning the table lock.</summary>
            private volatile UInt32 valueSeqNum = 0;

            /// <summary>Backing storeage for the SeqNum.  seqNum is defined to be volatile because it is observed without owning the table lock, even though it is only updated while owning the table lock.</summary>
            private volatile UInt32 metaDataSeqNum = 0;

            /// <summary>
            /// Purely for debugging assistance - allows debugger to look at raw table directly.
            /// </summary>
            public override string ToString()
            {
                if (metaDataSeqNum == 0)
                    return "VTE: {0:d4}:{1} seqNum:{2} {3}".CheckedFormat(ID, Name, ValueSeqNum, VC);
                else
                    return "VTE: {0:d4}:{1} seqNum:{2}/{3} {4} md:{5}".CheckedFormat(ID, Name, ValueSeqNum, MetaDataSeqNum, VC, publishedMetaData.ToString(includeROorRW: false));
            }
        }

        /// <summary>Implementation class for the IValueAccessor interface.  Provides pure ValueAsObject based use of the Value Interconnection instance that created this object</summary>
        internal class ValueAccessorImpl : IValueAccessor
        {
            /// <summary>Internal constructor.  Requires parent ValuesIterconnection instance and ValueTableEntry instance to which this accessor is attached.</summary>
            public ValueAccessorImpl(ValuesInterconnection ivi, ValueTableEntry tableEntry)
            {
                IVI = ivi;
                TableEntry = tableEntry;
                metaData = (tableEntry != null) ? tableEntry.MetaData : NamedValueSet.Empty;
            }

            /// <summary>Retains the ValuesInterconnection instance to which this accessor belongs, or null if there is none.</summary>
            internal ValuesInterconnection IVI { get; private set; }

            /// <summary>Retains the ValueTableEntry to which this accessor is attached, or null if there is none.</summary>
            private ValueTableEntry TableEntry { get; set; }

            /// <summary>Gives the Name of the Value that this accessor is used to access.</summary>
            public string Name { get { return ((TableEntry != null) ? TableEntry.Name : String.Empty); } }

            /// <summary>Gives the ID number assigned to this Named value's table entry in the interconnection table space.</summary>
            public int ID { get { return ((TableEntry != null) ? TableEntry.ID : 0); } }

            /// <summary>Gives access to the meta data stored for this named item in the corresponding IValuesInterconnection table.</summary>
            public INamedValueSet MetaData { get { return metaData; } }

            /// <summary>
            /// get/set property gives access to the value object that the accessor uses to contain the current value.
            /// Setter sets IsSetPending if the given value is not equal to the current one.
            /// <para/>This is a value object and as such this property can only be assigned an entire ValueContainer.
            /// </summary>
            public ValueContainer VC 
            { 
                get { return vc; } 
                set 
                {
                    IsSetPending |= !vc.IsEqualTo(value);
                    vc = value; 
                } 
            }

            /// <summary>
            /// get/set property is an alternative name for the VC property
            /// </summary>
            public ValueContainer ValueContainer { get { return VC; } set { VC = value; } }

            protected ValueContainer vc = default(ValueContainer);

            protected INamedValueSet metaData;

            /// <summary>
            /// get/set property contains the last set or the last updated value for this accessor.  
            /// The setter just updates the value that can be used later with the Set method.
            /// </summary>
            public object ValueAsObject 
            { 
                get { return vc.ValueAsObject; } 
                set { VC = new ValueContainer(value); } 
            }

            /// <summary>This property is set whenever the ValueContainer is changed and is cleared whenver the update has been delievered to the ValueInterconnection instance or when Update is called.</summary>
            public bool IsSetPending { get; set; }

            /// <summary>Sets the corresponding interconnection table space entry's value from the current ValueAsObject value.  This method supports call chaining.</summary>
            public IValueAccessor Set()
            {
                if (IVI != null)
                    IVI.Set(this);

                return this;
            }

            /// <summary>Resets this value accessor and the corresponding IVI table entry to be empty with sequence number zero.</summary>
            public IValueAccessor Reset()
            {
                vc.SetToEmpty();
                metaData = NamedValueSet.Empty;

                if (IVI != null)
                    IVI.Reset(this);

                return this;
            }

            /// <summary>Sets ValueContainer to the given value and then Sets the corresponding interconnection table space entry's value from the given one.  This method supports call chaining.</summary>
            public IValueAccessor Set(ValueContainer valueContainerIn)
            {
                vc = valueContainerIn;

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
                VC = valueContainer;

                if (IsSetPending)
                    Set();

                return this;
            }

            /// <summary>Converts valueAsObject to a ValueContainer and then calls SetIfDifferent(valueContainer) to set the access and the corresponding interconnect table entry from it if needed.  This method supports call chaining.</summary>
            public IValueAccessor SetIfDifferent(object valueAsObject)
            {
                return SetIfDifferent(new ValueContainer().SetFromObject(valueAsObject));
            }

            /// <summary>This property returns true if the ValueSeqNum is not the same as the CurrentSeqNum or MetaDataSeqNum is not the same as CurrentMetaDataSeqNum.</summary>
            public bool IsUpdateNeeded { get { return ((ValueSeqNum != CurrentSeqNum) || (MetaDataSeqNum != CurrentMetaDataSeqNum)); } }

            /// <summary>
            /// This method updates the locally stored value, metadata, and seqNums from the interconnection table space's corresponding table entry.  
            /// If the value IsSetPending then its value will be replaced by the table entry's value.
            /// This method supports call chaining.
            /// </summary>
            public IValueAccessor Update()
            {
                if ((IsUpdateNeeded || IsSetPending) && IVI != null)
                    IVI.Update(this);

                return this;
            }

            /// <summary>Gives the current sequence number of the value that is currently in the interconnection table.  The value zero is only used when the table entry has never been assigned a value.</summary>
            public UInt32 CurrentSeqNum { get { return ((TableEntry != null) ? TableEntry.ValueSeqNum : 0); } }

            /// <summary>Gives the sequence number of the value that was last Set to, or updated from, the interconnection table.  The accessor may be updated if this value is not equal to CurrentSeqNum.</summary>
            public UInt32 ValueSeqNum { get; set; }

            /// <summary>Gives the current sequence number of the value/md that is currently in the interconnection table.  The value zero is only used when the table entry has never been changed from its initial empty state.</summary>
            public UInt32 CurrentMetaDataSeqNum { get { return ((TableEntry != null) ? TableEntry.MetaDataSeqNum : 0); } }

            /// <summary>Gives the sequence number of the value/md that was last Set to, or updated from, the interconnection table.  The accessor may be updated if this value is not equal to CurrentMetaDataSeqNum.</summary>
            public UInt32 MetaDataSeqNum { get; set; }

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
                if (TableEntry != null)
                {
                    uint capturedCurrentValueSeqNum = TableEntry.ValueSeqNum;
                    uint capturedMetaDataSeqNum = TableEntry.MetaDataSeqNum;

                    if (ValueSeqNum != capturedCurrentValueSeqNum || IsSetPending)
                    {
                        vc = TableEntry.VC;
                        ValueSeqNum = capturedCurrentValueSeqNum;

                        IsSetPending = false;
                    }

                    if (MetaDataSeqNum != capturedMetaDataSeqNum)
                    {
                        metaData = TableEntry.MetaData;
                        MetaDataSeqNum = capturedMetaDataSeqNum;
                    }
                }
                else
                {
                    vc.SetToEmpty();
                    metaData = NamedValueSet.Empty;
                    ValueSeqNum = MetaDataSeqNum = 0;
                }
            }

            /// <summary>
            /// Internal method used to Set the TableEntry's value from the accessors current ValueAsObject value using the TableEntry.Set(value) method.  Also updates
            /// the ValueSeqNum from the TableEntries SeqNum (via CurrentSeqNum).
            /// <para/>This method may only be called from the ValueInterconnect internal logic while the current thread has exclusive access to the TableEntry in question.
            /// </summary>
            internal void InnerGuardedSetTableEntryFromValue()
            {
                if (TableEntry != null)
                    TableEntry.Set(ref vc);

                IsSetPending = false;

                ValueSeqNum = CurrentSeqNum;
            }

            /// <summary>
            /// Internal method used to Reset the TableEntry's value and seqNum.  Also updates the ValueSeqNum from the TableEntries SeqNum (via CurrentSeqNum).
            /// <para/>This method may only be called from the ValueInterconnect internal logic while the current thread has exclusive access to the TableEntry in question.
            /// </summary>
            internal void InnerGuardedResetTableEntry()
            {
                if (TableEntry != null)
                    TableEntry.Reset();

                IsSetPending = false;

                ValueSeqNum = CurrentSeqNum;
                MetaDataSeqNum = CurrentMetaDataSeqNum;
            }

            /// <summary>
            /// Internal method used to pass meta data set/update operation through to the corresponding TableEntry.  Also updates this accessor's metaData and MetaDataSeqNum.
            /// </summary>
            internal void InnerGuardedSetMetaData(INamedValueSet metaDataIn, NamedValueMergeBehavior mergeBehavior, INamedValueSet metaDataNVS_replaceAsRO)
            {
                if (TableEntry != null)
                    TableEntry.SetMetaData(metaDataIn, mergeBehavior, metaDataNVS_replaceAsRO);

                metaData = TableEntry.MetaData;

                MetaDataSeqNum = CurrentMetaDataSeqNum;
            }

            /// <summary>
            /// debug and logging helper method.
            /// </summary>
            public override string ToString()
            {
                uint capturedCurrentValueSeqNum = CurrentSeqNum;
                uint capturedCurrentMetaDataSeqNum = CurrentMetaDataSeqNum;

                string mdStr = string.Empty;

                if (MetaDataSeqNum != 0 || capturedCurrentMetaDataSeqNum != 0)
                {
                    if (MetaDataSeqNum == capturedCurrentMetaDataSeqNum)
                        mdStr = " md:{0} mdSeq:{1}".CheckedFormat(MetaData.ToString(includeROorRW: false), MetaDataSeqNum);
                    else
                        mdStr = " md:{0} mdSeq:{1} curr:{2}".CheckedFormat(MetaData.ToString(includeROorRW: false), MetaDataSeqNum, capturedCurrentMetaDataSeqNum);
                }

                if (IsSetPending)
                    return "Accessor '{0}'@{1} {2} [SetPending vSeq:{3} curr:{4}]{5}".CheckedFormat(Name, ID, VC, ValueSeqNum, capturedCurrentValueSeqNum, mdStr);
                else if (ValueSeqNum != capturedCurrentValueSeqNum)
                    return "Accessor '{0}'@{1} {2} [UpdateNeeded vSeq:{3} curr:{4}]{5}".CheckedFormat(Name, ID, VC, ValueSeqNum, capturedCurrentValueSeqNum, mdStr);
                else
                    return "Accessor '{0}'@{1} {2} [vSeq:{3}]{4}".CheckedFormat(Name, ID, VC, ValueSeqNum, mdStr);
            }
        }

        /// <summary>Implementation class for the IValueAccessor{TValueType} interface to provide type converted access to an Interconnection table space value.  Extends the ValueAccessor class.</summary>
        private class ValueAccessorImpl<TValueType> : ValueAccessorImpl, IValueAccessor<TValueType>
        {
            /// <summary>Internal constructor.  Requires parent ValuesIterconnection instance and ValueTableEntry instance to which this accessor is attached.</summary>
            internal ValueAccessorImpl(ValuesInterconnection interconnectInstance, ValueTableEntry tableEntry)
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
                    if (vc.IsNullOrEmpty && !decodedValueType.IsReferenceType() && !decodedTypeIsNullable)
                    {
                        if ((LastValueGetException as System.NullReferenceException) == null)
                            LastValueGetException = new NullReferenceException();

                        return default(TValueType);
                    }

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
                    VC = new ValueContainer().SetValue<TValueType>(value, decodedValueType, decodedTypeIsNullable);
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
                return vc.GetValue<TValueType>(decodedValueType, decodedTypeIsNullable, rethrow);
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
            /// This method updates the locally stored value, metadata, and seqNums from the interconnection table space's corresponding table entry.  
            /// If the value IsSetPending then its value will be replaced by the table entry's value.
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

    #region ValueSetItemAttribute, IValueSetAdpater, IModularValueSetAdapter, ValueSetAdapaterGroup, ValueSetAdapter, ValueSetAdapterBase

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
            /// <para/>Name = null, NameAdjust = NameAdjust.Prefix0, SilenceIssues = false, StorageType = ContainerStorageType.None
            /// </summary>
            public ValueSetItemAttribute() 
            { }
        }
    }

    /// <summary>
    /// ValueSet type agnostic interface for public methods in actual ValueSetAdapter implementation class
    /// </summary>
    public interface IValueSetAdapter
    {
        /// <summary>Defines the emitter used to emit Setup, Set, and Update related errors.  Defaults to the null emitter.</summary>
        Logging.IMesgEmitter IssueEmitter { get; set; }

        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        Logging.IMesgEmitter ValueNoteEmitter { get; set; }

        /// <summary>Allows the client to specify the default meta data to use with each IValueAccessor obtained during Setup.</summary>
        INamedValueSet DefaultMetaData { get; set; }

        /// <summary>Allows the client to specify the default meta data merge behavior to use with each IValueAccessor obtained during Setup.</summary>
        NamedValueMergeBehavior DefaultMetaDataMergeBehavior { get; set; }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Will use previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        IValueSetAdapter Setup(params string[] baseNames);

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>If a non-null valueInterconnect instance is given then it will be used otherwise this method will use any previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="ivi">Allows the caller to (re)specifiy the IValuesInterconnection instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        IValueSetAdapter Setup(IValuesInterconnection ivi, params string[] baseNames);

        /// <summary>
        /// Transfer the values from the ValueSet's annotated members to the corresponding set of IValueAccessors and then tell the IValuesInterconnection instance
        /// to Set all of the IValueAccessors.
        /// <para/>Supports call chaining.
        /// </summary>
        IValueSetAdapter Set();

        /// <summary>
        /// This property determines if the Set method uses ValueContainer equality testing to determine which IValueAccessor objects to actually write to the table.
        /// When this property is true (the default), equality testing will be used to prevent updating table entires for IValueAccessors that do not have a set pending (due to change in container value).
        /// When this property is false, all value table entries will be Set, without regard to whether their value might have changed.
        /// </summary>
        bool OptimizeSets { get; set; }

        /// <summary>
        /// Returns true if any of the IValueAccessors to which this adapter is attached indicate that there is a pending update 
        /// (because the accessed value has been set elsewhere so there may be a new value to update that accessor from).
        /// </summary>
        bool IsUpdateNeeded { get; }

        /// <summary>
        /// Requests the IValuesInterconnection instance to update all of the adapter's IValueAccessor objects and then transfers the updated values
        /// from those accessor objects to the corresponding annotated ValueSet members.
        /// <para/>Supports call chaining.
        /// </summary>
        IValueSetAdapter Update();

        /// <summary>
        /// Gives the caller access to the set of IValueAccessors that have been created by this adatper and which are used to interact with the corresponding IVI
        /// <para/>This array will be empty until Setup as been successfully invoked.
        /// </summary>
        IValueAccessor [] IVAArray { get; }

        /// <summary>
        /// Gives the caller access to the number of items (IVAs) that this adapter is using.
        /// </summary>
        int NumItems { get; }
    }

    /// <summary>
    /// This interface extends the <see cref="IValueSetAdapter"/> one by adding the TransferValuesFromIVAs and TransferValuesToIVAs methods.
    /// These methods may be used to allow a value set adapter to be added to a <see cref="ValueSetAdapterGroup"/> or to otherwise be used to generate as set of IVAs and allow
    /// an external entity to manage the actual IVI to/from IVA interaction (likely using arrays of IVAs from this and other sources) while preserving the abstraction that this
    /// adapter provides in supporting customized local binding/adapting the the IVA values to other value endpoints.
    /// </summary>
    public interface IModularValueSetAdapter : IValueSetAdapter
    {
        /// <summary>
        /// Transfers the current values from the associated set of IVAs IVAArray to the adapter specific set of value endpoints.
        /// This method expects/requires that some other logic has already updated the IVAs from this adapter to contain useful values.
        /// </summary>
        void TransferValuesFromIVAs();

        /// <summary>
        /// Transfers the curreent values from the adapter specific set of value endpoints to the VC values in the corresponding set of IVAs (IVAArray).
        /// This method simply assigns the locally contained values for the set of affected IVAs.  It does not make any effort to propagate these values to any corresponding <see cref="IValuesInterconnection"/>
        /// table entry(s).
        /// </summary>
        void TransferValuesToIVAs();
    }

    /// <summary>
    /// Objects support this interface if they allow an external entity to get and set their IVI property
    /// </summary>
    internal interface IGetSetIVI
    {
        IValuesInterconnection IVI { get; set; }
    }

    /// <summary>
    /// This adapter group class provides a client with an object that can be used to efficiently manage operations on sets of ValueSetAdapaters including
    /// group wide Setup, IssueEmitter and ValueNoteEmitter assignment, OptimizeSets assignement, Set, IsUpdateNeeded and Update.
    /// The primary purpose of this class is to support generation of a single array of IValueAccessor objects as the agregate of the set of such arrays from each of the ValueSetAdapterBase instances in this group.
    /// This allows the group to be used to perform IVI updates to or from the group as a single atomic set and optimizes the number of IVI round trip calls by using the larger array of all of the IValueAccessors all at once.
    /// <para/>Please note that the expected semantics of this use of this group are intentionally ambigious.  
    /// This object is intended to support use with externally provided ValueSetAdapterBase objects either before they have been Setup, or after they have been setup.
    /// This object may also be used to perform common setup steps to them by assigning this objects corresponding IValueSetAdapter property setters, each of which applies to all of the ValueSetAdapterBase instances in the group.
    /// </summary>
    public class ValueSetAdapterGroup : IModularValueSetAdapter
    {
        /// <summary>
        /// Constructor.  May be used with collection initializer (via implicit use of Add and AddRange methods)
        /// </summary>
        public ValueSetAdapterGroup() { }

        /// <summary>
        /// Adds the given IModularValueSetAdapter object to this group.
        /// </summary>
        public ValueSetAdapterGroup Add(IModularValueSetAdapter imvsa)
        {
            imvsaList.Add(imvsa);
            rebuildNeeded = true;
            return this;
        }

        /// <summary>
        /// Adds the given list of IModularValueSetAdapter items to this group.
        /// </summary>
        public ValueSetAdapterGroup AddItems(params IModularValueSetAdapter [] imvsaItemArray)
        {
            imvsaList.AddRange(imvsaItemArray);
            rebuildNeeded = true;
            return this;
        }

        /// <summary>
        /// Adds the given enumerable set of IModularValueSetAdapter objects to this group
        /// </summary>
        public ValueSetAdapterGroup AddRange(IEnumerable<IModularValueSetAdapter> imvsaSet)
        {
            imvsaList.AddRange(imvsaSet);
            rebuildNeeded = true;
            return this;
        }

        /// <summary>
        /// Adds the given IValueAccessor object to this group.
        /// </summary>
        public ValueSetAdapterGroup Add(IValueAccessor iva)
        {
            extraIVAList.Add(iva);
            rebuildNeeded = true;
            return this;
        }

        /// <summary>
        /// Adds the given list of IValueAccessor items to this group.
        /// </summary>
        public ValueSetAdapterGroup AddItems(params IValueAccessor [] ivaItemArray)
        {
            extraIVAList.AddRange(ivaItemArray);
            rebuildNeeded = true;
            return this;
        }

        /// <summary>
        /// Adds the given set of IValueAccessor items to this group.
        /// </summary>
        public ValueSetAdapterGroup AddRange(IEnumerable<IValueAccessor> ivaSet)
        {
            extraIVAList.AddRange(ivaSet);
            rebuildNeeded = true;
            return this;
        }

        List<IModularValueSetAdapter> imvsaList = new List<IModularValueSetAdapter>();
        List<IValueAccessor> extraIVAList = new List<IValueAccessor>();

        IModularValueSetAdapter[] imvsaArray = new IModularValueSetAdapter[0];
        bool rebuildNeeded = false;
        IValueAccessor [] ivaArray = null;
        int ivaArrayLength = 0;

        private void RebuildArraysIfNeeded()
        {
            if (!rebuildNeeded)
                return;

            imvsaArray = imvsaList.ToArray();
            ivaArray = imvsaArray.SelectMany(ivsa => ivsa.IVAArray).Concat(extraIVAList).ToArray();
            ivaArrayLength = ivaArray.Length;

            rebuildNeeded = false;
        }

        private Logging.IMesgEmitter issueEmitter = null, valueNotEmitter = null;
        private bool optimizeSets = true;

        /// <summary>
        /// Gives the caller get/set access to the IValuesInterconnection instance that will be used by all of the ValueSetAdapterBase instances in this group.
        /// This may also be implicitly assigned using the appropriate version of the Setup method
        /// </summary>
        public IValuesInterconnection IVI 
        {
            get { return _ivi; }
            set 
            { 
                _ivi = value ?? Values.Instance;
                foreach (var imvsa in imvsaList)
                {
                    IGetSetIVI igsivi = imvsa as IGetSetIVI;
                    if (igsivi != null)
                        igsivi.IVI = _ivi;
                }
            } 
        }
        private IValuesInterconnection _ivi = null;

        /// <summary>
        /// Returns an array of the ValueSetAdapterBase instances that have been added to this group, as an array of IValueSetAdapters
        /// </summary>
        public IValueSetAdapter[] ToArray() { RebuildArraysIfNeeded(); return imvsaArray; }

        /// <summary>
        /// Gives caller access to the set of ModularValueSetAdapters that have been added to this set.
        /// </summary>
        public IEnumerable<IModularValueSetAdapter> ModularValueSetAdapterSet { get { RebuildArraysIfNeeded(); return imvsaArray; } }

        /// <summary>Defines the emitter used to emit Setup, Set, and Update related errors.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter IssueEmitter  { get { return issueEmitter; } set { issueEmitter = value; imvsaList.DoForEach(imvsa => imvsa.IssueEmitter = value); } }

        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter ValueNoteEmitter { get { return valueNotEmitter; } set { valueNotEmitter = value; imvsaList.DoForEach(imvsa => imvsa.ValueNoteEmitter = value); } }

        /// <summary>Allows the client to specify the default meta data to use with each IValueAccessor obtained during Setup.  Used when this is neither null nor empty.</summary>
        public INamedValueSet DefaultMetaData { get { return _DefaultMetaData; } set { _DefaultMetaData = value.ConvertToReadOnly(); imvsaList.DoForEach(imvsa => imvsa.DefaultMetaData = _DefaultMetaData); } }
        private INamedValueSet _DefaultMetaData = NamedValueSet.Empty;

        /// <summary>Allows the client to specify the default meta data merge behavior to use with each IValueAccessor obtained during Setup.  Defaults to AddNewItems</summary>
        public NamedValueMergeBehavior DefaultMetaDataMergeBehavior { get { return defaultMetaDataMergeBehavior; } set { defaultMetaDataMergeBehavior = value; imvsaList.DoForEach(imvsa => imvsa.DefaultMetaDataMergeBehavior = value); } }
        private NamedValueMergeBehavior defaultMetaDataMergeBehavior = NamedValueMergeBehavior.AddNewItems;

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Will use previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public IValueSetAdapter Setup(params string[] baseNames)
        {
            return Setup(null, baseNames);
        }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>If a non-null valueInterconnect instance is given then it will be used otherwise this method will use any previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="ivi">Allows the caller to (re)specifiy the IValuesInterconnection instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public IValueSetAdapter Setup(IValuesInterconnection ivi, params string[] baseNames)
        {
            if (IVI == null || ivi != null)
                IVI = ivi;

            foreach (var vsab in imvsaList)
                vsab.Setup(IVI, baseNames);

            RebuildArraysIfNeeded();

            return this;
        }

        /// <summary>
        /// Transfer the values from the ValueSet's annotated members to the corresponding set of IValueAccessors and then tell the IValuesInterconnection instance
        /// to Set all of the IValueAccessors.
        /// <para/>Supports call chaining.
        /// </summary>
        public IValueSetAdapter Set()
        {
            RebuildArraysIfNeeded();

            InnerTransferValuesToIVAs();

            IVI.Set(ivaArray, numEntriesToSet: ivaArrayLength, optimize: OptimizeSets);

            return this;
        }

        /// <summary>
        /// This property determines if the Set method uses ValueContainer equality testing to determine which IValueAccessor objects to actually write to the table.
        /// When this property is true (the default), equality testing will be used to prevent updating table entires for IValueAccessors that do not have a set pending (due to change in container value).
        /// When this property is false, all value table entries will be Set, without regard to whether their value might have changed.
        /// </summary>
        public bool OptimizeSets { get { return optimizeSets; } set { optimizeSets = value; foreach (var vsab in imvsaList) vsab.OptimizeSets = value; } }

        /// <summary>
        /// Returns true if any of the IValueAccessors to which this adapter is attached indicate that there is a pending update 
        /// (because the accessed value has been set elsewhere so there may be a new value to update that accessor from).
        /// </summary>
        public bool IsUpdateNeeded
        {
            get
            {
                RebuildArraysIfNeeded(); 

                foreach (IValueAccessor iva in ivaArray)
                {
                    if (iva.IsUpdateNeeded)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Requests the IValuesInterconnection instance to update all of the adapter's IValueAccessor objects and then transfers the updated values
        /// from those accessor objects to the corresponding annotated ValueSet members.
        /// <para/>Supports call chaining.
        /// </summary>
        public IValueSetAdapter Update()
        {
            RebuildArraysIfNeeded();

            IVI.Update(ivaArray, numEntriesToUpdate: ivaArrayLength);

            InnerTransferValuesFromIVAs();

            return this;
        }

        /// <summary>
        /// Gives the caller access to the set of IValueAccessors that have been created by this adatper and which are used to interact with the corresponding IVI
        /// <para/>This array will be empty until Setup as been successfully invoked.
        /// </summary>
        public IValueAccessor[] IVAArray
        {
            get { RebuildArraysIfNeeded(); return ivaArray; }
        }

        /// <summary>
        /// Gives the caller access to the number of items (IVAs) that this adapter is using.
        /// </summary>
        public int NumItems
        {
            get { RebuildArraysIfNeeded(); return imvsaArray.Sum(ivsa => ivsa.NumItems); }
        }

        /// <summary>
        /// Transfers the current values from the associated set of IVAs (<seealso cref="IVAArray"/>) to the adapter specific set of value endpoints.
        /// This method expects/requires that some other logic has already updated the IVAs from this adapter to contain useful values.
        /// </summary>
        public void TransferValuesFromIVAs()
        {
            RebuildArraysIfNeeded();

            InnerTransferValuesFromIVAs();
        }

        /// <summary>
        /// Transfers the curreent values from the adapter specific set of value endpoints to the VC values in the corresponding set of IVAs (<seealso cref="IVAArray"/>).
        /// This method simply assigns the locally contained values for the set of affected IVAs.  It does not make any effort to propagate these values to any corresponding <seealso cref="IValuesInterconnection"/>
        /// table entry(s).
        /// </summary>
        public void TransferValuesToIVAs()
        {
            RebuildArraysIfNeeded();

            InnerTransferValuesToIVAs();
        }

        private void InnerTransferValuesFromIVAs()
        {
            foreach (var imvsa in imvsaArray)
                imvsa.TransferValuesFromIVAs();
        }

        private void InnerTransferValuesToIVAs()
        {
            foreach (var imvsa in imvsaArray)
                imvsa.TransferValuesToIVAs();
        }
    }

    /// <summary>
    /// This adapter class provides a client with a ValueSet style tool that supports getting and setting sets of values in a IValuesInterconnection instance.
    /// </summary>
    /// <typeparam name="TValueSet">
    /// Specifies the class type on which this adapter will operate.  
    /// Adapter harvests the list of <see cref="MosaicLib.Modular.Interconnect.Values.Attributes.ValueSetItemAttribute"/> annotated public member items from this type.
    /// </typeparam>
    /// <remarks>
    /// The primary methods/properties used on this adapter are: Construction, ValueSet, Setup, Set, Update, IsUpdateNeeded
    /// </remarks>
    public class ValueSetAdapter<TValueSet> 
        : ValueSetAdapter<TValueSet, Attributes.ValueSetItemAttribute>
        where TValueSet : class
    {
        /// <summary>
        /// Config instance constructor.  Assigns adapter to use given IValuesInterconnection valueInterconnect service instance.  This may be overridden during the Setup call.
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public ValueSetAdapter(IValuesInterconnection ivi = null, ItemSelection itemSelection = (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems))
            : base(ivi: ivi, itemSelection: itemSelection)
        { }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Will use previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public new ValueSetAdapter<TValueSet> Setup(params string[] baseNames)
        {
            Setup(ivi: null, baseNames: baseNames);

            return this;
        }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>If a non-null valueInterconnect instance is given then it will be used otherwise this method will use any previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="ivi">Allows the caller to (re)specifiy the IValuesInterconnection instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public new ValueSetAdapter<TValueSet> Setup(IValuesInterconnection ivi, params string[] baseNames)
        {
            base.Setup(ivi: ivi, baseNames: baseNames);

            return this;
        }

        /// <summary>
        /// Transfer the values from the ValueSet's annotated members to the corresponding set of IValueAccessors and then tell the IValuesInterconnection instance
        /// to Set all of the IValueAccessors.
        /// <para/>Supports call chaining.
        /// </summary>
        public new ValueSetAdapter<TValueSet> Set()
        {
            base.Set();

            return this;
        }

        /// <summary>
        /// Requests the IValuesInterconnection instance to update all of the adapter's IValueAccessor objects and then transfers the updated values
        /// from those accessor objects to the corresponding annotated ValueSet members.
        /// <para/>Supports call chaining.
        /// </summary>
        public new ValueSetAdapter<TValueSet> Update()
        {
            base.Update();

            return this;
        }
    }

    /// <summary>
    /// This adapter class provides a client with a ValueSet style tool that supports getting and setting sets of values in a IValuesInterconnection instance.
    /// </summary>
    /// <typeparam name="TValueSet">
    /// Specifies the class type on which this adapter will operate.  
    /// Adapter harvests the list of <see cref="MosaicLib.Modular.Interconnect.Values.Attributes.ValueSetItemAttribute"/> annotated public member items from this type.
    /// </typeparam>
    /// <typeparam name="TAttribute">
    /// Allows the client to customize this adapter to make use of any <seealso cref="Attributes.ValueSetItemAttribute"/> derived attribute type.
    /// This is intended to allow the client to make use of multiple custom attribute types in order to customize which adapter any given annotated item in a value set class the item is itended to be used with.
    /// </typeparam>
    /// <remarks>
    /// The primary methods/properties used on this adapter are: Construction, ValueSet, Setup, Set, Update, IsUpdateNeeded
    /// </remarks>
    public class ValueSetAdapter<TValueSet, TAttribute> 
        : ValueSetAdapterBase<TAttribute>
        where TValueSet : class
        where TAttribute : Attributes.ValueSetItemAttribute, new()
    {
        #region Constructors

        /// <summary>
        /// Config instance constructor.  Assigns adapter to use given IValuesInterconnection valueInterconnect service instance.  This may be overridden during the Setup call.
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public ValueSetAdapter(IValuesInterconnection ivi = null, ItemSelection itemSelection = (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems | ItemSelection.UseStrictAttributeTypeChecking))
            : base(typeof(TValueSet), ivi: ivi, itemSelection: itemSelection)
        {
            itemAccessSetupInfoArray = new ItemAccessSetupInfo<TValueSet, TAttribute>[NumItems];
        }

        #endregion

        #region IValueSetAdapter and IValueSetAdapter<TValueSet> implementation methods

        /// <summary>
        /// Contains the ValueSet object that is used as the value source for Set calls and receives updated values during Update.
        /// </summary>
        public TValueSet ValueSet { get; set; }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Will use previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public new ValueSetAdapter<TValueSet, TAttribute> Setup(params string[] baseNames)
        {
            base.Setup(baseNames);

            return this;
        }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>If a non-null valueInterconnect instance is given then it will be used otherwise this method will use any previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="ivi">Allows the caller to (re)specifiy the IValuesInterconnection instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public new ValueSetAdapter<TValueSet, TAttribute> Setup(IValuesInterconnection ivi, params string[] baseNames)
        {
            base.Setup(ivi, baseNames);

            return this;
        }

        /// <summary>
        /// Sets this objects ValueSet to the given valueSet and then calls the parameterless Set method.
        /// <para/>Supports call chaining.
        /// </summary>
        public ValueSetAdapter<TValueSet, TAttribute> Set(TValueSet valueSet)
        {
            ValueSet = valueSet;

            return Set();
        }

        /// <summary>
        /// Transfer the values from the ValueSet's annotated members to the corresponding set of IValueAccessors and then tell the IValuesInterconnection instance
        /// to Set all of the IValueAccessors.
        /// <para/>Supports call chaining.
        /// </summary>
        public new ValueSetAdapter<TValueSet, TAttribute> Set()
        {
            base.Set();

            return this;
        }

        /// <summary>
        /// Requests the IValuesInterconnection instance to update all of the adapter's IValueAccessor objects and then transfers the updated values
        /// from those accessor objects to the corresponding annotated ValueSet members.
        /// <para/>Supports call chaining.
        /// </summary>
        public new ValueSetAdapter<TValueSet, TAttribute> Update()
        {
            base.Update();

            return this;
        }

        #endregion

        #region IModularValueSetAdapter implementation methods

        /// <summary>
        /// Transfers the current values from the associated set of IVAs (IVAArray) to the adapter specific set of value endpoints.
        /// This method expects/requires that some other logic has already updated the IVAs from this adapter to contain useful values.
        /// </summary>
        public override void TransferValuesFromIVAs()
        {
            foreach (var iasi in itemAccessSetupInfoArray)
            {
                if (iasi != null && iasi.MemberFromIVAAction != null)
                    iasi.MemberFromIVAAction(ValueSet, IssueEmitter, ValueNoteEmitter);
            }
        }

        /// <summary>
        /// Transfers the curreent values from the adapter specific set of value endpoints to the VC values in the corresponding set of IVAs (IVAArray).
        /// This method simply assigns the locally contained values for the set of affected IVAs.  It does not make any effort to propagate these values to any corresponding <see cref="IValuesInterconnection"/>
        /// table entry(s).
        /// </summary>
        public override void TransferValuesToIVAs()
        {
            foreach (var iasi in itemAccessSetupInfoArray)
            {
                if (iasi != null && iasi.MemberToIVAAction != null)
                    iasi.MemberToIVAAction(ValueSet, IssueEmitter, ValueNoteEmitter);
            }
        }

        #endregion

        #region ValueSetAdatperBase implementation methods

        /// <summary>
        /// internal implementation method.  Performs all of the type specific setup (and some non-type specific setup) required to support annotated ValueSet access.
        /// </summary>
        internal override void InnerSetup(params string[] baseNames)
        {
            // setup all of the static information

            for (int idx = 0; idx < NumItems; idx++)
            {
                ItemInfo<TAttribute> itemInfo = valueSetItemInfoList[idx];
                Attributes.ValueSetItemAttribute itemAttribute = itemInfo.ItemAttribute;

                string memberName = itemInfo.MemberInfo.Name;
                string itemName = (!string.IsNullOrEmpty(itemAttribute.Name) ? itemAttribute.Name : itemInfo.MemberInfo.Name);
                string fullValueName = itemInfo.GenerateFullName(baseNames);

                if (!itemInfo.CanSetValue && MustSupportUpdate)
                {
                    if (!itemAttribute.SilenceIssues)
                        IssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: Member must provide public setter to support Update, in ValueSet type '{2}'", memberName, fullValueName, TValueSetTypeStr);

                    continue;
                }

                if (!itemInfo.CanGetValue && MustSupportSet)
                {
                    if (!itemAttribute.SilenceIssues)
                        IssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: Member must provide public getter to support Set, in ValueSet type '{2}'", memberName, fullValueName, TValueSetTypeStr);

                    continue;
                }

                IValueAccessor valueAccessor = IVI.GetValueAccessor(fullValueName, DefaultMetaData, DefaultMetaDataMergeBehavior);

                ContainerStorageType useStorageType;
                bool isNullable = false;
                ValueContainer.DecodeType(itemInfo.ItemType, out useStorageType, out isNullable);
                if (!itemAttribute.StorageType.IsNone())
                    useStorageType = itemAttribute.StorageType;

                ItemAccessSetupInfo<TValueSet, TAttribute> itemAccessSetupInfo = new ItemAccessSetupInfo<TValueSet, TAttribute>()
                {
                    IVA = valueAccessor,
                    ItemInfo = itemInfo,
                    UseStorageType = useStorageType,
                    ItemIsValueContainer = (itemInfo.ItemType == typeof(ValueContainer)),
                };

                itemAccessSetupInfo.GenerateMemberToFromValueAccessFuncs(ItemAccess);

                Logging.IMesgEmitter selectedIssueEmitter = IssueEmitter;

                if (itemAccessSetupInfo.MemberToIVAAction == null)
                {
                    if (MustSupportSet && ItemAccess.UseGetter() && itemInfo.CanGetValue && !itemAttribute.SilenceIssues)
                        selectedIssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: no valid getter delegate could be generated for its ValueSet type:'{3}'", memberName, fullValueName, itemInfo.ItemType, TValueSetTypeStr);
                }

                if (itemAccessSetupInfo.MemberFromIVAAction == null)
                {
                    if (MustSupportUpdate && ItemAccess.UseSetter() && itemInfo.CanSetValue && !itemAttribute.SilenceIssues)
                        selectedIssueEmitter.Emit("Member/Value '{0}'/'{1}' is not usable: no valid setter delegate could be generated for its ValueSet type:'{3}'", memberName, fullValueName, itemInfo.ItemType, TValueSetTypeStr);
                }

                itemAccessSetupInfoArray[idx] = itemAccessSetupInfo;
                IVAArray[idx] = valueAccessor;
            }
        }

        internal override bool IsValueSetValid { get { return (ValueSet != null); } }

        #endregion

        #region itemAccessSetupInfoArray

        /// <remarks>Non-null elements in this array correspond to fully vetted gettable and settable ValueSet items.</remarks>
        ItemAccessSetupInfo<TValueSet, TAttribute>[] itemAccessSetupInfoArray = null;

        #endregion
    }

    /// <summary>
    /// Base class for generaic ValueSetAdapter class used to support common pattern enforcement and to allow use of ValueSetAdapterGroup which applies to a set of ValueSetAdatperBase derived objects.
    /// All of the non-TValueSet type implementation and portions of the TValueSet type specific implementation are implemented at this level.
    /// </summary>
    public abstract class ValueSetAdapterBase 
        : ValueSetAdapterBase<Attributes.ValueSetItemAttribute>
    {
        protected ValueSetAdapterBase(Type tValueSetType, IValuesInterconnection ivi = null, ItemSelection itemSelection = (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems))
            : base(tValueSetType, ivi, itemSelection: itemSelection)
        { }
    }

    /// <summary>
    /// Base class for generaic ValueSetAdapter class used to support common pattern enforcement and to allow use of ValueSetAdapterGroup which applies to a set of ValueSetAdatperBase derived objects.
    /// All of the non-TValueSet type implementation and portions of the TValueSet type specific implementation are implemented at this level.
    /// </summary>
    public abstract class ValueSetAdapterBase<TAttribute>
        : IModularValueSetAdapter, IGetSetIVI
        where TAttribute: Attributes.ValueSetItemAttribute, new()
    {
        #region Construction

        /// <summary>
        /// If the given value of ivi is non-null, sets the IVI to it
        /// </summary>
        protected ValueSetAdapterBase(Type tValueSetType, IValuesInterconnection ivi = null, ItemSelection itemSelection = (ItemSelection.IncludeExplicitPublicItems | ItemSelection.IncludeInheritedItems | ItemSelection.UseStrictAttributeTypeChecking))
        {
            if (ivi != null)
                IVI = ivi;

            DefaultMetaDataMergeBehavior = NamedValueMergeBehavior.AddNewItems;

            MustSupportSet = true;
            MustSupportUpdate = true;

            TValueSetType = tValueSetType;
            TValueSetTypeStr = TValueSetType.Name;

            valueSetItemInfoList = AnnotatedClassItemAccessHelper<TAttribute>.ExtractItemInfoAccessListFrom(tValueSetType, itemSelection);
            NumItems = valueSetItemInfoList.Count;

            IVAArray = new IValueAccessor[NumItems];

            OptimizeSets = true;
        }

        #endregion

        #region TValueSetType, TValueSetTypeStr

        public Type TValueSetType { get; private set; }
        public string TValueSetTypeStr { get; private set; }

        #endregion

        #region internal IGetSetIVI implementation

        /// <summary>
        /// IVI is now settable by "friend" classes to support group wide assignment
        /// </summary>
        public IValuesInterconnection IVI { get { return _ivi; } set { _ivi = value ?? Values.Instance; } }        // delay making default ConfigInstance assignment until Setup method.
        private IValuesInterconnection _ivi;

        #endregion

        #region IValueSetAdapter implementation

        /// <summary>Defines the emitter used to emit Setup, Set, and Update related errors.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter IssueEmitter { get { return FixupEmitterRef(ref issueEmitter); } set { issueEmitter = value; } }

        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter ValueNoteEmitter { get { return FixupEmitterRef(ref valueNoteEmitter); } set { valueNoteEmitter = value; } }

        /// <summary>Allows the client to specify the default meta data to use with each IValueAccessor obtained during Setup.  Used when this is neither null nor empty.</summary>
        public INamedValueSet DefaultMetaData { get { return _DefaultMetaData; } set { _DefaultMetaData = value.ConvertToReadOnly(); } }
        private INamedValueSet _DefaultMetaData = NamedValueSet.Empty;

        /// <summary>Allows the client to specify the default meta data merge behavior to use with each IValueAccessor obtained during Setup.  Defaults to AddNewItems</summary>
        public NamedValueMergeBehavior DefaultMetaDataMergeBehavior { get; set; }

        /// <summary>When true (the default), TValueSet class is expected to provide public getters for all annotated properties</summary>
        public bool MustSupportSet { get; set; }

        /// <summary>When true (the default), TValueSet class is expected to provide public setters for all annotated properties</summary>
        public bool MustSupportUpdate { get; set; }

        /// <summary>This property helps define the set of behaviors that this adapter shall perform.  It defaults to ItemAccess.Normal (Get and Set).  Setting it to any other value will also clear the corresponding MustSupport flag(s)</summary>
        public ItemAccess ItemAccess
        {
            get { return _itemAccess; }
            set
            {
                _itemAccess = value;
                if (!_itemAccess.IsSet(ItemAccess.UseGetterIfPresent))
                    MustSupportSet = false;
                if (!_itemAccess.IsSet(ItemAccess.UseSetterIfPresent))
                    MustSupportUpdate = false;
            }
        }
        private ItemAccess _itemAccess = ItemAccess.Normal;


        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>Will use previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public IValueSetAdapter Setup(params string[] baseNames)
        {
            return Setup(null, baseNames);
        }

        /// <summary>
        /// This method determines the set of full Parameter Names from the ValueSet's annotated items, and creates a set of IValueAccessor objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the ValueSet.
        /// <para/>If a non-null valueInterconnect instance is given then it will be used otherwise this method will use any previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="ivi">Allows the caller to (re)specifiy the IValuesInterconnection instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public IValueSetAdapter Setup(IValuesInterconnection ivi, params string[] baseNames)
        {
            if (ivi != null || IVI == null)
                IVI = ivi;

            if (!IsValueSetValid)
                throw new System.NullReferenceException("ValueSet property must be Valid (non-null) before Setup can be called");

            InnerSetup(baseNames);

            return this;
        }

        /// <summary>
        /// Transfer the values from the ValueSet's annotated members to the corresponding set of IValueAccessors and then tell the IValuesInterconnection instance
        /// to Set all of the IValueAccessors.
        /// <para/>Supports call chaining.
        /// </summary>
        public IValueSetAdapter Set()
        {
            if (!IsValueSetValid)
            {
                IssueEmitter.Emit("ValueSet property must be Valid (non-null) before Set can be called");
                return this;
            }

            TransferValuesToIVAs();

            IVI.Set(IVAArray, NumItems, optimize: OptimizeSets);

            return this;
        }

        /// <summary>
        /// This property determines if the Set method uses ValueContainer equality testing to determine which IValueAccessor objects to actually write to the table.
        /// When this property is true (the default), equality testing will be used to prevent updating table entires for IValueAccessors that do not have a set pending (due to change in container value).
        /// When this property is false, all value table entries will be Set, without regard to whether their value might have changed.
        /// </summary>
        public bool OptimizeSets { get { return optimizeSets; } set { optimizeSets = value; } }
        protected bool optimizeSets = false;

        /// <summary>
        /// Returns true if any of the IValueAccessors to which this adapter is attached indicate that there is a pending update 
        /// (because the accessed value has been set elsewhere so there may be a new value to update that accessor from).
        /// </summary>
        public bool IsUpdateNeeded { get { return IVAArray.IsUpdateNeeded(); } }

        /// <summary>
        /// Requests the IValuesInterconnection instance to update all of the adapter's IValueAccessor objects and then transfers the updated values
        /// from those accessor objects to the corresponding annotated ValueSet members.
        /// <para/>Supports call chaining.
        /// </summary>
        public IValueSetAdapter Update()
        {
            if (!IsValueSetValid)
                throw new System.NullReferenceException("ValueSet property must be Valid (non-null) before Update can be called");

            IVI.Update(IVAArray, numEntriesToUpdate: NumItems);

            TransferValuesFromIVAs();

            return this;
        }

        /// <summary>
        /// Gives the caller access to the set of IValueAccessors that have been created by this adatper and which are used to interact with the corresponding IVI
        /// <para/>This array will be empty until Setup as been successfully invoked.
        /// </summary>
        public IValueAccessor[] IVAArray { get; protected set; }

        /// <summary>
        /// Gives the caller access to the number of items (IVAs) that this adapter is using.
        /// </summary>
        public int NumItems { get; private set; }

        #endregion

        #region IModularIValueSetAdapter implementation (sort of).

        public abstract void TransferValuesFromIVAs();
        public abstract void TransferValuesToIVAs();

        #endregion

        #region abstract methods to be implemented by a derived class

        internal abstract void InnerSetup(params string[] baseNames);

        internal abstract bool IsValueSetValid { get; }

        #endregion

        #region protected implememtation definitions and methods

        protected List<ItemInfo<TAttribute>> valueSetItemInfoList = null;       // gets built by the AnnotatedClassItemAccessHelper.

        /// <summary>
        /// Internal class used to capture the key specific setup information for a given annotated property in the ValueSet.
        /// </summary>
        protected class ItemAccessSetupInfo<TValueSet, TItemAttribute>
            where TItemAttribute : Attributes.ValueSetItemAttribute, new()
        {
            /// <summary>The value's corresponding IValueAccessor object.</summary>
            public IValueAccessor IVA { get; set; }

            /// <summary>
            /// Retains access to the ItemInfo for the corresponding item in the value set
            /// </summary>
            public ItemInfo<TItemAttribute> ItemInfo { get; set; }

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
            public string ValueName { get { return ((IVA != null) ? IVA.Name : String.Empty); } }

            /// <summary>
            /// Gives the storage type that will be used with any container associated with this item
            /// </summary>
            public ContainerStorageType UseStorageType { get; set; }

            /// <summary>
            /// Returns true if the Item data storage type is a ValueContainer (ie it is to receive exactly what is in the IVA without conversion)
            /// </summary>
            public bool ItemIsValueContainer { get; set; }

            /// <summary>
            /// Carries the ValueSeqNum at the end of the last inbound or outbound transfer.  Allows Update to skip transfering values repeatedly.
            /// </summary>
            public UInt32 LastTransferedValueSeqNum { get; set; }

            /// <summary>delegate that is used to set a specific member's value from a given IValueAccessor's value.</summary>
            /// <remarks>this item will be null for static items and for items that failed to be setup correctly.</remarks>
            public Action<TValueSet, Logging.IMesgEmitter, Logging.IMesgEmitter> MemberToIVAAction { get; set; }

            /// <summary>delegate that is used to set a specific member's value from a given IValueAccessor's value.</summary>
            /// <remarks>this item will be null for static items and for items that failed to be setup correctly.</remarks>
            public Action<TValueSet, Logging.IMesgEmitter, Logging.IMesgEmitter> MemberFromIVAAction { get; set; }

            /// <summary>
            /// Generates the MemberToValueAccessAction and the MemberFromValueAccessAction for this ItemAccessSetupInfo instance.
            /// </summary>
            public void GenerateMemberToFromValueAccessFuncs(ItemAccess adapterItemAccess)
            {
                ItemInfo<TItemAttribute> itemInfo = ItemInfo;
                ContainerStorageType useStorageType = UseStorageType;

                AnnotatedClassItemAccessHelper.GetMemberAsVCFunctionDelegate<TValueSet> getMemberAsVCFunction = (adapterItemAccess.UseGetter() ? itemInfo.GenerateGetMemberToVCFunc<TValueSet>() : null);
                AnnotatedClassItemAccessHelper.SetMemberFromVCActionDelegate<TValueSet> setMemberFromVCFunction = (adapterItemAccess.UseSetter() ? itemInfo.GenerateSetMemberFromVCAction<TValueSet>(forceRethrowFlag: false) : null);
                
                string TValueSetTypeStr = typeof(TValueSet).Name;
                bool silenceIssues = ((itemInfo.IAnnotatedItemAttribute != null) && itemInfo.IAnnotatedItemAttribute.SilenceIssues);

                if (getMemberAsVCFunction != null)
                {
                    MemberToIVAAction = delegate(TValueSet valueSetObj, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter)
                    {
                        try
                        {
                            // rethrow is only enabled if the item's issues have not been silenced and if we have a active issue emitter to use.
                            bool rethrow = !silenceIssues && updateIssueEmitter != null && updateIssueEmitter.IsEnabled;

                            IVA.VC = getMemberAsVCFunction(valueSetObj, null, null, rethrow: rethrow);
                            LastTransferedValueSeqNum = 0;      // trigger that next update needs to retransfer the value.

                            if (IVA.IsSetPending && valueUpdateEmitter != null && valueUpdateEmitter.IsEnabled)
                                valueUpdateEmitter.Emit("Member:'{0}' transfered to Name:'{1}' value:'{2}' [type:'{3}']", MemberName, ValueName, IVA.VC, TValueSetTypeStr);
                        }
                        catch (System.Exception ex)
                        {
                            if (!itemInfo.ItemAttribute.SilenceIssues && updateIssueEmitter != null)
                                updateIssueEmitter.Emit("Member'{0}' tranfer to Name:'{1}' in type '{2}' could not be performed: {3}", MemberName, ValueName, TValueSetTypeStr, ex);
                        }
                    };
                }

                if (setMemberFromVCFunction != null)
                {
                    MemberFromIVAAction = delegate(TValueSet valueSetObj, Logging.IMesgEmitter updateIssueEmitter, Logging.IMesgEmitter valueUpdateEmitter)
                    {
                        try
                        {
                            if (!OptimizeUpdates || LastTransferedValueSeqNum != IVA.ValueSeqNum)
                            {
                                // rethrow is only enabled if the item's issues have not been silenced and if we have a active issue emitter to use.
                                bool rethrow = !silenceIssues && updateIssueEmitter != null && updateIssueEmitter.IsEnabled;

                                setMemberFromVCFunction(valueSetObj, IVA.VC, null, null, rethrow);
                                LastTransferedValueSeqNum = IVA.ValueSeqNum;
                            }

                            if (valueUpdateEmitter != null && valueUpdateEmitter.IsEnabled)
                                valueUpdateEmitter.Emit("Member:'{0}' transfered from Name:'{1}' value:'{2}' [type:'{3}']", MemberName, ValueName, IVA.VC, TValueSetTypeStr);
                        }
                        catch (System.Exception ex)
                        {
                            if (!itemInfo.ItemAttribute.SilenceIssues && updateIssueEmitter != null)
                                updateIssueEmitter.Emit("Member:'{0}' transfer from Name:'{1}' in type '{2}' could not be performed: {3}", MemberName, ValueName, TValueSetTypeStr, ex);
                        }
                    };
                }
            }

            private static readonly object[] emptyObjectArray = new object[0];

            /// <summary>
            /// Flag used to determine if Update operations are optimized for this item
            /// <para/>defaults to false
            /// </summary>
            public bool OptimizeUpdates { get; set; }
        }

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

    #region DelegateValueSetAdapter

    /// <summary>
    /// This is a type of IValueSetAdapter that is to connect a set of IValueAccessors to a given set of Gettable and/or Settable DelegateItemSpecs
    /// </summary>
    public class DelegateValueSetAdapter 
        : IModularValueSetAdapter, IGetSetIVI
    {
        #region Constructors

        /// <summary>
        /// Basic constructor.  Assigns adapter to use given IValuesInterconnection valueInterconnect service instance.  This may be overridden during the Setup call.
        /// For use with Property Initializers and the Setup method to define and setup the adapter instance for use.
        /// <para/>Please Note: the Setup method must be called before the adapter can be used.  
        /// </summary>
        public DelegateValueSetAdapter(IValuesInterconnection ivi = null)
        {
            if (ivi != null)
                IVI = ivi;

            DefaultMetaDataMergeBehavior = NamedValueMergeBehavior.AddNewItems;
        }

        #endregion

        #region internal IGetSetIVI implementation

        /// <summary>
        /// IVI is now settable by "friend" classes to support group wide assignment
        /// </summary>
        public IValuesInterconnection IVI { get { return _ivi; } set { _ivi = value ?? Values.Instance; } }        // delay making default ConfigInstance assignment until Setup method.
        private IValuesInterconnection _ivi;

        #endregion

        #region delegate set building

        /// <summary>
        /// This method is used to add an DelegateItemSpec to the adapter.  These calls must all be completed before Setup is called.
        /// </summary>
        public DelegateValueSetAdapter Add<TValueSet>(DelegateItemSpec<TValueSet> itemSpec)
        {
            addedItemList.Add(new DelegateIVAItem<TValueSet>(itemSpec));

            NumItems = addedItemList.Count;

            return this;
        }

        List<IDelegateIVAItem> addedItemList = new List<IDelegateIVAItem>();

        #endregion

        #region IValueSetAdapter implementation

        /// <summary>Defines the emitter used to emit Setup, Set, and Update related errors.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter IssueEmitter { get { return FixupEmitterRef(ref issueEmitter); } set { issueEmitter = value; } }

        /// <summary>Defines the emitter used to emit Update related changes in config point values.  Defaults to the null emitter.</summary>
        public Logging.IMesgEmitter ValueNoteEmitter { get { return FixupEmitterRef(ref valueNoteEmitter); } set { valueNoteEmitter = value; } }

        /// <summary>Allows the client to specify the default meta data to use with each IValueAccessor obtained during Setup.</summary>
        public INamedValueSet DefaultMetaData { get { return _DefaultMetaData; } set { _DefaultMetaData = value.ConvertToReadOnly(); } }
        private INamedValueSet _DefaultMetaData = NamedValueSet.Empty;

        /// <summary>Allows the client to specify the default meta data merge behavior to use with each IValueAccessor obtained during Setup.  Default value is AddNewItems</summary>
        public NamedValueMergeBehavior DefaultMetaDataMergeBehavior { get; set; }

        /// <summary>this flag determines if the adapter emits ValueNoteEmitter messages when the transferred value is equal to the last transferred value (defaults to false)(</summary>
        public bool EmitValueNoteNoChangeMessages { get; set; }

        /// <summary>
        /// This method determines the set of full IVA Names from the added DelegateItemSpec items, and creates a set of IVA objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the created IVAs.
        /// <para/>Will use previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public IValueSetAdapter Setup(params string[] baseNames)
        {
            return Setup(null, baseNames);
        }

        /// <summary>
        /// This method determines the set of full IVA Names from the added DelegateItemSpec items, and creates a set of IVA objects for them.
        /// In most cases the client will immediately call Set or Update to transfer the values from or to the created IVAs.
        /// <para/>If a non-null ivi instance is given then it will be used otherwise this method will use any previously defined IValuesInterconnection instance or the Values.Instance singleton.
        /// <para/>Supports call chaining.
        /// </summary>
        /// <param name="ivi">Allows the caller to (re)specifiy the IValuesInterconnection instance that is to be used henceforth by this adapter</param>
        /// <param name="baseNames">
        /// Gives a list of 1 or more base names that are prepended to specific sub-sets of the list of item names based on each item's NameAdjust attribute property value.
        /// </param>
        public IValueSetAdapter Setup(IValuesInterconnection ivi, params string[] baseNames)
        {
            if (ivi != null || IVI == null)
                IVI = ivi;

            NumItems = addedItemList.Count;
            foreach (var item in addedItemList)
            {
                item.IssueEmitter = IssueEmitter;
                item.ValueNoteEmitter = ValueNoteEmitter;
                item.EmitValueNoteNoChangeMessages = EmitValueNoteNoChangeMessages;
                item.IVA = IVI.GetValueAccessor(item.NameAdjust.GenerateFullName(item.Name, memberName: null, paramsStrArray: baseNames), DefaultMetaData, DefaultMetaDataMergeBehavior);
            }

            setSpecificDelegateIVAItemArray = addedItemList.Where(item => item.HasValueSetterDelegate).ToArray();
            setSpecificIvaArray = setSpecificDelegateIVAItemArray.Select(item => item.IVA).ToArray();
            setSpecificIvaArrayLength = setSpecificIvaArray.Length;

            updateSpecificDelegateIVAItemArray = addedItemList.Where(item => item.HasValueGetterDelegate).ToArray();
            updateSpecificIvaArray = updateSpecificDelegateIVAItemArray.Select(item => item.IVA).ToArray();
            updateSpecificIvaArrayLength = updateSpecificIvaArray.Length;

            IVAArray = addedItemList.Select(item => item.IVA).Where(iva => iva != null).ToArray();

            return this;
        }

        IDelegateIVAItem[] setSpecificDelegateIVAItemArray;
        IDelegateIVAItem[] updateSpecificDelegateIVAItemArray;

        IValueAccessor[] setSpecificIvaArray;
        IValueAccessor[] updateSpecificIvaArray;
        int setSpecificIvaArrayLength;
        int updateSpecificIvaArrayLength;

        /// <summary>
        /// Transfer the values from the getter delegates to the corresponding set of IValueAccessors and then tell the IValuesInterconnection instance
        /// to Set all of the IValueAccessors.
        /// <para/>Supports call chaining.
        /// </summary>
        public IValueSetAdapter Set()
        {
            TransferValuesToIVAs();

            IVI.Set(setSpecificIvaArray, numEntriesToSet: setSpecificIvaArrayLength, optimize: OptimizeSets);

            return this;
        }

        /// <summary>
        /// This property determines if the Set method uses ValueContainer equality testing to determine which IValueAccessor objects to actually write to the table.
        /// When this property is true (the default), equality testing will be used to prevent updating table entires for IValueAccessors that do not have a set pending (due to change in container value).
        /// When this property is false, all value table entries will be Set, without regard to whether their value might have changed.
        /// </summary>
        public bool OptimizeSets { get { return optimizeSets; } set { optimizeSets = value; } }
        protected bool optimizeSets = true;

        /// <summary>
        /// Returns true if any of the Update specific IValueAccessors to which this adapter is attached indicate IsUpdateNeeded
        /// (because the accessed value has been set elsewhere so there may be a new value to update that accessor from).
        /// </summary>
        public bool IsUpdateNeeded { get { return updateSpecificIvaArray.IsUpdateNeeded(); } }

        /// <summary>
        /// Requests the IValuesInterconnection instance to update all of the adapter's IValueAccessor objects and then transfers the updated values
        /// from those accessor objects to the corresponding setter delegates.
        /// <para/>Supports call chaining.
        /// </summary>
        public IValueSetAdapter Update()
        {
            IVI.Update(updateSpecificIvaArray, numEntriesToUpdate: updateSpecificIvaArrayLength);

            TransferValuesFromIVAs();

            return this;
        }

        /// <summary>
        /// Gives the caller access to the set of IValueAccessors that have been created by this adatper and which are used to interact with the corresponding IVI
        /// <para/>This array will be empty until Setup as been successfully invoked.
        /// </summary>
        public IValueAccessor[] IVAArray { get; protected set; }

        /// <summary>
        /// Gives the caller access to the number of items (IVAs) that this adapter is using.
        /// </summary>
        public int NumItems { get; protected set; }

        #endregion

        #region IModularValueSetAdapter implementation

        /// <summary>
        /// Transfers the current values from the associated set of IVAs (<seealso cref="IVAArray"/>) to the adapter specific set of value endpoints.
        /// This method expects/requires that some other logic has already updated the IVAs from this adapter to contain useful values.
        /// </summary>
        public void TransferValuesFromIVAs()
        {
            foreach (var delegateItem in updateSpecificDelegateIVAItemArray)
                delegateItem.TransferFromIVAValueContainerToDelegate();
        }

        /// <summary>
        /// Transfers the curreent values from the adapter specific set of value endpoints to the VC values in the corresponding set of IVAs (<seealso cref="IVAArray"/>).
        /// This method simply assigns the locally contained values for the set of affected IVAs.  It does not make any effort to propagate these values to any corresponding <seealso cref="IValuesInterconnection"/>
        /// table entry(s).
        /// </summary>
        public void TransferValuesToIVAs()
        {
            foreach (var delegateItem in setSpecificDelegateIVAItemArray)
                delegateItem.TransferFromDelegateToIVAValueContainer();
        }

        #endregion

        #region IDelegateIVAItem and DelegateIVAItem

        public interface IDelegateIVAItem
        {
            string Name { get; }
            NameAdjust NameAdjust { get; }
            bool HasValueSetterDelegate { get; }
            bool HasValueGetterDelegate { get; }
            IValueAccessor IVA { get; set; }
            Logging.IMesgEmitter IssueEmitter { get; set; }
            Logging.IMesgEmitter ValueNoteEmitter { get; set; }
            bool EmitValueNoteNoChangeMessages { get; set; }
            void TransferFromDelegateToIVAValueContainer();
            void TransferFromIVAValueContainerToDelegate();
        }

        protected class DelegateIVAItem<TValueType> : DelegateItemSpec<TValueType>, IDelegateIVAItem
        {
            public DelegateIVAItem(DelegateItemSpec<TValueType> createFrom) 
                : base(createFrom)
            {
                ValueContainer.DecodeType(typeof(TValueType), out decodedCST, out decodedIsNullable);
            }

            private ContainerStorageType decodedCST;
            private bool decodedIsNullable;

            public bool HasValueGetterDelegate { get { return (SetterDelegate != null); } }
            public bool HasValueSetterDelegate { get { return (GetterDelegate != null); } }

            public Logging.IMesgEmitter IssueEmitter { get; set; }
            public Logging.IMesgEmitter ValueNoteEmitter { get; set; }
            public bool EmitValueNoteNoChangeMessages { get; set; }
            public IValueAccessor IVA { get; set; }

            private ValueContainer lastTransferredVC;
            public void TransferFromDelegateToIVAValueContainer()
            {
                try
                {
                    ValueContainer entryVC = lastTransferredVC;

                    IVA.VC = lastTransferredVC.SetValue<TValueType>(GetterDelegate(), decodedCST, decodedIsNullable);

                    if (ValueNoteEmitter != null && ValueNoteEmitter.IsEnabled)
                    {
                        if (!entryVC.Equals(lastTransferredVC))
                            ValueNoteEmitter.Emit("Set IVA '{0}' value to {1} [from {2}]", IVA.Name, lastTransferredVC, entryVC);
                        else if (EmitValueNoteNoChangeMessages)
                            ValueNoteEmitter.Emit("IVA '{0}' value not changed [from {1}]", IVA.Name, entryVC);
                    }
                }
                catch (System.Exception ex)
                {
                    (IssueEmitter ?? Logging.NullEmitter).Emit("{0} failed on IVA {1}: {2}", Fcns.CurrentMethodName, IVA, ex.ToString(ExceptionFormat.TypeAndMessage));
                    IVA.VC = ValueContainer.Empty;
                }
            }

            public void TransferFromIVAValueContainerToDelegate()
            {
                try
                {
                    bool rethrow = (IssueEmitter != null && IssueEmitter.IsEnabled);

                    ValueContainer entryVC = lastTransferredVC;
                    SetterDelegate((lastTransferredVC = IVA.VC).GetValue<TValueType>(decodedCST, decodedIsNullable, rethrow: rethrow));

                    if (ValueNoteEmitter != null && ValueNoteEmitter.IsEnabled)
                    {
                        if (!entryVC.Equals(lastTransferredVC))
                            ValueNoteEmitter.Emit("Set delegate value from IVA '{0}' to {1} [from {2}]", IVA.Name, lastTransferredVC, entryVC);
                        else if (EmitValueNoteNoChangeMessages)
                            ValueNoteEmitter.Emit("Delegate value from IVA '{0}' not changed [from {1}]", IVA.Name, entryVC);
                    }
                }
                catch (System.Exception ex)
                {
                    (IssueEmitter ?? Logging.NullEmitter).Emit("{0} failed on IVA {1}: {2}", Fcns.CurrentMethodName, IVA, ex.ToString(ExceptionFormat.TypeAndMessage));
                }
            }
        }

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

    #region Related Extension Methods: IsUpdatedNeeded and IsSetPending for a IValueAccessor arrays

    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Checks each IVA in the given array and returns true if any such non-null IVA's IsUpdateNeeded flag is set.
        /// Returns false if no such IVA IsUpdateNeeded flag is set, or the array is null or empty.
        /// </summary>
        public static bool IsUpdateNeeded(this IValueAccessor[] ivaArray)
        {
            foreach (IValueAccessor iva in ivaArray ?? emptyIVAArray)
            {
                if (iva != null && iva.IsUpdateNeeded)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks each IVA in the given array and returns true if any such non-null IVA's IsSetPending flag is set.
        /// Returns false if no such IVA IsSetPending flag is set, or the array is null or empty.
        /// </summary>
        public static bool IsSetPending(this IValueAccessor[] ivaArray)
        {
            foreach (IValueAccessor iva in ivaArray ?? emptyIVAArray)
            {
                if (iva != null && iva.IsSetPending)
                    return true;
            }

            return false;
        }

        private static readonly IValueAccessor[] emptyIVAArray = new IValueAccessor[0];

        /// <summary>
        /// This extension method allows the caller to set the given <paramref name="iva"/>'s VC to from the given <paramref name="valueAsObject"/> value.  
        /// This will also cause the <paramref name="iva"/>'s IsSetPending flag to be set if the contained value is not equal to the newly assigned one.
        /// </summary>
        public static TIValueAccessor SetVCInline<TIValueAccessor>(this TIValueAccessor iva, object valueAsObject)
            where TIValueAccessor : IValueAccessor
        {
            if (iva != null)
                iva.VC = new ValueContainer(valueAsObject);

            return iva;
        }

        /// <summary>
        /// This extension method allows the caller to set the given <paramref name="iva"/>'s VC to from the given <paramref name="vc"/> value.  
        /// This will also cause the <paramref name="iva"/>'s IsSetPending flag to be set.
        /// </summary>
        public static TIValueAccessor SetVCInline<TIValueAccessor>(this TIValueAccessor iva, ValueContainer vc)
            where TIValueAccessor : IValueAccessor
        {
            if (iva != null)
                iva.VC = vc;

            return iva;
        }

        /// <summary>
        /// This extension method is used to conditionally initialize the corresponding table entry in the IVI (from which this <paramref name="iva"/> was obtained) to the value in this <paramref name="iva"/> if the table entry's value is in its initial state where its ValueSeqNum is 0.
        /// If the table entry has already been set (ValueSetNum != 0) then this method has no effect.
        /// </summary>
        public static TIValueAccessor SetInitialValueIfNeeded<TIValueAccessor>(this TIValueAccessor iva) 
            where TIValueAccessor : IValueAccessor
        {
            var ivaImpl = iva as ValuesInterconnection.ValueAccessorImpl;

            if (ivaImpl != null && ivaImpl.CurrentSeqNum == 0 && ivaImpl.IVI != null)
            {
                ivaImpl.IVI.Set(new IValueAccessor[] { iva }, setMode: SetMode.IfValueSeqNumIsZero);
            }

            return iva;
        }

        /// <summary>
        /// This extension method is used to Set (or Update/Merge) this <paramref name="iva"/>'s meta data using the given <paramref name="metaDataIn"/> and <paramref name="mergeBehavior"/>.  
        /// </summary>
        /// <param name="iva">Gives the IValueAccessor instance on which to set or update the meta data.</param>
        /// <param name="metaDataIn">Gives the meta date that is to be used to update the selected items (as determined by the <paramref name="mergeBehavior"/> parameter)</param>
        /// <param name="mergeBehavior">Defines how the selected items existing meta data will be combined with the given value.</param>
        public static TIValueAccessor SetMetaData<TIValueAccessor>(this TIValueAccessor iva, INamedValueSet metaDataIn, NamedValueMergeBehavior mergeBehavior = NamedValueMergeBehavior.AddAndUpdate)
            where TIValueAccessor : IValueAccessor
        {
            var ivaImpl = iva as ValuesInterconnection.ValueAccessorImpl;

            if (ivaImpl != null && ivaImpl.IVI != null)
            {
                ivaImpl.IVI.SetMetaData(new IValueAccessor[] { iva }, metaDataIn: metaDataIn, mergeBehavior: mergeBehavior);
            }

            return iva;
        }
    }

    #endregion
}

//-------------------------------------------------------------------
