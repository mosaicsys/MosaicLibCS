//-------------------------------------------------------------------
/*! @file Sets.cs
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;

using System.Xml.Serialization;
using System.Configuration;
using System.ComponentModel;
using System.Reflection;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Semi.E039;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Utils.Pooling;

namespace MosaicLib.WPF.Tools.Sets
{
    #region SetTracker (with static factory GetSetTracker method)

    /// <summary>
    /// This class has two functions:  
    /// <para/>First it contains a static GetSetTracker method that may be used to find, or create requested SetTracker.  
    /// As such the first requestor for a given SetID value will create the required SetTracker object and later requestors for the same setID will be given the same SetTracker instance.
    /// <para/>
    /// <para/>Second it provides the implementation of a dispatcher timer based engine that finds and tracks an identified ITrackableSet and generates two resulting read-only DependencyPropertyKey's: ReferenceSet and TrackingSet
    /// <para/>Produced (read-only) DependencyPropertyKeys: SetID, ReferenceSet, TrackingSet
    /// <para/>Supported DependencyProperties: UpdateRate, MaximumItemsPerIteration
    /// <para/>Supported events: NotifyOnSetUpdateList (IBasicNotificationList), SetDeltasEventNotificationList (IEventHandlerNotificationList{ISetDelta})
    /// </summary>
    public class SetTracker : DependencyObject, IServiceable
    {
        /// <summary>Gives the default update rate for set trackers (5.0 Hz)</summary>
        public const double DefaultUpdateRate = 5.0;

        /// <summary>Gives the default maximum items per iteration (0 - no limit)</summary>
        public const int DefaultMaximumItemsPerIteration = 0;

        #region static factory method : GetSetTracker(SetID)

        public static SetTracker GetSetTracker(string setName, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate, int defaultMaximumItemsPerIteration = DefaultMaximumItemsPerIteration, bool allowDelayedTrackingStart = true)
        {
            return GetSetTracker(new SetID(setName, generateUUIDForNull: false), isi: isi, defaultUpdateRate: defaultUpdateRate, defaultMaximumItemsPerIteration: defaultMaximumItemsPerIteration, allowDelayedTrackingStart: allowDelayedTrackingStart);
        }

        public static SetTracker GetSetTracker(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate, int defaultMaximumItemsPerIteration = DefaultMaximumItemsPerIteration, bool allowDelayedTrackingStart = true)
        {
            SetTracker setTracker = null;
            string setName = setID.Name.Sanitize();

            if (!setID.UUID.IsNullOrEmpty())
                setTracker = setUUIDToTrackerDictionary.SafeTryGetValue(setID.UUID);
            else
                setTracker = setNameToTrackerDictionary.SafeTryGetValue(setName);

            if (setTracker == null && !setName.IsNullOrEmpty())
            {
                setTracker = new SetTracker(setID, isi: isi, defaultUpdateRate: defaultUpdateRate, defaultMaximumItemsPerIteration: defaultMaximumItemsPerIteration, allowDelayedTrackingStart: allowDelayedTrackingStart);

                if (!setNameToTrackerDictionary.ContainsKey(setName))
                    setNameToTrackerDictionary[setName] = setTracker;

                if (!setID.UUID.IsNullOrEmpty())
                    setUUIDToTrackerDictionary[setID.UUID] = setTracker;
            }

            return setTracker;
        }

        private static Dictionary<string, SetTracker> setNameToTrackerDictionary = new Dictionary<string, SetTracker>();
        private static Dictionary<string, SetTracker> setUUIDToTrackerDictionary = new Dictionary<string, SetTracker>();

        #endregion

        public SetTracker(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate, int defaultMaximumItemsPerIteration = DefaultMaximumItemsPerIteration, bool allowDelayedTrackingStart = true)
        {
            SetID = setID;
            ISI = isi ?? Modular.Interconnect.Sets.Sets.Instance;

            ClientName = "{0} {1}".CheckedFormat(Fcns.CurrentClassLeafName, SetID);

            _setDeltasEventNotificationList.Source = this;

            _updateRate = defaultUpdateRate;
            _maximumItemsPerIteration = defaultMaximumItemsPerIteration;

            if (_updateRate != DefaultUpdateRate)
                UpdateRate = _updateRate;

            if (_maximumItemsPerIteration != DefaultMaximumItemsPerIteration)
                MaximumItemsPerIteration = _maximumItemsPerIteration;

            if (allowDelayedTrackingStart)
                System.Threading.SynchronizationContext.Current.Post((o) => AttemptToStartTracking(), this);
            else
                AttemptToStartTracking();
        }

        public ISetsInterconnection ISI { get; private set; }
        public string ClientName { get; private set; }

        private IDisposable sharedDispatcherTimerToken;

        private static DependencyPropertyKey SetIDPropertyKey = DependencyProperty.RegisterReadOnly("SetID", typeof(SetID), typeof(SetTracker), new PropertyMetadata(null));
        private static DependencyPropertyKey ReferenceSetPropertyKey = DependencyProperty.RegisterReadOnly("ReferenceSet", typeof(ITrackableSet), typeof(SetTracker), new PropertyMetadata(null));
        private static DependencyPropertyKey TrackingSetPropertyKey = DependencyProperty.RegisterReadOnly("TrackingSet", typeof(ITrackingSet), typeof(SetTracker), new PropertyMetadata(null));

        private static DependencyProperty UpdateRateProperty = DependencyProperty.Register("UpdateRate", typeof(double), typeof(SetTracker), new PropertyMetadata(DefaultUpdateRate, HandleUpdateRatePropertyChanged));
        private static DependencyProperty MaximumItemsPerIterationProperty = DependencyProperty.Register("MaximumItemsPerIteration", typeof(int), typeof(SetTracker), new PropertyMetadata(DefaultMaximumItemsPerIteration, HandleUpdateMaximumItemsPerIterationPropertyChanged));

        public SetID SetID { get { return _setID; } private set { SetValue(SetIDPropertyKey, (_setID = value)); } }
        public ITrackableSet ReferenceSet { get { return _referenceSet; } private set { SetValue(ReferenceSetPropertyKey, (_referenceSet = value)); } }
        public ITrackingSet TrackingSet { get { return _trackingSet; } private set { SetValue(TrackingSetPropertyKey, (_trackingSet = value)); } }

        public double UpdateRate
        {
            get { return (double)GetValue(UpdateRateProperty); }
            set { SetValue(UpdateRateProperty, value); }
        }

        public int MaximumItemsPerIteration
        {
            get { return (int)GetValue(MaximumItemsPerIterationProperty); }
            set { SetValue(MaximumItemsPerIterationProperty, value); }
        }

        public IBasicNotificationList NotifyOnSetUpdateList { get { return _notifyOnSetUpdateList; } }
        private BasicNotificationList _notifyOnSetUpdateList = new BasicNotificationList();

        public IEventHandlerNotificationList<ISetDelta> SetDeltasEventNotificationList { get { return _setDeltasEventNotificationList;} }
        private EventHandlerNotificationList<ISetDelta> _setDeltasEventNotificationList = new EventHandlerNotificationList<ISetDelta>();

        public SetTracker AddSetDeltasEventHandler(EventHandlerDelegate<ISetDelta> handler, Action<ISetDelta> setPrimingDelegate = null, bool delayPriming = false)
        {
            if (!delayPriming || setPrimingDelegate == null)
            {
                AttemptToStartTracking();

                if (_trackingSet != null && setPrimingDelegate != null)
                {
                    var syntheticInitializationSetDelta = _trackingSet.GenerateInitializerSetDelta();

                    setPrimingDelegate(syntheticInitializationSetDelta);
                }

                SetDeltasEventNotificationList.OnNotify += handler;
            }
            else
            {
                int entryCount = pendingAddSetDeltasEventHandlersWithPrimingList.Count;

                pendingAddSetDeltasEventHandlersWithPrimingList.Add(Tuple.Create(handler, setPrimingDelegate));

                if (entryCount == 0)
                    System.Threading.SynchronizationContext.Current.Post((o) => HandlePendingAddDeltasEventHandlersWithPriming(), this);
            }

            return this;
        }

        /// <summary>
        /// Caller passes temporary control to the target object's IServiceable.Service method.  Calls the underlying UpdateSet(useNullNotify: false) method and returns the result mapped to 1 (true)/0 (false).
        /// </summary>
        public virtual int Service(QpcTimeStamp qpcTimeStamp = default(QpcTimeStamp))
        {
            return UpdateSet(useNullNotify: false).MapToInt();
        }

        private List<Tuple<EventHandlerDelegate<ISetDelta>, Action<ISetDelta>>> pendingAddSetDeltasEventHandlersWithPrimingList = new List<Tuple<EventHandlerDelegate<ISetDelta>, Action<ISetDelta>>>();

        private void HandlePendingAddDeltasEventHandlersWithPriming()
        {
            UpdateSet(forceFullUpdate: true, useNullNotify: false);
        }

        private void ServicePendingAddDeltasPrimingList()
        {
            if (pendingAddSetDeltasEventHandlersWithPrimingList.Count > 0)
            {
                if (_trackingSet != null)
                {
                    var syntheticInitializationSetDelta = _trackingSet.GenerateInitializerSetDelta();

                    pendingAddSetDeltasEventHandlersWithPrimingList.DoForEach(t => InnerAddSetDeltasEventHandler(t.Item1, t.Item2, syntheticInitializationSetDelta));
                    pendingAddSetDeltasEventHandlersWithPrimingList.Clear();
                }
            }
        }

        private void InnerAddSetDeltasEventHandler(EventHandlerDelegate<ISetDelta> handler, Action<ISetDelta> setPrimingDelegate, ISetDelta primingSetDelta)
        {
            if (_trackingSet != null && setPrimingDelegate != null)
                setPrimingDelegate(primingSetDelta);

            SetDeltasEventNotificationList.OnNotify += handler;
        }

        public void AttemptToStartTracking(bool servicePendingAddDeltasPrimingList = true)
        {
            if (_referenceSet == null)
            {
                var foundReferenceSet = SetID.FindFirstSet(ISI);

                if (foundReferenceSet != null)
                {
                    ReferenceSet = foundReferenceSet;                           // also sets the DependencyPropertyKey.
                    TrackingSet = foundReferenceSet.CreateTrackingSet();        // also sets the DependencyPropertyKey.
                }
            }

            if (servicePendingAddDeltasPrimingList)
                ServicePendingAddDeltasPrimingList();

            if (sharedDispatcherTimerToken == null)
            {
                sharedDispatcherTimerToken = Timers.SharedDispatcherTimerFactory.GetAndStartSharedTimer(UpdateRate, ClientName, onTickNotifyDelegate: () => UpdateSet());
            }
        }

        QpcTimer relookupHoldoffTimer = new QpcTimer() { TriggerIntervalInSec = 1.0, AutoReset = true }.Start();

        public bool UpdateSet(bool forceFullUpdate = false, bool useNullNotify = true)
        {
            if (_trackingSet == null)
            {
                if (relookupHoldoffTimer.IsTriggered || forceFullUpdate)
                    AttemptToStartTracking();

                if (_trackingSet == null)
                {
                    if (useNullNotify)
                        _setDeltasEventNotificationList.Notify(null);

                    return false;
                }
            }

            if (_trackingSet.IsUpdateNeeded || _trackingSet.IsUpdateInProgress || forceFullUpdate)
            {
                var setDelta = _trackingSet.PerformUpdateIteration(forceFullUpdate ? 0 : _maximumItemsPerIteration, generateSetDelta: true);

                _notifyOnSetUpdateList.Notify();
                _setDeltasEventNotificationList.Notify(setDelta);

                return true;
            }
            else
            {
                if (useNullNotify)
                    _setDeltasEventNotificationList.Notify(null);

                return false;
            }
        }

        private SetID _setID;
        private ITrackableSet _referenceSet;
        private ITrackingSet _trackingSet;
        private double _updateRate = DefaultUpdateRate;
        private int _maximumItemsPerIteration = DefaultMaximumItemsPerIteration;

        private static void HandleUpdateRatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetTracker me = d as SetTracker;

            if (me != null)
            {
                double selectedUpdateRate = (double)e.NewValue;

                if (me._updateRate != selectedUpdateRate)
                {
                    Fcns.DisposeOfObject(ref me.sharedDispatcherTimerToken);

                    me.AttemptToStartTracking();
                }
            }
        }

        private static void HandleUpdateMaximumItemsPerIterationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetTracker me = d as SetTracker;

            if (me != null)
            {
                int selectedMaximumItemsPerIteration = (int)e.NewValue;

                if (me._maximumItemsPerIteration != selectedMaximumItemsPerIteration)
                {
                    me._maximumItemsPerIteration = (int)e.NewValue;
                }
            }
        }
    }

    #endregion

    #region SetNewestItemPicker

    /// <summary>
    /// This tool is constructed to find a given SetTracker (by name/setID) and then to monitor the resulting stream of SetDelta additions and retain the last added item
    /// who's selected key (using the keySelector) matches the given keyValue.
    /// <para/>Each time a new item is found this method will update its bindable read-only DependencyProperty called the Item.
    /// <para/>Produced (read-only) DependencyPropertyKeys: Item
    /// </summary>
    public class SetNewestItemPicker<TItemType, TKeyType> : DependencyObject
            where TKeyType : IEquatable<TKeyType>
    {
        public SetNewestItemPicker(string setName, Func<TItemType, TKeyType> keySelector, TKeyType keyValue, bool delayPriming = true)
            : this(new SetID(setName, generateUUIDForNull: false), keySelector, keyValue, delayPriming)
        { }

        public SetNewestItemPicker(SetID setID, Func<TItemType, TKeyType> keySelector, TKeyType keyValue, bool delayPriming = true)
        {
            this.setID = setID;
            this.keySelector = keySelector;
            this.keyValue = keyValue;

            rootSetTracker = SetTracker.GetSetTracker(setID);

            rootSetTracker.AddSetDeltasEventHandler(HandleOnNotifySetDeltasEvent, setPrimingDelegate: primingSetDelta => HandleOnNotifySetDeltasEvent(rootSetTracker, primingSetDelta), delayPriming: delayPriming);
        }

        private SetID setID;
        private Func<TItemType, TKeyType> keySelector;
        private TKeyType keyValue;

        private SetTracker rootSetTracker;

        /// <summary>
        /// Note: this method is coded to iterate forward through the elements in each given set delta.  This is based on the assumption that
        /// forward iteration is faster than reverse iteration and that matching elements will only appear sparsely in the set deltas.
        /// </summary>
        private void HandleOnNotifySetDeltasEvent(object source, ISetDelta eventArgs)
        {
            if (eventArgs != null)
            {
                TItemType lastMatchedItem = default(TItemType);
                int matchCount = 0;

                var addRangeItemSet = eventArgs.AddRangeItems;

                foreach (var addRangeItem in addRangeItemSet)
                {
                    var rangeItemSet = addRangeItem.RangeObjects;

                    foreach (TItemType testItem in rangeItemSet.SafeToSet<TItemType>())
                    {
                        TKeyType testItemKey = keySelector(testItem);

                        if (keyValue.Equals(testItemKey))
                        {
                            lastMatchedItem = testItem;
                            matchCount++;
                        }
                    }
                }

                if (matchCount > 0)
                    Item = lastMatchedItem;
            }
        }

        private static DependencyPropertyKey ItemPropertyKey = DependencyProperty.RegisterReadOnly("Item", typeof(TItemType), typeof(SetNewestItemPicker<TItemType, TKeyType>), new PropertyMetadata(null));

        public TItemType Item { get { return _item; } set { SetValue(ItemPropertyKey, _item = value); } }
        private TItemType _item;
    }

    #endregion

    #region SetNewestItemGroupPicker

    /// <summary>
    /// This tool is constructed to find a given SetTracker (by name/setID) and then to monitor the resulting stream of SetDelta additions and retain the last added item who's selected key (using the keySelector)
    /// matches any of the key values in the given groupKeyValuesArray.  The resulting array of selected items will be published to the GroupArray DependencyProperty at the end of each handled SetDelta if any of the
    /// items in the group where updated by that SetDelta.
    /// <para/>Produced (read-only) DependencyPropertyKeys: GroupArray
    /// </summary>
    public class SetNewestItemGroupPicker<TItemType, TKeyType> : DependencyObject
    {
        /// <summary>
        /// Constructor.  Accepts <paramref name="setName"/>, <paramref name="keySelector"/> and <paramref name="groupKeyValuesArray"/>.
        /// Finds the SetTracker for the given <paramref name="setName"/> and builds a dictinary of key values to group item indecies which will be used to identify set items that are to be picked and where they are to be put into the array of group items.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Will be thrown if any of the given values in the <paramref name="groupKeyValuesArray"/> are null or are otherwise incompatible with the use of the Dictionary.Add method</exception>
        /// <exception cref="System.ArgumentException">Will be thrown if there are any duplicates in the given <paramref name="groupKeyValuesArray"/>, or if the Dictionary.Add method believes that they are duplicates.</exception>
        public SetNewestItemGroupPicker(string setName, Func<TItemType, TKeyType> keySelector, TKeyType[] groupKeyValuesArray, bool delayPriming = true)
            : this(new SetID(setName, generateUUIDForNull: false), keySelector, groupKeyValuesArray, delayPriming)
        { }

        /// <summary>
        /// Constructor.  Accepts <paramref name="setID"/>, <paramref name="keySelector"/> and <paramref name="groupKeyValuesArray"/>.
        /// Finds the SetTracker for the given <paramref name="setID"/> and builds a dictinary of key values to group item indecies which will be used to identify set items that are to be picked and where they are to be put into the array of group items.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Will be thrown if any of the given values in the <paramref name="groupKeyValuesArray"/> are null or are otherwise incompatible with the use of the Dictionary.Add method</exception>
        /// <exception cref="System.ArgumentException">Will be thrown if there are any duplicates in the given <paramref name="groupKeyValuesArray"/>, or if the Dictionary.Add method believes that they are duplicates.</exception>
        public SetNewestItemGroupPicker(SetID setID, Func<TItemType, TKeyType> keySelector, TKeyType[] groupKeyValuesArray, bool delayPriming = true)
        {
            this.setID = setID;
            this.keySelector = keySelector;
            this.groupKeyValuesArray = groupKeyValuesArray ?? Utils.Collections.EmptyArrayFactory<TKeyType>.Instance;

            this.groupKeyValuesArray.DoForEach((keyValue, idx) => keyToIndexDictionary.Add(keyValue, idx));

            rootSetTracker = SetTracker.GetSetTracker(setID);

            referenceGroupArray = new TItemType[maxGroupIndex = groupKeyValuesArray.Length];

            GroupArray = referenceGroupArray.MakeCopyOf();

            rootSetTracker.AddSetDeltasEventHandler(HandleOnNotifySetDeltasEvent, setPrimingDelegate: primingSetDelta => HandleOnNotifySetDeltasEvent(rootSetTracker, primingSetDelta), delayPriming: delayPriming);
        }

        private SetID setID;
        private Func<TItemType, TKeyType> keySelector;
        private TKeyType [] groupKeyValuesArray;
        private Dictionary<TKeyType, int> keyToIndexDictionary = new Dictionary<TKeyType, int>();

        private TItemType[] referenceGroupArray;
        private int maxGroupIndex;

        private SetTracker rootSetTracker;

        private void HandleOnNotifySetDeltasEvent(object source, ISetDelta eventArgs)
        {
            if (eventArgs != null)
            {
                int matchCount = 0;

                var addRangeItemSet = eventArgs.AddRangeItems;

                foreach (var addRangeItem in addRangeItemSet)
                {
                    var rangeItemSet = addRangeItem.RangeObjects;

                    foreach (TItemType testItem in rangeItemSet.SafeToSet<TItemType>())
                    {
                        TKeyType testItemKey = keySelector(testItem);

                        int groupIdx = keyToIndexDictionary.SafeTryGetValue(testItemKey, fallbackValue: -1);

                        if (groupIdx >= 0 && groupIdx <= maxGroupIndex)
                        {
                            referenceGroupArray[groupIdx] = testItem;
                            matchCount++;
                        }
                    }
                }

                if (matchCount > 0)
                    GroupArray = referenceGroupArray.MakeCopyOf();
            }
        }

        private static DependencyPropertyKey GroupArrayPropertyKey = DependencyProperty.RegisterReadOnly("GroupArray", typeof(TItemType[]), typeof(SetNewestItemGroupPicker<TItemType, TKeyType>), new PropertyMetadata(null));

        public TItemType [] GroupArray { get { return _groupArray; } set { SetValue(GroupArrayPropertyKey, _groupArray = value); } }
        private TItemType [] _groupArray;
    }

    #endregion

    #region FilteredSubSetTracker

    /// <summary>
    /// This tool is constructed to find a given SetTracker (by name/setID) and then to monitor the resulting stream of SetDelta additions and retain a filtered set of the tracked items.
    /// <para/>Produced (read-only) DependencyPropertyKeys: Set
    /// <para/>DependencyProperties: SetName (get/set)
    /// </summary>
    public class FilteredSubSetTracker<TItemType> : DependencyObject
    {
        public FilteredSubSetTracker(string setName, int maximumCapacity = 1000, bool allowItemsToBeRemoved = true, Func<TItemType, bool> filterDelegate = null)
            : this(new SetID(setName, generateUUIDForNull: false), maximumCapacity: maximumCapacity, allowItemsToBeRemoved: allowItemsToBeRemoved, filterDelegate: filterDelegate)
        { }

        public FilteredSubSetTracker(SetID setID = null, int maximumCapacity = 1000, bool allowItemsToBeRemoved = true, Func<TItemType, bool> filterDelegate = null)
        {
            this.setID = setID;
            this.allowItemsToBeRemoved = allowItemsToBeRemoved;
            this.maximumCapacity = maximumCapacity;
            this.filterDelegate = filterDelegate;

            if (setID != null)
            {
                HandleNewSetID(setID);
                SetName = setID.Name;
            }
        }

        private bool allowItemsToBeRemoved;
        private int maximumCapacity;
        private SetID setID;

        private void HandleNewSetID(SetID setID)
        {
            if (handleOnNotifySetDeltasEventHandler == null)
                handleOnNotifySetDeltasEventHandler = HandleOnNotifySetDeltasEvent;

            // if we had a prior rootSetTracker, unhook our HandleOnNotifySetDeltasEvent from it
            if (rootSetTracker != null)
                rootSetTracker.SetDeltasEventNotificationList.OnNotify -= handleOnNotifySetDeltasEventHandler;

            rootSetTracker = SetTracker.GetSetTracker(setID);

            applyDeltasConfig = new ApplyDeltasConfig<TItemType>(allowItemsToBeRemoved: allowItemsToBeRemoved, addItemFilter: filterDelegate);

            adjustableSet = (rootSetTracker.TrackingSet != null) ? new AdjustableTrackingSet<TItemType>(rootSetTracker.TrackingSet, maximumCapacity, applyDeltasConfig) : null;

            if (adjustableSet != null)
                Set = adjustableSet;

            // Add our HandleOnNotifySetDeltasEvent handler to it and prime the local set using a synthetic set deltas from the source set that contains the full contents of the source set.
            rootSetTracker.AddSetDeltasEventHandler(handleOnNotifySetDeltasEventHandler, setPrimingDelegate: primingSetDelta => handleOnNotifySetDeltasEventHandler(rootSetTracker, primingSetDelta), delayPriming: true);

            if (adjustableSet != null)
            {
                setRebuiltNotificationList.Notify();

                if (adjustableSet.Count > 0)
                    newItemsAddedNotificationList.Notify();
            }
        }

        private EventHandlerDelegate<ISetDelta> handleOnNotifySetDeltasEventHandler;

        private void HandleOnNotifySetDeltasEvent(object source, ISetDelta eventArgs)
        {
            if (rootSetTracker == null || rootSetTracker.TrackingSet == null)
                return;

            if (adjustableSet == null)
            {
                adjustableSet = new AdjustableTrackingSet<TItemType>(rootSetTracker.TrackingSet, maximumCapacity, applyDeltasConfig);
                Set = adjustableSet;

                setRebuiltNotificationList.Notify();
            }

            long entryLastItemSeqNum = adjustableSet.ItemListSeqNumRangeInfo.Last;

            if (eventArgs != null)
                adjustableSet.ApplyDeltas(eventArgs);

            if (adjustableSet.ItemListSeqNumRangeInfo.Last != entryLastItemSeqNum)
                newItemsAddedNotificationList.Notify();
        }

        private SetTracker rootSetTracker;
        private ApplyDeltasConfig<TItemType> applyDeltasConfig;
        private AdjustableTrackingSet<TItemType> adjustableSet;

        private Func<TItemType, bool> filterDelegate = null;

        private static DependencyPropertyKey SetPropertyKey = DependencyProperty.RegisterReadOnly("Set", typeof(ITrackingSet<TItemType>), typeof(FilteredSubSetTracker<TItemType>), new PropertyMetadata(null));
        public static DependencyProperty SetNameProperty = DependencyProperty.Register("SetName", typeof(string), typeof(FilteredSubSetTracker<TItemType>), new PropertyMetadata(null, HandleSetNamePropertyChanged));

        public ITrackingSet<TItemType> Set { get { return adjustableSet; } set { SetValue(SetPropertyKey, value); } }
        public string SetName { get { return (string)GetValue(SetNameProperty); } set { SetValue(SetNameProperty, value); } }

        private static void HandleSetNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            FilteredSubSetTracker<TItemType> me = d as FilteredSubSetTracker<TItemType>;

            if (me != null)
            {
                SetID entrySetID = me.setID;

                me.setID = new SetID((string)e.NewValue, generateUUIDForNull: false);

                if (!me.setID.Equals(entrySetID))
                    me.HandleNewSetID(me.setID);
            }
        }

        public event BasicNotificationDelegate SetRebuilt { add { setRebuiltNotificationList.OnNotify += value; } remove { setRebuiltNotificationList.OnNotify -= value; } }
        public event BasicNotificationDelegate NewItemsAdded { add { newItemsAddedNotificationList.OnNotify += value; } remove { newItemsAddedNotificationList.OnNotify -= value; } }

        private BasicNotificationList setRebuiltNotificationList = new BasicNotificationList();
        private BasicNotificationList newItemsAddedNotificationList = new BasicNotificationList();
    }

    #endregion

    #region AdjustableLogMessageSetTracker

    /// <summary>
    /// This DependencyObject class provides a customized and adjustable means to track and filter a set of log messages as observed from selected SetTracker instance.
    /// <para/>Produced (read-only) DependencyPropertyKeys: Set, TotalCount
    /// <para/>Supported DependencyProperties: SetName, Pause, FilterDelegate, EnabledSources, FilterString, LogGate, EnableFatal, EnableError, EnableWarning, EnableSignif, EnableInfo, EnableDebug, EnableTrace
    /// <para/>Supported events: SetRebuilt (BasicNotificationDelegate), NewItemsAdded (BasicNotificationDelegate)
    /// </summary>
    public class AdjustableLogMessageSetTracker : DependencyObject
    {
        public static readonly MosaicLib.Logging.LogGate DefaultLogGate = MosaicLib.Logging.LogGate.Debug;

        public AdjustableLogMessageSetTracker(string setName, int maximumCapacity = 1000, bool allowItemsToBeRemoved = false)
            : this(setName.IsNeitherNullNorEmpty() ? new SetID(setName, generateUUIDForNull: false) : null, maximumCapacity: maximumCapacity, allowItemsToBeRemoved: allowItemsToBeRemoved)
        { }

        public AdjustableLogMessageSetTracker(SetID setID = null, int maximumCapacity = 1000, bool allowItemsToBeRemoved = false)
        {
            this.setID = setID;
            this.allowItemsToBeRemoved = allowItemsToBeRemoved;
            this.maximumCapacity = maximumCapacity;

            if (setID != null)
            {
                HandleNewSetID(setID);
                SetName = setID.Name;
            }
        }

        public void Clear()
        {
            if (rootSetTracker != null)
            {
                setCollector.Clear();
                adjustableSet.Clear();
                ClearCollectedSetDeltas();
            }
        }

        public int MaximumCapacity { get { return maximumCapacity; } }

        private void HandleNewSetID(SetID setID)
        {
            if (handleOnNotifySetDeltasEventHandler == null)
                handleOnNotifySetDeltasEventHandler = HandleOnNotifySetDeltasEvent;

            if (rootSetTracker != null)
                rootSetTracker.SetDeltasEventNotificationList.OnNotify -= handleOnNotifySetDeltasEventHandler;

            rootSetTracker = SetTracker.GetSetTracker(setID);

            setCollector = new TrackingSet<MosaicLib.Logging.ILogMessage>(setID, setType: SetType.Tracking, initialCapacity: maximumCapacity);

            RegenerateAdjustableSet();

            rootSetTracker.AddSetDeltasEventHandler(handleOnNotifySetDeltasEventHandler, setPrimingDelegate: primingSetDelta => setCollector.ApplyDeltas(primingSetDelta), delayPriming: true);
        }

        private EventHandlerDelegate<ISetDelta> handleOnNotifySetDeltasEventHandler;

        private void RegenerateAdjustableSet()
        {
            if (rootSetTracker != null)
            {
                if (filterDelegate != null)
                {
                    applyDeltasConfig = new ApplyDeltasConfig<MosaicLib.Logging.ILogMessage>(allowItemsToBeRemoved: allowItemsToBeRemoved, addItemFilter: filterDelegate);
                }
                else
                {
                    ValueContainer enabledSourcesVC = ValueContainer.CreateFromObject(enabledSources);
                    HashSet<string> sourceFilterHashSet = (enabledSourcesVC.IsNullOrEmpty) ? null : new HashSet<string>(enabledSourcesVC.GetValue<IList<string>>(rethrow: false, defaultValue: MosaicLib.Utils.Collections.EmptyArrayFactory<string>.Instance));
                    var useLogGate = logGate;

                    string[] splitFilterStringArray = filterString.MapNullToEmpty().Split('|').Where(str => str.IsNeitherNullNorEmpty()).ToArray();

                    switch (splitFilterStringArray.Length)
                    {
                        case 0:
                            addItemFilter = (lm) => ((lm != null)
                                                     && (useLogGate.IsTypeEnabled(lm.MesgType))
                                                     && (sourceFilterHashSet == null || sourceFilterHashSet.Contains(lm.LoggerName))
                                                     );
                            break;

                        case 1:
                            var firstFilterStr = splitFilterStringArray[0];
                            addItemFilter = (lm) => ((lm != null)
                                                     && (useLogGate.IsTypeEnabled(lm.MesgType))
                                                     && (sourceFilterHashSet == null || sourceFilterHashSet.Contains(lm.LoggerName))
                                                     && ((lm.MesgType.ToString().IndexOf(firstFilterStr, StringComparison.CurrentCultureIgnoreCase) >= 0) || lm.LoggerName.IndexOf(firstFilterStr, StringComparison.CurrentCultureIgnoreCase) >= 0) || (lm.Mesg.IndexOf(firstFilterStr, StringComparison.CurrentCultureIgnoreCase) >= 0)
                                                     );
                            break;

                        default:
                            addItemFilter = (lm) => ((lm != null)
                                                     && (useLogGate.IsTypeEnabled(lm.MesgType))
                                                     && (sourceFilterHashSet == null || sourceFilterHashSet.Contains(lm.LoggerName))
                                                     && splitFilterStringArray.Any(filterStrItem => ((lm.MesgType.ToString().IndexOf(filterStrItem, StringComparison.CurrentCultureIgnoreCase) >= 0) || lm.LoggerName.IndexOf(filterStrItem, StringComparison.CurrentCultureIgnoreCase) >= 0) || (lm.Mesg.IndexOf(filterStrItem, StringComparison.CurrentCultureIgnoreCase) >= 0))
                                                     );
                            break;
                    }

                    applyDeltasConfig = new ApplyDeltasConfig<MosaicLib.Logging.ILogMessage>(allowItemsToBeRemoved: allowItemsToBeRemoved, addItemFilter: addItemFilter);
                }

                adjustableSet = new AdjustableTrackingSet<MosaicLib.Logging.ILogMessage>(setCollector, maximumCapacity, applyDeltasConfig);
                adjustableSet.PerformUpdateIteration();
                Set = adjustableSet;

                ClearCollectedSetDeltas();

                setRebuiltNotificationList.Notify();
            }
        }

        private void HandleOnNotifySetDeltasEvent(object source, ISetDelta eventArgs)
        {
            if (rootSetTracker == null)
            { }
            else if (!pause)
            {
                if (!regenerateSetsAfterUnpaused)
                {
                    if (totalCollectedAddItems > 0)
                    {
                        collectedSetDeltaListWhilePaused.DoForEach(collectedSetDelta => setCollector.ApplyDeltas(collectedSetDelta));

                        ClearCollectedSetDeltas();
                    }

                    if (eventArgs != null)
                    {
                        setCollector.ApplyDeltas(eventArgs);
                    }

                    long entryLastItemSeqNum = adjustableSet.ItemListSeqNumRangeInfo.Last;

                    adjustableSet.PerformUpdateIteration();
                    TotalCount = setCollector.Count;

                    if (adjustableSet.ItemListSeqNumRangeInfo.Last != entryLastItemSeqNum)
                        newItemsAddedNotificationList.Notify();
                }
                else
                {
                    RegenerateAdjustableSet();
                }
            }
            else if (!regenerateSetsAfterUnpaused && eventArgs != null)
            {
                collectedSetDeltaListWhilePaused.Add(eventArgs);
                totalCollectedAddItems += eventArgs.TotalAddedItemCount;
                if (totalCollectedAddItems >= maximumCapacity)
                    regenerateSetsAfterUnpaused = true;
            }
        }

        private List<ISetDelta> collectedSetDeltaListWhilePaused = new List<ISetDelta>();
        private int totalCollectedAddItems = 0;
        private bool regenerateSetsAfterUnpaused = false;

        private void ClearCollectedSetDeltas()
        {
            collectedSetDeltaListWhilePaused.Clear();
            totalCollectedAddItems = 0;
            regenerateSetsAfterUnpaused = false;
        }

        private bool allowItemsToBeRemoved;
        private int maximumCapacity;
        private SetID setID;

        private SetTracker rootSetTracker;
        private ITrackingSet<MosaicLib.Logging.ILogMessage> setCollector;
        private ApplyDeltasConfig<MosaicLib.Logging.ILogMessage> applyDeltasConfig;
        private AdjustableTrackingSet<MosaicLib.Logging.ILogMessage> adjustableSet;

        private bool pause = false;
        private Func<MosaicLib.Logging.ILogMessage, bool> filterDelegate = null;
        private object enabledSources = null;
        private MosaicLib.Logging.LogGate logGate = DefaultLogGate;
        private string filterString = null;
        private Func<MosaicLib.Logging.ILogMessage, bool> addItemFilter = null;

        private static DependencyPropertyKey SetPropertyKey = DependencyProperty.RegisterReadOnly("Set", typeof(ITrackingSet<MosaicLib.Logging.ILogMessage>), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(null));
        private static DependencyPropertyKey TotalCountPropertyKey = DependencyProperty.RegisterReadOnly("TotalCount", typeof(int), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(0));
        public static DependencyProperty SetNameProperty = DependencyProperty.Register("SetName", typeof(string), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(null, HandleSetNamePropertyChanged));
        public static DependencyProperty PauseProperty = DependencyProperty.Register("Pause", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(false, HandlePausePropertyChanged));
        public static DependencyProperty FilterDelegateProperty = DependencyProperty.Register("FilterDelegate", typeof(Func<MosaicLib.Logging.ILogMessage, bool>), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(null, HandleFilterDelegatePropertyChanged));
        public static DependencyProperty EnabledSourcesProperty = DependencyProperty.Register("EnabledSources", typeof(object), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(null, HandleEnabledSourcesPropertyChanged));
        public static DependencyProperty FilterStringProperty = DependencyProperty.Register("FilterString", typeof(string), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata("", HandleFilterStringPropertyChanged));
        public static DependencyProperty LogGateProperty = DependencyProperty.Register("LogGate", typeof(MosaicLib.Logging.LogGate), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate, HandleLogGatePropertyChanged));
        public static DependencyProperty EnableFatalProperty = DependencyProperty.Register("EnableFatal", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Fatal), (d, e) => HandleMessageTypeLogGatePropertyChanged(d, MosaicLib.Logging.MesgType.Fatal, e)));
        public static DependencyProperty EnableErrorProperty = DependencyProperty.Register("EnableError", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Error), (d, e) => HandleMessageTypeLogGatePropertyChanged(d, MosaicLib.Logging.MesgType.Error, e)));
        public static DependencyProperty EnableWarningProperty = DependencyProperty.Register("EnableWarning", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Warning), (d, e) => HandleMessageTypeLogGatePropertyChanged(d, MosaicLib.Logging.MesgType.Warning, e)));
        public static DependencyProperty EnableSignifProperty = DependencyProperty.Register("EnableSignif", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Signif), (d, e) => HandleMessageTypeLogGatePropertyChanged(d, MosaicLib.Logging.MesgType.Signif, e)));
        public static DependencyProperty EnableInfoProperty = DependencyProperty.Register("EnableInfo", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Info), (d, e) => HandleMessageTypeLogGatePropertyChanged(d, MosaicLib.Logging.MesgType.Info, e)));
        public static DependencyProperty EnableDebugProperty = DependencyProperty.Register("EnableDebug", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Debug), (d, e) => HandleMessageTypeLogGatePropertyChanged(d, MosaicLib.Logging.MesgType.Debug, e)));
        public static DependencyProperty EnableTraceProperty = DependencyProperty.Register("EnableTrace", typeof(bool), typeof(AdjustableLogMessageSetTracker), new PropertyMetadata(DefaultLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Trace), (d, e) => HandleMessageTypeLogGatePropertyChanged(d, MosaicLib.Logging.MesgType.Trace, e)));

        public ITrackingSet<MosaicLib.Logging.ILogMessage> Set { get { return adjustableSet; } set { SetValue(SetPropertyKey, value); } }
        public int TotalCount { get { return TotalCount; } set { SetValue(TotalCountPropertyKey, value); } }
        public string SetName { get { return (string)GetValue(SetNameProperty); } set { SetValue(SetNameProperty, value); } }
        public bool Pause { get { return (bool)GetValue(PauseProperty); } set { SetValue(PauseProperty, value); } }
        public Func<MosaicLib.Logging.ILogMessage, bool> FilterDelegate { get { return (Func<MosaicLib.Logging.ILogMessage, bool>)GetValue(FilterDelegateProperty); } set { SetValue(FilterDelegateProperty, value); } }
        public object EnabledSources { get { return GetValue(EnabledSourcesProperty); } set { SetValue(EnabledSourcesProperty, value); } }
        public string FilterString { get { return (string) GetValue(FilterStringProperty); } set { SetValue(FilterStringProperty, value); } }
        public MosaicLib.Logging.LogGate LogGate { get { return (MosaicLib.Logging.LogGate) GetValue(LogGateProperty); } set { SetValue(LogGateProperty, value); } }
        public bool EnableFatal { get { return (bool) GetValue(EnableFatalProperty); } set { SetValue(EnableFatalProperty, value); } }
        public bool EnableError { get { return (bool) GetValue(EnableErrorProperty); } set { SetValue(EnableErrorProperty, value); } }
        public bool EnableWarning { get { return (bool) GetValue(EnableWarningProperty); } set { SetValue(EnableWarningProperty, value); } }
        public bool EnableSignif { get { return (bool) GetValue(EnableSignifProperty); } set { SetValue(EnableSignifProperty, value); } }
        public bool EnableInfo { get { return (bool) GetValue(EnableInfoProperty); } set { SetValue(EnableInfoProperty, value); } }
        public bool EnableDebug { get { return (bool) GetValue(EnableDebugProperty); } set { SetValue(EnableDebugProperty, value); } }
        public bool EnableTrace { get { return (bool) GetValue(EnableTraceProperty); } set { SetValue(EnableTraceProperty, value); } }

        private static void HandleSetNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AdjustableLogMessageSetTracker me = d as AdjustableLogMessageSetTracker;

            if (me != null)
            {
                SetID entrySetID = me.setID;

                me.setID = new SetID((string)e.NewValue, generateUUIDForNull: false);

                if (!me.setID.Equals(entrySetID))
                    me.HandleNewSetID(me.setID);
            }
        }

        private static void HandlePausePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AdjustableLogMessageSetTracker me = d as AdjustableLogMessageSetTracker;

            if (me != null)
            {
                bool entryPause = me.pause;

                me.pause = (bool) e.NewValue;

                if (entryPause && !me.pause)
                    me.HandleOnNotifySetDeltasEvent(d, null);
            }
        }

        private static void HandleFilterDelegatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AdjustableLogMessageSetTracker me = d as AdjustableLogMessageSetTracker;

            if (me != null)
            {
                Func<MosaicLib.Logging.ILogMessage, bool> selectedFilter = (Func<MosaicLib.Logging.ILogMessage, bool>) e.NewValue;

                if (me.filterDelegate != selectedFilter)
                {
                    me.filterDelegate = selectedFilter;
                    me.RegenerateAdjustableSet();
                }
            }
        }

        private static void HandleEnabledSourcesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AdjustableLogMessageSetTracker me = d as AdjustableLogMessageSetTracker;

            if (me != null)
            {
                object selectedEnabledSources = e.NewValue;

                if (!object.Equals(me.enabledSources, selectedEnabledSources))
                {
                    me.enabledSources = selectedEnabledSources;
                    me.RegenerateAdjustableSet();
                }
            }
        }

        private static void HandleFilterStringPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AdjustableLogMessageSetTracker me = d as AdjustableLogMessageSetTracker;

            if (me != null)
            {
                string selectedFilterString = (string) e.NewValue;

                if (me.filterString != selectedFilterString)
                {
                    me.filterString = selectedFilterString;
                    me.RegenerateAdjustableSet();
                }
            }
        }

        bool isInHandleLogGatePropertyChangedCallback = false;

        private static void HandleLogGatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AdjustableLogMessageSetTracker me = d as AdjustableLogMessageSetTracker;

            if (me != null)
            {
                me.isInHandleLogGatePropertyChangedCallback = true;

                MosaicLib.Logging.LogGate entryLogGate = me.logGate;
                MosaicLib.Logging.LogGate selectedLogGate = (MosaicLib.Logging.LogGate)e.NewValue;

                if (me.logGate != selectedLogGate)
                {
                    me.logGate = selectedLogGate;
                    me.RegenerateAdjustableSet();

                    MosaicLib.Logging.LogGate logGateDiffBits = entryLogGate ^ selectedLogGate;

                    if (logGateDiffBits.IsTypeEnabled(MosaicLib.Logging.MesgType.Error))
                        me.EnableError = selectedLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Error);
                    if (logGateDiffBits.IsTypeEnabled(MosaicLib.Logging.MesgType.Warning))
                        me.EnableWarning = selectedLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Warning);
                    if (logGateDiffBits.IsTypeEnabled(MosaicLib.Logging.MesgType.Signif))
                        me.EnableSignif = selectedLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Signif);
                    if (logGateDiffBits.IsTypeEnabled(MosaicLib.Logging.MesgType.Info))
                        me.EnableInfo = selectedLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Info);
                    if (logGateDiffBits.IsTypeEnabled(MosaicLib.Logging.MesgType.Debug))
                        me.EnableDebug = selectedLogGate.IsTypeEnabled(MosaicLib.Logging.MesgType.Debug);
                }

                me.isInHandleLogGatePropertyChangedCallback = false;
            }
        }

        private static void HandleMessageTypeLogGatePropertyChanged(DependencyObject d, MosaicLib.Logging.MesgType mesgType, DependencyPropertyChangedEventArgs e)
        {
            AdjustableLogMessageSetTracker me = d as AdjustableLogMessageSetTracker;

            if (me != null && !me.isInHandleLogGatePropertyChangedCallback)
            {
                bool enableMesgType = (bool) e.NewValue;
                MosaicLib.Logging.LogGate entryLogGate = me.logGate;
                MosaicLib.Logging.LogGate updatedLogGate = entryLogGate;
                MosaicLib.Logging.LogGate mesgTypeAsLogGateBit = new MosaicLib.Logging.LogGate(mesgType, MosaicLib.Logging.MesgTypeMask.MaskType.Bit);

                if (enableMesgType)
                    updatedLogGate |= mesgTypeAsLogGateBit;
                else
                    updatedLogGate &= ~mesgTypeAsLogGateBit;

                if (entryLogGate != updatedLogGate)
                    me.LogGate = updatedLogGate;
            }
        }

        public event BasicNotificationDelegate SetRebuilt { add { setRebuiltNotificationList.OnNotify += value; } remove { setRebuiltNotificationList.OnNotify -= value; } }
        public event BasicNotificationDelegate NewItemsAdded { add { newItemsAddedNotificationList.OnNotify += value; } remove { newItemsAddedNotificationList.OnNotify -= value; } }

        private BasicNotificationList setRebuiltNotificationList = new BasicNotificationList();
        private BasicNotificationList newItemsAddedNotificationList = new BasicNotificationList();
    }

    #endregion

    #region E039 related Set tracker objects

    namespace E039
    {
        /// <summary>
        /// This class is used as the root DependencyObject for bindable observation of objects in a set of IE039Objects that is generally
        /// maintained by an IE039TableUpdater (such as an E039BasicTablePart).  
        /// <para/>This object supports the IE039TableTypeDictionaryFactory interface which is used to obtain access to IE039TableTypeDictionary instances for a given object "Type"
        /// From the resulting IE039TableTypeDictionary the client can obtain individual E039ObjectTracker instances for given object "Name" values.  These objects provide a 
        /// simple Object Dependency Property that can be used to observe the contents of the object with the resulting Type and Name.  In addition these objects also support
        /// access to E039LinkToObjectTracker factory objects and E039LinksFromObjectsTracker factories.  These two factories are used to create link to/from tracker objects
        /// for a given LinkKey.  These trackers then report the Object/Objects at the other end of the name link from/to the original E039ObjectTracker object.
        /// <para/>This object and the related tracker DependencyObjects are designed to support clean use from XAML using binding statements with paths such as:
        /// <code>E039TableSetTracker[loc][loc_03].E039LinksFromObjectsTrackerFactory[From].FirstObject</code> and
        /// <code>E039TableSetTracker[item][item_01].Object</code>
        /// <para/>See remarks section below for more details.
        /// </summary>
        /// <remarks>
        /// Instances of this class are constructed from a SetID which defines the set that they will observe (using a corresonding SetTracker instance)
        /// <para/>Like the SetTracker, this class supports a static GetE039TableSetTracker factory method to simplify shared use of this object type.
        /// <para/>
        /// <para/>It is expected that the use of this top level objects and the sub-objects it supports use with will be combined with use of custom controls, templates, and value converters
        /// to produce useful and expressive visualization of the contents of the underlying E039 object tables.
        /// </remarks>
        public class E039TableSetTracker : DependencyObject, IE039TablePerTypeDictionaryFactory
        {
            #region Constant values (DefaultUpdateRate)

            /// <summary>Gives the default update rate for set trackers (5.0 Hz)</summary>
            public const double DefaultUpdateRate = 5.0;

            #endregion

            #region E039TableSetTracker static factory method(s) [GetE039TableSetTracker]

            public static E039TableSetTracker GetE039TableSetTracker(string setName, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
            {
                return GetE039TableSetTracker(new SetID(setName, generateUUIDForNull: false), isi: isi, defaultUpdateRate: defaultUpdateRate);
            }

            public static E039TableSetTracker GetE039TableSetTracker(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
            {
                E039TableSetTracker setTracker = null;
                string setName = setID.Name.Sanitize();

                if (!setID.UUID.IsNullOrEmpty())
                    setTracker = setUUIDToTrackerDictionary.SafeTryGetValue(setID.UUID);
                else
                    setTracker = setNameToTrackerDictionary.SafeTryGetValue(setName);

                if (setTracker == null && !setName.IsNullOrEmpty())
                {
                    setTracker = new E039TableSetTracker(setID, isi: isi, defaultUpdateRate: defaultUpdateRate);

                    if (!setNameToTrackerDictionary.ContainsKey(setName))
                        setNameToTrackerDictionary[setName] = setTracker;

                    if (!setID.UUID.IsNullOrEmpty())
                        setUUIDToTrackerDictionary[setID.UUID] = setTracker;
                }

                return setTracker;
            }

            private static Dictionary<string, E039TableSetTracker> setNameToTrackerDictionary = new Dictionary<string, E039TableSetTracker>();
            private static Dictionary<string, E039TableSetTracker> setUUIDToTrackerDictionary = new Dictionary<string, E039TableSetTracker>();

            #endregion

            #region Construction

            public E039TableSetTracker(string setName, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
                : this(new SetID(setName, generateUUIDForNull: false), isi: isi, defaultUpdateRate: defaultUpdateRate)
            { }

            public E039TableSetTracker(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
            {
                SetTracker = SetTracker.GetSetTracker(setID, isi: isi, defaultUpdateRate: defaultUpdateRate, defaultMaximumItemsPerIteration: 0);

                if (SetTracker.MaximumItemsPerIteration != 0)
                    SetTracker = new SetTracker(setID, isi: isi, defaultUpdateRate: defaultUpdateRate, defaultMaximumItemsPerIteration: 0);

                var setTrackerTracker = new SetTrackerTracker()
                {
                    setID = setID,
                    setTracker = SetTracker,
                };

                SetTracker.AddSetDeltasEventHandler((sender, setDelta) => HandleOnNotifySetDeltasEvent(setTrackerTracker, setDelta), setDelta => HandleOnNotifySetDeltasEvent(setTrackerTracker, setDelta), delayPriming: true);
            }

            #endregion

            #region public interface (TypeNames, E039TableTypeDictionaryFactory, IE039TableTypeDictionary this[typeName], GetObjectTracker)

            /// <summary>
            /// This gives access to the setTracker instance that this table set tracker is based on.
            /// </summary>
            public SetTracker SetTracker { get; private set; }

            public IE039TablePerTypeDictionary this[string typeName] { get { return GetTableTypeDictionary(typeName); } }

            /// <summary>
            /// Requests that this table set tracker explicitly create an E039ObjectTracker for the given E039ObjectID <paramref name="objID"/>.
            /// The caller can indicate if this object is intended for temporary use (can be reclaimed when desired) or if it is for perminant use (suitable for shared use).
            /// If the caller indicates that it is for temproary use then the client is responsible for calling Dispose on the returned object when the client is done using.  
            /// <para/>The use of such temporary use objects is most useful when using such a tracker to bind to an object that is itself only temporary.
            /// </summary>
            public E039ObjectTracker GetObjectTracker(E039ObjectID objID, bool forTemporaryUse = false) { return CreateE039ObjectTracker(GetInnerObjectTracker(objID, null), forSharedUse: !forTemporaryUse); }

            #endregion

            #region Support helper methods for subordinate types

            internal E039LinkToObjectTracker HandleGetLinkToObjectTracker(E039ObjectTracker objTracker, string linkKey)
            {
                var innerObjTracker = objTracker.innerObjectTracker;

                var linkToTracker = objTracker.linkToObjectTrackerByLinkKeyDictionary.SafeTryGetValue(linkKey);

                if (linkToTracker == null)
                {
                    linkToTracker = new E039LinkToObjectTracker()
                    {
                        tableSetTracker = this,
                        e039ObjectTracker = objTracker,
                        linkKey = linkKey,
                    };

                    objTracker.linkToObjectTrackerByLinkKeyDictionary[linkKey] = linkToTracker;
                    objTracker.linkToObjectTrackerArray = objTracker.linkToObjectTrackerByLinkKeyDictionary.Values.ToArray();

                    ServiceInnerObjectTracker(innerObjTracker, forceUpdateLinksToList: true);
                }

                return linkToTracker;
            }

            internal E039LinksFromObjectsTracker HandleGetLinksFromObjectsTracker(E039ObjectTracker objTracker, string linkKey)
            {
                var innerObjTracker = objTracker.innerObjectTracker;

                var linksFromTracker = objTracker.linksFromObjectsTrackerByLinkKeyDictionary.SafeTryGetValue(linkKey);

                if (linksFromTracker == null)
                {
                    linksFromTracker = new E039LinksFromObjectsTracker()
                    {
                        tableSetTracker = this,
                        e039ObjectTracker = objTracker,
                        linkKey = linkKey,
                    };

                    objTracker.linksFromObjectsTrackerByLinkKeyDictionary[linkKey] = linksFromTracker;
                    objTracker.linksFromObjectsTrackerArray = objTracker.linksFromObjectsTrackerByLinkKeyDictionary.Values.ToArray();

                    ServiceInnerObjectTracker(innerObjTracker, forceUpdateLinksFromList: true);
                }

                return linksFromTracker;
            }

            internal void HandleDispose(E039ObjectTracker objTracker)
            {
                if (!objTracker.DisposeRequested)
                {
                    objTracker.DisposeRequested = true;

                    // unhook both sets of LinkTrackers from the underling innerObjectTrackers that they are observing.  This is done by telling them that the links are empty.
                    foreach (var linkToTracker in objTracker.linkToObjectTrackerArray)
                        HandleLinkUpdate(linkToTracker, E039Link.Empty);

                    foreach (var linksFromTracker in objTracker.linksFromObjectsTrackerArray)
                        HandleLinksUpdate(linksFromTracker, Utils.Collections.EmptyArrayFactory<E039Link>.Instance);

                    var innerObjTracker = objTracker.innerObjectTracker;
                    innerObjTracker.attachedObjectTrackerList.Remove(objTracker);

                    NoteTouched(innerObjTracker);
                }
            }

            #endregion

            #region HandleOnNotifySetDeltasEvent and related fields

            private Dictionary<string, E039TableTypeDictionary> typeNameToTableTypeDictionary = new Dictionary<string, E039TableTypeDictionary>();

            private List<InnerObjectTracker> touchedInnerObjectTrackerList = new List<InnerObjectTracker>();

            private void HandleOnNotifySetDeltasEvent(SetTrackerTracker setTrackerTracker, ISetDelta setDelta)
            {
                if (setDelta == null)
                    return;

                if (setDelta.ClearSetAtStart)
                {
                    var allItemsSeqNumArray = setTrackerTracker.currentItemInnerObjTrackerListSortedBySeqNum.Keys.ToArray();

                    foreach (var itemSeqNum in allItemsSeqNumArray)
                        RemoveItemBySeqNumIfPresent(setTrackerTracker, itemSeqNum);
                }

                if (setDelta.RemoveRangeItems != null)
                {
                    foreach (var removeRangeItem in setDelta.RemoveRangeItems)
                    {
                        long rangeStartSeqNum = removeRangeItem.RangeStartSeqNum;

                        foreach (var offset in Enumerable.Range(0, removeRangeItem.Count))
                            RemoveItemBySeqNumIfPresent(setTrackerTracker, rangeStartSeqNum + offset);
                    }
                }

                if (setDelta.AddRangeItems != null)
                {
                    foreach (var addRangeItem in setDelta.AddRangeItems)
                    {
                        long seqNum = addRangeItem.RangeStartSeqNum;

                        foreach (var addObject in addRangeItem.RangeObjects)
                        {
                            IE039Object addE039Object = addObject as IE039Object;
                            E039ObjectID addE039ObjectID = (addE039Object != null) ? addE039Object.ID : null;

                            if (addE039ObjectID != null && addE039ObjectID.Type != null && addE039ObjectID.Name != null)    // it is "ok" if they are both empty
                            {
                                var pair = new ItemSeqNumAndE039ObjectPair() { ItemSeqNum = seqNum, Object = addE039Object };

                                E039TableTypeDictionary tableTypeDictionary = setTrackerTracker.typeDictionary.SafeTryGetValue(addE039ObjectID.Type);
                                if (tableTypeDictionary == null)
                                {
                                    tableTypeDictionary = GetTableTypeDictionary(addE039ObjectID.Type);

                                    setTrackerTracker.typeDictionary[addE039ObjectID.Type] = tableTypeDictionary;
                                }

                                InnerObjectTracker innerObjTracker = GetInnerObjectTracker(addE039ObjectID, tableTypeDictionary);

                                // if needed update the innerObjectTracker's objectID and publish it to the attached object trackers since we now know the UUID (if any) for this object
                                if (!addE039ObjectID.Equals(innerObjTracker.objectID))
                                {
                                    innerObjTracker.objectID = addE039ObjectID;
                                    foreach (var objTrackers in innerObjTracker.attachedObjectTrackerList)
                                        objTrackers.ObjectID = addE039ObjectID;
                                }

                                if (innerObjTracker.pair.Object != null)
                                    innerObjTracker.prevPair = innerObjTracker.pair;

                                IE039Object prevObj = innerObjTracker.prevPair.Object;

                                innerObjTracker.pair = pair;

                                NoteTouched(innerObjTracker);

                                IList<E039Link> linksToOtherObjectsList = addE039Object.LinksToOtherObjectsList;
                                IList<E039Link> linksFromOtherObjectsList = addE039Object.LinksFromOtherObjectsList;

                                innerObjTracker.objLinksToListTouched = (prevObj == null || !linksToOtherObjectsList.IsEqualTo(prevObj.LinksToOtherObjectsList));
                                innerObjTracker.objLinksFromListTouched = (prevObj == null || !linksFromOtherObjectsList.IsEqualTo(prevObj.LinksFromOtherObjectsList));

                                setTrackerTracker.currentItemInnerObjTrackerListSortedBySeqNum.Add(innerObjTracker.pair.ItemSeqNum, innerObjTracker);
                            }

                            seqNum++;
                        }
                    }
                }

                ServiceTouchedItems();
            }

            private void RemoveItemBySeqNumIfPresent(SetTrackerTracker setTrackerTracker, long itemSeqNum)
            {
                InnerObjectTracker innerObjTracker = setTrackerTracker.currentItemInnerObjTrackerListSortedBySeqNum.SafeTryGetValue(itemSeqNum);

                if (setTrackerTracker.currentItemInnerObjTrackerListSortedBySeqNum.TryGetValue(itemSeqNum, out innerObjTracker))
                {
                    setTrackerTracker.currentItemInnerObjTrackerListSortedBySeqNum.Remove(itemSeqNum);

                    if (innerObjTracker != null)
                    {
                        innerObjTracker.prevPair = innerObjTracker.pair;
                        innerObjTracker.pair = default(ItemSeqNumAndE039ObjectPair);

                        // these will only remain set if the object has actually been deleted.  Otherwise these values will be updated when the replacement object is added.
                        innerObjTracker.objLinksToListTouched = true;
                        innerObjTracker.objLinksFromListTouched = true;

                        NoteTouched(innerObjTracker);
                    }
                }
            }

            #endregion

            #region Service methods: ServiceTouchedItems, ServiceTouchedInnerObjectTrackerList, ServiceInnerObjectTracker

            private void ServiceTouchedItems()
            {
                while (ServiceTouchedInnerObjectTrackerList())
                { }
            }

            private bool ServiceTouchedInnerObjectTrackerList()
            {
                int iterationTouchedCount = touchedInnerObjectTrackerList.Count;

                if (iterationTouchedCount <= 0)
                    return false;

                for (int index = 0; index < iterationTouchedCount; index++)
                    ServiceInnerObjectTracker(touchedInnerObjectTrackerList[index]);

                touchedInnerObjectTrackerList.RemoveRange(0, iterationTouchedCount);

                return true;
            }

            private void ServiceInnerObjectTracker(InnerObjectTracker innerObjTracker, bool forceUpdateLinksToList = false, bool forceUpdateLinksFromList = false)
            {
                innerObjTracker.touched = false;

                var pair = innerObjTracker.pair;

                // handle updates to the links to list for this object
                if (innerObjTracker.objLinksToListTouched || forceUpdateLinksToList)
                {
                    innerObjTracker.objLinksToListTouched = false;
                    var linksToOthersList = ((pair.Object != null) ? pair.Object.LinksToOtherObjectsList : null) ?? Utils.Collections.ReadOnlyIList<E039Link>.Empty;

                    foreach (var objTracker in innerObjTracker.attachedObjectTrackerList)
                    {
                        foreach (var linkToTracker in objTracker.linkToObjectTrackerArray)
                        {
                            E039Link e039Link = linksToOthersList.FirstOrDefault(link => link.Key == linkToTracker.linkKey);
                            HandleLinkUpdate(linkToTracker, e039Link);
                        }
                    }
                }

                // handle updates to the links from list for this object
                if (innerObjTracker.objLinksFromListTouched || forceUpdateLinksFromList)
                {
                    innerObjTracker.objLinksFromListTouched = false;
                    var linksFromOthersList = ((pair.Object != null) ? pair.Object.LinksFromOtherObjectsList : null) ?? Utils.Collections.ReadOnlyIList<E039Link>.Empty;

                    foreach (var objTracker in innerObjTracker.attachedObjectTrackerList)
                    {
                        foreach (var linksFromTracker in objTracker.linksFromObjectsTrackerArray)
                        {
                            E039Link[] e039LinksArray = linksFromOthersList.Where(link => link.Key == linksFromTracker.linkKey).ToArray();
                            HandleLinksUpdate(linksFromTracker, e039LinksArray);
                        }
                    }
                }

                // publish this new pair to all of the attached trackers
                foreach (var objTracker in innerObjTracker.attachedObjectTrackerList)
                    objTracker.ItemSeqNumAndE039ObjectPair = pair;

                foreach (var linksToThis in innerObjTracker.attachedLinksToObjectTrackerList)
                    linksToThis.ItemSeqNumAndE039ObjectPair = pair;

                foreach (var linksFromThis in innerObjTracker.attachedLinksFromObjectsTrackerList)
                    linksFromThis.ProcessItemSeqNumAndE039ObjectPairUpdate(innerObjTracker, pair);

                // check if it is time to remove this inner object tracker (aka no one is using it any more)
                if (innerObjTracker.pair.Object == null && !innerObjTracker.AreAnyAttachementsActive)
                {
                    if (innerObjTracker.prevPair.Object != null && !innerObjTracker.prevPair.Object.Flags.IsSet(E039ObjectFlags.IsFinal))
                    {
                        releaseOfNonFinalObjectCount++;
                    }

                    innerObjTracker.tableTypeDictionary.objNameToTrackerDictionary.Remove(innerObjTracker.objectID.Name);

                    innerObjectTrackerFreeList.Release(ref innerObjTracker);
                }
            }

            private static volatile int releaseOfNonFinalObjectCount = 0;

            #endregion

            #region HandleLinkUpdate (linkTo and linksFrom variants)

            private void HandleLinkUpdate(E039LinkToObjectTracker linkToObjectTracker, E039Link e039Link)
            {
                if (!linkToObjectTracker.Link.Equals(e039Link))
                {
                    var unlinkFromInnerOT = linkToObjectTracker.linkToInnerObjectTracker;

                    if (unlinkFromInnerOT != null)
                    {
                        unlinkFromInnerOT.attachedLinksToObjectTrackerList.Remove(linkToObjectTracker);
                        NoteTouched(unlinkFromInnerOT);      // in case this object now needs to be removed

                        linkToObjectTracker.linkToInnerObjectTracker = null;
                    }

                    ItemSeqNumAndE039ObjectPair seqNumAndObjPair;

                    if (!e039Link.ToID.IsEmpty)
                    {
                        var linkToInnerOT = GetInnerObjectTracker(e039Link.ToID, null);

                        linkToObjectTracker.linkToInnerObjectTracker = linkToInnerOT;
                        linkToInnerOT.attachedLinksToObjectTrackerList.Add(linkToObjectTracker);

                        seqNumAndObjPair = linkToInnerOT.pair;
                    }
                    else
                    {
                        linkToObjectTracker.linkToInnerObjectTracker = null;
                        seqNumAndObjPair = emptyItemSeqNumAndE039ObjectPair;
                    }

                    linkToObjectTracker.Link = e039Link;
                    linkToObjectTracker.ItemSeqNumAndE039ObjectPair = seqNumAndObjPair;
                }
            }

            private static readonly ItemSeqNumAndE039ObjectPair emptyItemSeqNumAndE039ObjectPair = default(ItemSeqNumAndE039ObjectPair);

            private void HandleLinksUpdate(E039LinksFromObjectsTracker linksFromObjectsTracker, E039Link [] e039LinkArrayIn)
            {
                var e039LinkArrayInLength = e039LinkArrayIn.Length;
                var linksFromArray = linksFromObjectsTracker.Links;

                // if the incomming link array length is not equal to either the current Links array length or the linkedInnerObjectTrackerArray length then rebuild the arrays from scratch
                if (e039LinkArrayInLength != linksFromArray.Length || e039LinkArrayInLength != linksFromObjectsTracker.linkedFromInnerObjectTrackerArray.Length)
                {
                    foreach (var unlinkFromInnerOT in linksFromObjectsTracker.linkedFromInnerObjectTrackerArray)
                    {
                        unlinkFromInnerOT.attachedLinksFromObjectsTrackerList.Remove(linksFromObjectsTracker);
                        NoteTouched(unlinkFromInnerOT);      // in case this object now needs to be removed
                    }

                    var linkedFromInnerOTArray = e039LinkArrayIn.Select(link => GetInnerObjectTracker(link.FromID, null)).ToArray();
                    linksFromObjectsTracker.linkedFromInnerObjectTrackerArray = linkedFromInnerOTArray;
                    linkedFromInnerOTArray.DoForEach(linkedFromInnerOT => linkedFromInnerOT.attachedLinksFromObjectsTrackerList.Add(linksFromObjectsTracker));

                    linksFromObjectsTracker.Links = e039LinkArrayIn.SafeToArray();
                    linksFromObjectsTracker.PairArray = linkedFromInnerOTArray.Select(linkedFromInnerOT => linkedFromInnerOT.pair).ToArray();
                }
                else if (!linksFromArray.IsEqualTo(e039LinkArrayIn))
                {
                    var newLinkArray = new E039Link[e039LinkArrayInLength];
                    var newPairArray = new ItemSeqNumAndE039ObjectPair[e039LinkArrayInLength];
                    var currentPairArray = linksFromObjectsTracker.PairArray;

                    for (int index = 0; index < e039LinkArrayInLength; index++)
                    {
                        var newLink = e039LinkArrayIn[index];
                        var currentLink = linksFromArray[index];
                        var currentPair = currentPairArray[index];

                        if (!newLink.Equals(currentLink))
                        {
                            var unlinkFromInnerOT = linksFromObjectsTracker.linkedFromInnerObjectTrackerArray[index];
                            if (unlinkFromInnerOT != null)
                            {
                                unlinkFromInnerOT.attachedLinksFromObjectsTrackerList.Remove(linksFromObjectsTracker);
                                NoteTouched(unlinkFromInnerOT);      // in case this object now needs to be removed
                            }

                            var linkToInnerOT = GetInnerObjectTracker(newLink.FromID, null);
                            linksFromObjectsTracker.linkedFromInnerObjectTrackerArray[index] = linkToInnerOT;
                            linkToInnerOT.attachedLinksFromObjectsTrackerList.Add(linksFromObjectsTracker);

                            newLinkArray[index] = newLink;
                            newPairArray[index] = linkToInnerOT.pair;
                        }
                        else
                        {
                            newLinkArray[index] = currentLink;
                            newPairArray[index] = currentPair;
                        }
                    }

                    linksFromObjectsTracker.PairArray = newPairArray;
                    linksFromObjectsTracker.Links = newLinkArray;
                }
            }

            #endregion

            #region NoteTouched, GetInnerObjectTracker, GetTableTypeDictionary, CreateE039ObjectTracker

            /// <summary>
            /// This method is called repeatedly on <paramref name="innerObjTracker"/> instances to report that they have been changed and will need to be serviced.
            /// If the given object has not already been marked as touched then this method marks it touched and adds it to the touchedInnerObjectTrackerList.
            /// Otherwise this method does nothing.
            /// </summary>
            private void NoteTouched(InnerObjectTracker innerObjTracker)
            {
                if (!innerObjTracker.touched)
                {
                    innerObjTracker.touched = true;
                    touchedInnerObjectTrackerList.Add(innerObjTracker);
                }
            }

            internal InnerObjectTracker GetInnerObjectTracker(E039ObjectID objID, E039TableTypeDictionary tableTypeDictionary, bool createIfNeeded = true)
            {
                if (tableTypeDictionary == null)
                {
                    tableTypeDictionary = GetTableTypeDictionary(objID.Type, createIfNeeded: createIfNeeded);

                    if (tableTypeDictionary == null)
                        return null;
                }

                InnerObjectTracker objTracker = tableTypeDictionary.objNameToTrackerDictionary.SafeTryGetValue(objID.Name);

                if (objTracker == null && createIfNeeded)
                {
                    objTracker = innerObjectTrackerFreeList.Get();

                    objTracker.tableSetTracker = this;
                    objTracker.tableTypeDictionary = tableTypeDictionary;
                    objTracker.objectID = objID;

                    tableTypeDictionary.objNameToTrackerDictionary[objID.Name] = objTracker;
                }

                return objTracker;
            }

            internal E039TableTypeDictionary GetTableTypeDictionary(string typeName, bool createIfNeeded = true)
            {
                var tableTypeDictionary = typeNameToTableTypeDictionary.SafeTryGetValue(typeName);

                if (tableTypeDictionary == null && createIfNeeded)
                {
                    tableTypeDictionary = new E039TableTypeDictionary() { tableSetTracker = this, TypeName = typeName };
                    typeNameToTableTypeDictionary[typeName] = tableTypeDictionary;
                }

                return tableTypeDictionary;
            }

            internal E039ObjectTracker CreateE039ObjectTracker(InnerObjectTracker innerObjTracker, bool forSharedUse = true)
            {
                var objTracker = innerObjTracker.sharedObjectTracker;

                if (objTracker == null || !forSharedUse)
                {
                    objTracker = new E039ObjectTracker(this) 
                    {
                        isSharedInstance = forSharedUse,
                        innerObjectTracker = innerObjTracker, 
                        ObjectID = innerObjTracker.objectID, 
                        ItemSeqNumAndE039ObjectPair = innerObjTracker.pair 
                    };

                    innerObjTracker.attachedObjectTrackerList.Add(objTracker);

                    if (forSharedUse)
                        innerObjTracker.sharedObjectTracker = objTracker;
                }

                return objTracker;
            }

            #endregion

            #region internal classes: SetTrackerTracker, E039TableTypeDictionary, InnerObjectTracker

            /// <summary>
            /// This is the set of information that is needed to distribute incoming set deltas for a given SetID.
            /// </summary>
            internal class SetTrackerTracker
            {
                public SetID setID;
                public SetTracker setTracker;
                public Dictionary<string, E039TableTypeDictionary> typeDictionary = new Dictionary<string, E039TableTypeDictionary>();

                public SortedList<long, InnerObjectTracker> currentItemInnerObjTrackerListSortedBySeqNum = new SortedList<long, InnerObjectTracker>();
            }

            /// <summary>
            /// This is the internal table of all InnerObjectTrackers that are known for a given TypeName.
            /// It is also used to implment the externally usable IE039TableTypeDictionary
            /// </summary>
            internal class E039TableTypeDictionary : IE039TablePerTypeDictionary
            {
                public E039TableSetTracker tableSetTracker;

                public Dictionary<string, InnerObjectTracker> objNameToTrackerDictionary = new Dictionary<string, InnerObjectTracker>();

                public string TypeName { get; internal set; }

                public IList<E039ObjectID> ObjectIDs { get { return objNameToTrackerDictionary.Values.Select(tracker => tracker.objectID).ConvertToReadOnly(); } }
                public IList<IE039Object> Objects { get { return objNameToTrackerDictionary.Values.Select(tracker => tracker.pair.Object).ConvertToReadOnly(); } }
                public E039ObjectTracker this[string objectName] 
                { 
                    get { return tableSetTracker.CreateE039ObjectTracker(tableSetTracker.GetInnerObjectTracker(new E039ObjectID(objectName, TypeName, assignUUID: false), this)); } 
                }

                public E039ObjectTracker GetObjectTracker(string objectName, bool forTemporaryUse = false)
                {
                    return tableSetTracker.CreateE039ObjectTracker(tableSetTracker.GetInnerObjectTracker(new E039ObjectID(objectName, TypeName, assignUUID: false), this), forSharedUse: !forTemporaryUse); 
                }
            }

            /// <summary>
            /// This is the internal version of an object tracker.  In addition to retaining information about the object that is useful for recombining remove/add set delta records into single update records,
            /// It also serves as the hub for all object publication work.  All entities that are tracking a given object actually retain a reference to one of these objects and register their interest
            /// using one of the attachedYYYTrackerLists.  Then when the touched inner object trackers are being serviced, the updated object (pairs) will be passed to the attached trackers accordingly.
            /// <para/>Given its high usage ratio and its relative volatility in this class, instances of these object are used with a free list (innerObjectTrackerFreeList).
            /// </summary>
            internal class InnerObjectTracker
            {
                public E039TableSetTracker tableSetTracker;
                public E039TableTypeDictionary tableTypeDictionary;
                public E039ObjectID objectID;

                public ItemSeqNumAndE039ObjectPair pair;
                public ItemSeqNumAndE039ObjectPair prevPair;

                public bool touched;
                public bool objLinksToListTouched, objLinksFromListTouched;

                /// <summary>This is the list of E039ObjectTrackers that are attached to this InnerObjectTracker</summary>
                public List<E039ObjectTracker> attachedObjectTrackerList = new List<E039ObjectTracker>();

                /// <summary>This is the object tracker instance that will be used for this objectID when a client does not mind using a shared one (one that cannot be disposed and reclaimed later)</summary>
                public E039ObjectTracker sharedObjectTracker;

                /// <summary>This is the list of E039LinkToObjectTracker that are linked to, and attachd to, this InnerObjectTracker</summary>
                public List<E039LinkToObjectTracker> attachedLinksToObjectTrackerList = new List<E039LinkToObjectTracker>();

                /// <summary>This is the list of E039LinksFromObjectsTracker that are linked from, and attachd to, this InnerObjectTracker</summary>
                public List<E039LinksFromObjectsTracker> attachedLinksFromObjectsTrackerList = new List<E039LinksFromObjectsTracker>();

                /// <summary>Returns true if there are any attachments to this inner object tracker</summary>
                public bool AreAnyAttachementsActive { get { return (attachedObjectTrackerList.Count > 0 || attachedLinksToObjectTrackerList.Count > 0 || attachedLinksFromObjectsTrackerList.Count > 0); } }

                public void Clear()
                {
                    tableSetTracker = null;
                    tableTypeDictionary = null;
                    objectID = E039ObjectID.Empty;

                    pair = prevPair = default(ItemSeqNumAndE039ObjectPair);

                    touched = objLinksToListTouched = objLinksFromListTouched = false;

                    if (AreAnyAttachementsActive)
                    {
                        attachedObjectTrackerList.Clear();
                        attachedLinksToObjectTrackerList.Clear();
                        attachedLinksFromObjectsTrackerList.Clear();
                    }
                }
            }

            // add free list for inner object trackers - try to decrease the GC load when objects frequently go in and out of use.
            internal BasicFreeList<InnerObjectTracker> innerObjectTrackerFreeList = new BasicFreeList<InnerObjectTracker>() { FactoryDelegate = () => new InnerObjectTracker(), ClearDelegate = item => item.Clear(), MaxItemsToKeep = 100 };

            #endregion
        }

        /// <summary>
        /// Proxy interface applied to a E039TableSetTracker so that it can be used to produce IE039TableTypeDictionary instances for given typeName values.
        /// <para/>Provies a single string index property to obtain an IE039TableTypeDictionary intance for a given typeName
        /// </summary>
        public interface IE039TablePerTypeDictionaryFactory
        {
            IE039TablePerTypeDictionary this[string typeName] { get; }
        }

        /// <summary>
        /// This is the public interface for a dictionary of objects that is maintained by a source E039TableSetTracker.
        /// This interface is also used as a factory for E039ObjectTrackers using the object's objectName
        /// </summary>
        public interface IE039TablePerTypeDictionary
        {
            string TypeName { get; }
            E039ObjectTracker this[string objectName] { get; }
            E039ObjectTracker GetObjectTracker(string objectName, bool forTemporaryUse = false);
        }

        /// <summary>
        /// DependencyObject class used to allow a client to observe an IE039Object, based on its E039ObjectID.
        /// <para/>Supports the following DependencyPropertyKeys:
        /// <para/>ObjectID: gives E039ObjectID of the object that this tracker is tracking
        /// <para/>Object: gives the current IE039Object for the given key or null if the object has been deleted.
        /// <para/>Also supports a ObjectEventNotificationList event list that is notified with each newly observed object
        /// </summary>
        public class E039ObjectTracker : DependencyObject, IDisposable
        {
            internal E039ObjectTracker(E039TableSetTracker tableSetTracker) 
            {
                this.tableSetTracker = tableSetTracker;

                E039LinkToObjectTrackerFactory = new LinkToObjectTrackerFactory() { objTracker = this, tableSetTracker = tableSetTracker };
                E039LinksFromObjectsTrackerFactory = new LinksFromObjectsTrackerFactory { objTracker = this, tableSetTracker = tableSetTracker };

                _objectEventNotificationList.Source = this;
            }

            private E039TableSetTracker tableSetTracker;
            internal E039TableSetTracker.InnerObjectTracker innerObjectTracker;
            internal bool isSharedInstance;

            internal ItemSeqNumAndE039ObjectPair ItemSeqNumAndE039ObjectPair
            {
                set
                {
                    if (ObjectItemSeqNum != value.ItemSeqNum)
                    {
                        ObjectItemSeqNum = value.ItemSeqNum;
                        Object = value.Object;
                        _objectEventNotificationList.Notify(value.Object);
                    }
                }
            }

            internal long ObjectItemSeqNum { get; set; }

            public static readonly DependencyPropertyKey ObjectIDPropertyKey = DependencyProperty.RegisterReadOnly("ObjectID", typeof(E039ObjectID), typeof(E039ObjectTracker), new PropertyMetadata(E039ObjectID.Empty));
            public static readonly DependencyPropertyKey ObjectPropertyKey = DependencyProperty.RegisterReadOnly("Object", typeof(IE039Object), typeof(E039ObjectTracker), new PropertyMetadata(null));

            public E039ObjectID ObjectID { get { return _objectID; } internal set { SetValue(ObjectIDPropertyKey, _objectID = value); } }
            public E039ObjectID _objectID = E039ObjectID.Empty;

            public IE039Object Object { get { return _object; } private set { SetValue(ObjectPropertyKey, _object = value); } }
            private IE039Object _object = null;

            public IE039LinkToObjectTrackerFactory E039LinkToObjectTrackerFactory { get; private set; }
            public IE039LinksFromObjectsTrackerFactory E039LinksFromObjectsTrackerFactory { get; private set; }

            public IEventHandlerNotificationList<IE039Object> ObjectEventNotificationList { get { return _objectEventNotificationList; } }
            private EventHandlerNotificationList<IE039Object> _objectEventNotificationList = new EventHandlerNotificationList<IE039Object>();

            internal Dictionary<string, E039LinkToObjectTracker> linkToObjectTrackerByLinkKeyDictionary = new Dictionary<string, E039LinkToObjectTracker>();
            internal Dictionary<string, E039LinksFromObjectsTracker> linksFromObjectsTrackerByLinkKeyDictionary = new Dictionary<string, E039LinksFromObjectsTracker>();

            internal E039LinkToObjectTracker [] linkToObjectTrackerArray = Utils.Collections.EmptyArrayFactory<E039LinkToObjectTracker>.Instance;
            internal E039LinksFromObjectsTracker[] linksFromObjectsTrackerArray = Utils.Collections.EmptyArrayFactory<E039LinksFromObjectsTracker>.Instance;

            public void Dispose() 
            {
                if (!isSharedInstance)
                    tableSetTracker.HandleDispose(this); 
            }
            internal bool DisposeRequested { get; set; }

            private struct LinkToObjectTrackerFactory : IE039LinkToObjectTrackerFactory
            {
                public E039ObjectTracker objTracker;
                public E039TableSetTracker tableSetTracker;
                public E039LinkToObjectTracker this[string linkKey] { get { return tableSetTracker.HandleGetLinkToObjectTracker(objTracker, linkKey); } }
            }

            private struct LinksFromObjectsTrackerFactory : IE039LinksFromObjectsTrackerFactory
            {
                public E039ObjectTracker objTracker;
                public E039TableSetTracker tableSetTracker;
                public E039LinksFromObjectsTracker this[string linkKey] { get { return tableSetTracker.HandleGetLinksFromObjectsTracker(objTracker, linkKey); } }
            }
        }

        /// <summary>
        /// Proxy interface applied to a object tracker so that it can be used to produce E039LinkToObjectTracker instances for given linkKey values.
        /// </summary>
        public interface IE039LinkToObjectTrackerFactory
        {
            E039LinkToObjectTracker this[string linkKey] { get; }
        }

        /// <summary>
        /// Proxy interface applied to a object tracker so that it can be used to produce E039LinksFromsObjectTracker instances for given linkKey values.
        /// </summary>
        public interface IE039LinksFromObjectsTrackerFactory
        {
            E039LinksFromObjectsTracker this[string linkKey] { get; }
        }

        /// <summary>
        /// DependencyObject class used to allow a client to observe an IE039Object that has been linked to from a given source object (a specific one in its LinksToOtherObjectsList for a given link name)
        /// <para/>Supports the following DependencyPropertyKeys:
        /// <para/>Link: gives current LinkTo E039Link that matches the given linkKey from the source object against which this tracker was created.
        /// <para/>Object: gives the IE039Object this LinkTo Link is currently referencing, or null if the LinkTo Link is not established or if the target object is not currently available.
        /// <para/>Also supports a ObjectEventNotificationList event list that is notified with each newly observed object that the link points at.
        /// </summary>
        public class E039LinkToObjectTracker : DependencyObject
        {
            internal E039LinkToObjectTracker() 
            {
                _objectEventNotificationList.Source = this;
            }

            internal string linkKey;
            internal E039ObjectTracker e039ObjectTracker;
            internal E039TableSetTracker tableSetTracker;

            internal E039TableSetTracker.InnerObjectTracker linkToInnerObjectTracker;

            internal ItemSeqNumAndE039ObjectPair ItemSeqNumAndE039ObjectPair
            {
                set
                {
                    if (ObjectItemSeqNum != value.ItemSeqNum)
                    {
                        ObjectItemSeqNum = value.ItemSeqNum;
                        Object = value.Object;
                        _objectEventNotificationList.Notify(value.Object);
                    }
                }
            }

            internal long ObjectItemSeqNum { get; set; }
 
            public static readonly DependencyPropertyKey LinkPropertyKey = DependencyProperty.RegisterReadOnly("Link", typeof(E039Link), typeof(E039LinkToObjectTracker), new PropertyMetadata(E039Link.Empty));
            public static readonly DependencyPropertyKey ObjectPropertyKey = DependencyProperty.RegisterReadOnly("Object", typeof(IE039Object), typeof(E039LinkToObjectTracker), new PropertyMetadata(null));

            public E039Link Link { get { return _link; } internal set { SetValue(LinkPropertyKey, _link = value); } }
            private E039Link _link = E039Link.Empty;
 
            public IE039Object Object { get { return _object; } private set { SetValue(ObjectPropertyKey, _object = value); } }
            private IE039Object _object = null;

            public IEventHandlerNotificationList<IE039Object> ObjectEventNotificationList { get { return _objectEventNotificationList; } }
            private EventHandlerNotificationList<IE039Object> _objectEventNotificationList = new EventHandlerNotificationList<IE039Object>();
        }

        /// <summary>
        /// DependencyObject class used to allow a client to observe the set of IE039Objects that have links to a specific object (those in its LinksFromOtherObjectsList with a given link name)
        /// <para/>Supports the following DependencyPropertyKeys:
        /// <para/>Links: gives the set of LinkFrom E039Links that match the given linkKey from the source object against which this tracker was created.
        /// <para/>Objects: gives the set of IE039Objects the LinkFrom Links are currently referenceing.  This array may contain nulls if the corresonding LinkFrom link is not established or if the corresonding target object cannot currently be found.
        /// <para/>Also supports a ObjectsEventNotificationList event list that is notified with each newly observed set of objects that have links that point at a given original object.
        /// </summary>
        public class E039LinksFromObjectsTracker : DependencyObject
        {
            internal E039LinksFromObjectsTracker() 
            {
                _objectsEventNotificationList.Source = this;
            }

            internal string linkKey;
            internal E039ObjectTracker e039ObjectTracker;
            internal E039TableSetTracker tableSetTracker;

            internal E039TableSetTracker.InnerObjectTracker[] linkedFromInnerObjectTrackerArray = Utils.Collections.EmptyArrayFactory < E039TableSetTracker.InnerObjectTracker>.Instance;

            internal void ProcessItemSeqNumAndE039ObjectPairUpdate(E039TableSetTracker.InnerObjectTracker innerObjTracer, ItemSeqNumAndE039ObjectPair pairIn)
            {
                if (linkedFromInnerObjectTrackerArray.Any(item => Object.ReferenceEquals(item, innerObjTracer)))
                {
                    int objectArrayLen = linkedFromInnerObjectTrackerArray.Length;
                    IE039Object[] newObjArray = new IE039Object[objectArrayLen];
                    IE039Object[] currentObjArray = Objects;

                    for (int index = 0; index < objectArrayLen; index++)
                    {
                        var indexedInnerObjTracker = linkedFromInnerObjectTrackerArray.SafeAccess(index);
                        var indexedPair = _pairArray.SafeAccess(index);

                        if (object.ReferenceEquals(indexedInnerObjTracker, innerObjTracer) && indexedPair.ItemSeqNum != pairIn.ItemSeqNum)
                        {
                            _pairArray.SafePut(index, pairIn);
                            newObjArray[index] = pairIn.Object;
                        }
                        else
                        {
                            newObjArray[index] = currentObjArray.SafeAccess(index);
                        }
                    }

                    Objects = newObjArray;
                    FirstObject = newObjArray.SafeAccess(0);
                    _objectsEventNotificationList.Notify(newObjArray);
                }
            }

            internal ItemSeqNumAndE039ObjectPair[] PairArray
            {
                get { return _pairArray; }
                set
                {
                    if (_pairArray.Length != value.Length || !_pairArray.Zip(value, (a, b) => (a.ItemSeqNum == b.ItemSeqNum)).All(a => a))
                    {
                        _pairArray = value ?? Utils.Collections.EmptyArrayFactory<ItemSeqNumAndE039ObjectPair>.Instance;
                        var objArray = _pairArray.Select(pair => pair.Object).ToArray();
                        Objects = objArray;
                        FirstObject = objArray.FirstOrDefault();
                        _objectsEventNotificationList.Notify(objArray);
                    }
                }
            }
            private ItemSeqNumAndE039ObjectPair[] _pairArray = Utils.Collections.EmptyArrayFactory<ItemSeqNumAndE039ObjectPair>.Instance;

            public static readonly DependencyPropertyKey LinksPropertyKey = DependencyProperty.RegisterReadOnly("Links", typeof(E039Link []), typeof(E039LinksFromObjectsTracker), new PropertyMetadata(Utils.Collections.EmptyArrayFactory<E039Link>.Instance));
            public static readonly DependencyPropertyKey ObjectsPropertyKey = DependencyProperty.RegisterReadOnly("Objects", typeof(IE039Object[]), typeof(E039LinksFromObjectsTracker), new PropertyMetadata(Utils.Collections.EmptyArrayFactory<IE039Object>.Instance));
            public static readonly DependencyPropertyKey FirstObjectPropertyKey = DependencyProperty.RegisterReadOnly("FirstObject", typeof(IE039Object), typeof(E039LinksFromObjectsTracker), new PropertyMetadata(null));

            public E039Link[] Links { get { return _links; } internal set { SetValue(LinksPropertyKey, _links = value ?? Utils.Collections.EmptyArrayFactory<E039Link>.Instance); } }
            private E039Link[] _links = Utils.Collections.EmptyArrayFactory<E039Link>.Instance;

            public IE039Object[] Objects { get { return _objects; } private set { SetValue(ObjectsPropertyKey, _objects = value ?? Utils.Collections.EmptyArrayFactory<IE039Object>.Instance); } }
            private IE039Object[] _objects = Utils.Collections.EmptyArrayFactory<IE039Object>.Instance;

            public IE039Object FirstObject { get { return _firstObject; } private set { SetValue(FirstObjectPropertyKey, _firstObject = value); } }
            public IE039Object _firstObject;

            public IEventHandlerNotificationList<IE039Object[]> ObjectsEventNotificationList { get { return _objectsEventNotificationList; } }
            private EventHandlerNotificationList<IE039Object[]> _objectsEventNotificationList = new EventHandlerNotificationList<IE039Object[]>();
        }

        /// <summary>
        /// Internal object used to distribute ItemSeqNums with corresonding IE039Object instances.
        /// </summary>
        internal struct ItemSeqNumAndE039ObjectPair
        {
            public long ItemSeqNum { get; set; }
            public IE039Object Object { get; set; }
        }
    }

    #endregion

    #region E090 related helper objects

    namespace E090
    {
        #region IE090SubstLocAndSubstTrackerFactory, E090TableSetTracker, E090TableSetTrackerWithSubstLocListBuilder, E090CombinedSubstLocAndSubstInfoTracker, E090CombinedSubstLocAndSubstInfo

        /// <summary>
        /// Interface supported by E090TableSetTracker.  Used to generate E090CombinedSubstLocAndSubstInfoTracker objects for given SubstLoc names.
        /// </summary>
        public interface IE090SubstLocAndSubstTrackerFactory
        {
            /// <summary>
            /// This string indexed getter returns a new E090CombinedSubstLocAndSubstInfoTracker instance for the given <paramref name="substLocName"/>.
            /// This method is dictionary based.  
            /// If a <paramref name="substLocName"/> is requested more than once, the originally created instance for that <paramref name="substLocName"/> will be returned.
            /// </summary>
            E090CombinedSubstLocAndSubstInfoTracker this[string substLocName] { get; }
        }

        /// <summary>
        /// This object acts as the base for E090 centric WPF set observer related tools.  
        /// This object is constructed with the SetID of an E039Object set that contains E090 specific object types, namely SubstLoc and Substrate.
        /// This object is then used to create E090CombinedSubstLocAndSubstInfoTracker instances for given substrate location names.  
        /// </summary>
        public class E090TableSetTracker : DependencyObject, IE090SubstLocAndSubstTrackerFactory
        {
            #region Constant values (DefaultUpdateRate)

            /// <summary>Gives the default update rate for set trackers (5.0 Hz)</summary>
            public const double DefaultUpdateRate = 5.0;

            #endregion

            public E090TableSetTracker(string e090SetName, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
                : this(new SetID(e090SetName, generateUUIDForNull: false), isi: isi, defaultUpdateRate: defaultUpdateRate)
            { }

            public E090TableSetTracker(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
            {
                DefaultUsePostForCombinedInfoUpdates = true;

                E039TableSetTracker = Tools.Sets.E039.E039TableSetTracker.GetE039TableSetTracker(setID, isi: isi, defaultUpdateRate: defaultUpdateRate);
                SetTracker = E039TableSetTracker.SetTracker;
                SubstLocDict = E039TableSetTracker[Semi.E090.Constants.SubstrateLocationObjectType];
            }

            /// <summary>
            /// This property determines the default value for newly created E090CombinedSubstLocAndSubstInfoTracker's UsePostForCombinedInfoUpdates properties.
            /// UsePostForCombinedInfoUpdates is determines if the tracker immediately generates and sets its CombinedInfo DP on any newly observed object, or if it
            /// posts the internal update so as to generally be able to coallece changes to more than one observed object source into a single CombinedInfo update.
            /// <para/>Defaults to true.
            /// </summary>
            public bool DefaultUsePostForCombinedInfoUpdates { get; set; }

            protected Tools.Sets.E039.E039TableSetTracker E039TableSetTracker { get; private set; }
            protected SetTracker SetTracker { get; private set; }
            protected E039.IE039TablePerTypeDictionary SubstLocDict { get; private set; }

            private Dictionary<string, E090CombinedSubstLocAndSubstInfoTracker> substLocNameToCombinedInfoTrackerDictionary = new Dictionary<string, E090CombinedSubstLocAndSubstInfoTracker>();

            /// <summary>
            /// This string indexed getter returns a new E090CombinedSubstLocAndSubstInfoTracker instance for the given <paramref name="substLocName"/>.
            /// This method is dictionary based.  
            /// If a <paramref name="substLocName"/> is requested more than once, the originally created instance for that <paramref name="substLocName"/> will be returned.
            /// </summary>
            public E090CombinedSubstLocAndSubstInfoTracker this[string substLocName]
            {
                get 
                {
                    substLocName = substLocName.Sanitize();

                    var tracker = substLocNameToCombinedInfoTrackerDictionary.SafeTryGetValue(substLocName);
                    if (tracker == null)
                    {
                        tracker = new E090CombinedSubstLocAndSubstInfoTracker(substLocName, SubstLocDict)
                        {
                            UsePostForCombinedInfoUpdates = DefaultUsePostForCombinedInfoUpdates,
                        };

                        substLocNameToCombinedInfoTrackerDictionary[substLocName] = tracker;
                    }

                    return tracker;
                }
            }
        }

        /// <summary>
        /// This class is derived from the E090TableSetTracker and adds automatically reviewing all updates reported by the underlying SetTracker object and any time a new
        /// SubstLoc is reported as having been added, this object accumulates these and then reports the additions using a SubstLocNamesAddedNotificationList of event handlers.
        /// This pattern is generally used for cases where a control would like to dynamically create view items for substrate locations without knowing the full list of them at the start.
        /// </summary>
        public class E090TableSetTrackerWithSubstLocListBuilder : E090TableSetTracker
        {
            public E090TableSetTrackerWithSubstLocListBuilder(string e090SetName, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
                : this(new SetID(e090SetName, generateUUIDForNull: false), isi: isi, defaultUpdateRate: defaultUpdateRate)
            { }

            public E090TableSetTrackerWithSubstLocListBuilder(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate)
                : base(setID, isi, defaultUpdateRate)
            {
                _substLocNamesAddedNotificationList.Source = this;

                SetTracker.AddSetDeltasEventHandler(handler: (o, sd) => HandleSetDeltas(sd), setPrimingDelegate: sd => HandleSetDeltas(sd), delayPriming: true);
            }

            public IEventHandlerNotificationList<string[]> SubstLocNamesAddedNotificationList { get { return _substLocNamesAddedNotificationList; } }
            private EventHandlerNotificationList<string[]> _substLocNamesAddedNotificationList = new EventHandlerNotificationList<string[]>();

            private void HandleSetDeltas(ISetDelta setDelta)
            {
                if (setDelta == null)
                    return;

                int addedItemCount = 0;
                foreach (var addRangeItem in setDelta.AddRangeItems)
                {
                    foreach (var addedItem in addRangeItem.RangeObjects)
                    {
                        var e039Object = addedItem as IE039Object;
                        var e039ObjectID = (e039Object ?? E039Object.Empty).ID;
                        var e039ObjectName = e039ObjectID.Name;

                        if (e039ObjectID.Type == Semi.E090.Constants.SubstrateLocationObjectType && !knownSubstLocNamesSet.Contains(e039ObjectName))
                        {
                            addedItemCount++;
                            knownSubstLocNamesSet.Add(e039ObjectName);
                            pendingAddedSubstLocList.Add(e039ObjectName);
                        }
                    }
                }

                if (addedItemCount > 0 && !handlePendingAddedSubstLocItemsPosted)
                {
                    System.Threading.SynchronizationContext.Current.Post(o => HandlePendingAddedSubstLocItems(), this);
                    handlePendingAddedSubstLocItemsPosted = true;
                }
            }

            private void HandlePendingAddedSubstLocItems()
            {
                handlePendingAddedSubstLocItemsPosted = false;

                _substLocNamesAddedNotificationList.Notify(pendingAddedSubstLocList.ToArray());

                pendingAddedSubstLocList.Clear();
            }

            private HashSet<string> knownSubstLocNamesSet = new HashSet<string>();
            private List<string> pendingAddedSubstLocList = new List<string>();
            private bool handlePendingAddedSubstLocItemsPosted = false;
        }

        /// <summary>
        /// This is the object that is produced using an E090CombinedSubstLocAndSubstInfoTracker.  
        /// It contains an E090SubstLocInfo and a E090SubstInfo.  
        /// The E090SubstLocInfo gives the directly known information about a substrate location while the 
        /// E090SubstInfo gives the information about the substrate that is currently contained in this location, or which
        /// is the first substrate to indicate that the location is its source or destination location.  The SubstInfoFrom
        /// property indicates which substrate object link the SubstInfo contents was obtained from.
        /// </summary>
        public class E090CombinedSubstLocAndSubstInfo
        {
            /// <summary>
            /// Default constructor.
            /// </summary>
            internal E090CombinedSubstLocAndSubstInfo() 
            {
                SubstLocName = string.Empty;
                LinkedFromSrcLocObjSet = ReadOnlyIList<IE039Object>.Empty;
                LinkedFromDestLocObjSet = ReadOnlyIList<IE039Object>.Empty;
            }

            /// <summary>Gives an empty E090CombinedSubstLocAndSubstInfo object</summary>
            public static E090CombinedSubstLocAndSubstInfo Empty { get { return _empty; } }

            private static readonly E090CombinedSubstLocAndSubstInfo _empty = new E090CombinedSubstLocAndSubstInfo();

            /// <summary>This gives the originally specified substrate location name and is valid even if there is no such location in the system.</summary>
            public string SubstLocName { get; internal set; }

            /// <summary>Gives the extracted substrate location information for the substrate location being tracked and reported using this class.</summary>
            public Semi.E090.E090SubstLocInfo SubstLocInfo { get; internal set; }

            /// <summary>Indicates which substrate object link source was used to populate the SubstInfo contained here.</summary>
            public SubstInfoFrom SubstInfoFrom { get; internal set; }

            /// <summary>Gives the extracted substrate information for the substrate as obtained using the SubstInfoFrom link in relation to the substrate location that is being tracked and reported using this class.</summary>
            public Semi.E090.E090SubstInfo SubstInfo { get; internal set; }

            /// <summary>Gives the last (unmodified) substrate location object that was obaserved.  May be null.</summary>
            public IE039Object SubstLocObj { get; internal set; }

            /// <summary>Gives the last object that this substrate location is linked to via the Contains link.  May be null.</summary>
            public IE039Object LinkedToContainsObj { get; internal set; }

            /// <summary>Gives the set of objects that are linked to this substrate location via their SrcLoc links.  May be empty.</summary>
            public ReadOnlyIList<IE039Object> LinkedFromSrcLocObjSet { get; internal set; }

            /// <summary>Gives the set of objects that are linked to this substrate location via their DestLoc links.  May be empty.</summary>
            public ReadOnlyIList<IE039Object> LinkedFromDestLocObjSet { get; internal set; }

            /// <summary>Debugging and logging helper method</summary>
            public override string ToString()
            {
                switch (SubstInfoFrom)
                {
                    case E090.SubstInfoFrom.None:
                        return "{0} {1}".CheckedFormat(SubstLocInfo, SubstInfoFrom);
                    case E090.SubstInfoFrom.Contains:
                        return "{0} Contains {1}".CheckedFormat(SubstLocInfo, SubstInfo);
                    case E090.SubstInfoFrom.Source:
                        return "{0} IsSourceFor {1}".CheckedFormat(SubstLocInfo, SubstInfo);
                    case E090.SubstInfoFrom.Destination:
                        return "{0} IsDestinationFor {1}".CheckedFormat(SubstLocInfo, SubstInfo);
                    default:
                        return "{0} ?:{1} {2}".CheckedFormat(SubstLocInfo, SubstInfoFrom, SubstInfo);
                }
            }
        }

        /// <summary>
        /// Defines the set of substrate object sources for the SubstInfo property in a E090CombinedSubstLocAndSubstInfo object.
        /// <para/>None (0), Contains, Source, Destination
        /// </summary>
        public enum SubstInfoFrom : int
        {
            /// <summary>No source.  The SubstInfo is in its default state.</summary>
            None = 0,

            /// <summary>The SubstInfo represents the extracted information from the substrate object at the To end of the SubstLoc's Contains link.</summary>
            Contains,

            /// <summary>The SubstInfo represents the extracted information from the first substrate object that links to this SubstLoc using a Source Location (SrcLoc) link.</summary>
            Source,

            /// <summary>The SubstInfo represents the extracted information from the first substrate object that links to this SubstLoc using a Destination Location (DestLoc) link.</summary>
            Destination,
        }

        /// <summary>
        /// DependencyObject class used to allow a client to observe the decoded states for a set of cross-linked objects, starting at a substrate location.
        /// This object observes the given location and the Links to Contains and from SrcLoc and DestLoc and then uses the resulting observed set of objects
        /// to generate a dynamically updating E090CombinedSubstLocAndSubstInfo which is published using the CombinedInfo read only DependencyPropertyKey.
        /// <para/>Supports the following DependencyPropertyKeys:
        /// <para/>CombinedInfo: gives the E090CombinedSubstLocAndSubstInfo derived from the state of the location tracker and link to/from trackers that this object is observing.
        /// </summary>
        public class E090CombinedSubstLocAndSubstInfoTracker : DependencyObject
        {
            public E090CombinedSubstLocAndSubstInfoTracker(string substLocName, E039.IE039TablePerTypeDictionary substLocObjObserverFactory)
            {
                SubstLocName = substLocName.Sanitize();
                SubstLocID = new E039ObjectID(SubstLocName, Semi.E090.Constants.SubstrateLocationObjectType, assignUUID: false);

                UsePostForCombinedInfoUpdates = true;

                substLocObjTracker = substLocObjObserverFactory[SubstLocName];
                substLocContainsObjTracker = substLocObjTracker.E039LinkToObjectTrackerFactory[Semi.E090.Constants.ContainsLinkKey];
                srcLocLinkedObjTracker = substLocObjTracker.E039LinksFromObjectsTrackerFactory[Semi.E090.Constants.SourceLocationLinkKey];
                destLocLinkedObjTracker = substLocObjTracker.E039LinksFromObjectsTrackerFactory[Semi.E090.Constants.DestinationLocationLinkKey];

                substLocObjTracker.ObjectEventNotificationList.OnNotify += HandleNewSubstLocObj;
                substLocContainsObjTracker.ObjectEventNotificationList.OnNotify += HandleNewContainsSubstObj;
                srcLocLinkedObjTracker.ObjectsEventNotificationList.OnNotify += HandleNewFirstSrcLocLinkedObj;
                destLocLinkedObjTracker.ObjectsEventNotificationList.OnNotify += HandleNewFirstDestLocLinkedObj;

                substLocObj = substLocObjTracker.Object;
                containsSubstObj = substLocContainsObjTracker.Object;
                srcLocLinkedObjSet = srcLocLinkedObjTracker.Objects.ConvertToReadOnly();
                destLocLinkedObjSet = destLocLinkedObjTracker.Objects.ConvertToReadOnly();

                InnerUpdateCombinedInfo();
            }

            /// <summary>
            /// Gives the substrate location name that this tracker has been constructed to observe.
            /// </summary>
            public string SubstLocName { get; private set; }

            /// <summary>
            /// Gives the inferred substrate location object ID for the tracked substrate location.
            /// </summary>
            public E039ObjectID SubstLocID { get; private set; }

            /// <summary>
            /// UsePostForCombinedInfoUpdates is determines if the tracker immediately generates and sets its CombinedInfo DP on any newly observed object, or if it
            /// posts the internal update so as to generally be able to coallece changes to more than one observed object source into a single CombinedInfo update.
            /// <para/>Defaults to true.
            /// </summary>
            public bool UsePostForCombinedInfoUpdates { get; set; }

            private E039.E039ObjectTracker substLocObjTracker;
            private E039.E039LinkToObjectTracker substLocContainsObjTracker;
            private E039.E039LinksFromObjectsTracker srcLocLinkedObjTracker, destLocLinkedObjTracker;

            public static readonly DependencyPropertyKey CombinedInfoPropertyKey = DependencyProperty.RegisterReadOnly("CombinedInfo", typeof(E090CombinedSubstLocAndSubstInfo), typeof(E090CombinedSubstLocAndSubstInfoTracker), new PropertyMetadata(null));

            public E090CombinedSubstLocAndSubstInfo CombinedInfo { get { return _combinedInfo; } set { SetValue(CombinedInfoPropertyKey, (_combinedInfo = value)); } }
            private E090CombinedSubstLocAndSubstInfo _combinedInfo = E090CombinedSubstLocAndSubstInfo.Empty;

            public override string ToString()
            {
                return "Tracker {0}".CheckedFormat(_combinedInfo);
            }

            private void HandleNewSubstLocObj(object sender, IE039Object obj)
            {
                substLocObj = obj;

                UpdateCombinedInfoIfNeeded();
            }

            private void HandleNewContainsSubstObj(object sender, IE039Object obj)
            {
                containsSubstObj = obj;

                UpdateCombinedInfoIfNeeded();
            }

            private void HandleNewFirstSrcLocLinkedObj(object sender, IE039Object [] objArray)
            {
                srcLocLinkedObjSet = objArray.ConvertToReadOnly();

                UpdateCombinedInfoIfNeeded();
            }

            private void HandleNewFirstDestLocLinkedObj(object sender, IE039Object[] objArray)
            {
                destLocLinkedObjSet = objArray.ConvertToReadOnly();

                UpdateCombinedInfoIfNeeded();
            }

            private IE039Object substLocObj, containsSubstObj;
            private ReadOnlyIList<IE039Object> srcLocLinkedObjSet, destLocLinkedObjSet;

            private Semi.E090.E090SubstLocInfo substLocInfo;
            private Semi.E090.E090SubstInfo containsSubstInfo, firstSrcLocLinkedSubstInfo, firstDestLocLinkedSubstInfo;
            private SubstInfoFrom substInfoFrom;

            private void UpdateCombinedInfoIfNeeded()
            {
                totalUpdateCount++;

                if (!UsePostForCombinedInfoUpdates)
                {
                    InnerUpdateCombinedInfo();
                }
                else if (!updatePosted)
                {
                    updatePosted = true;
                    System.Threading.SynchronizationContext.Current.Post(o => { updatePosted = false; InnerUpdateCombinedInfo(); }, this);
                }
                else
                {
                    coallecedUpdateCount++;
                }
            }

            private bool updatePosted;
            private static int totalUpdateCount = 0;
            private static int coallecedUpdateCount = 0;

            /// <summary>
            /// This method consultes the contents of the set of E039Objects that are tracked here (substLocObj, containsSubstObj, firstSrcLocLinkedObj, firstDestLocLinkedObj)
            /// and updates the E090CombinedSubstLocAndSubstInfo based on the contents of these objects (when present).
            /// This logic includes determining where to obtain the E090SubstInfo from (none, contains, source, destination) and handling cases where the IsOccupied state of the
            /// SubstLocInfo does not match the presance of the last observed containsSubstObj.  
            /// At present the value of the containsSubstObj object will generally override the substLocInfo.LinkToSubst.ToID so that the reported IsOccupied value will generally
            /// be true if containsSubstObj != null. 
            /// </summary>
            private void InnerUpdateCombinedInfo()
            {
                if (substLocObj == null)
                    substLocInfo = new Semi.E090.E090SubstLocInfo(substLocObj) { ObjID = SubstLocID };
                else if (!Object.ReferenceEquals(substLocObj, substLocInfo.Obj))
                    substLocInfo = new Semi.E090.E090SubstLocInfo(substLocObj);

                if (!Object.ReferenceEquals(containsSubstInfo.Obj, containsSubstObj))
                    containsSubstInfo = new Semi.E090.E090SubstInfo(containsSubstObj);

                var firstSrcLocLinkedObj = srcLocLinkedObjSet.FirstOrDefault();
                var firstDestLocLinkedObj = destLocLinkedObjSet.FirstOrDefault();

                if (!Object.ReferenceEquals(firstSrcLocLinkedSubstInfo.Obj, firstSrcLocLinkedObj))
                    firstSrcLocLinkedSubstInfo = new Semi.E090.E090SubstInfo(firstSrcLocLinkedObj);

                if (!Object.ReferenceEquals(firstDestLocLinkedSubstInfo.Obj, firstDestLocLinkedObj))
                    firstDestLocLinkedSubstInfo = new Semi.E090.E090SubstInfo(firstDestLocLinkedObj);

                Semi.E090.E090SubstInfo selectedSubstInfo;

                if (containsSubstObj != null)
                {
                    // this location contains the given object
                    substInfoFrom = SubstInfoFrom.Contains;
                    selectedSubstInfo = containsSubstInfo;
                }
                else if (firstSrcLocLinkedObj != null && (firstDestLocLinkedObj == null || firstSrcLocLinkedSubstInfo.SPS == Semi.E090.SubstProcState.NeedsProcessing || firstSrcLocLinkedSubstInfo.SPS == Semi.E090.SubstProcState.Undefined))
                {
                    substInfoFrom = SubstInfoFrom.Source;
                    selectedSubstInfo = firstSrcLocLinkedSubstInfo;
                }
                else if (firstDestLocLinkedObj != null)
                {
                    substInfoFrom = SubstInfoFrom.Destination;
                    selectedSubstInfo = firstDestLocLinkedSubstInfo;
                }
                else
                {
                    // this location is neither a source nor a destination for any substrate and it is not currently occupied.
                    substInfoFrom = SubstInfoFrom.None;
                    selectedSubstInfo = Semi.E090.E090SubstInfo.Empty;
                }

                var combinedInfo = new E090CombinedSubstLocAndSubstInfo()
                    {
                        SubstLocName = SubstLocName,
                        SubstLocInfo = substLocInfo,
                        SubstInfoFrom = substInfoFrom,
                        SubstInfo = selectedSubstInfo,
                        SubstLocObj = substLocObj,
                        LinkedToContainsObj = containsSubstObj,
                        LinkedFromSrcLocObjSet = srcLocLinkedObjSet,
                        LinkedFromDestLocObjSet = destLocLinkedObjSet,
                    };

                bool replaceSubstLocInfoLink = (substInfoFrom == SubstInfoFrom.Contains && !substLocInfo.LinkToSubst.ToID.Equals(containsSubstInfo.ObjID));

                if (replaceSubstLocInfoLink)
                {
                    // otherwise the substLocInfo linkage is not consistent with the last object we observed to be contained here - update the link to match the observed contained substrate - covers both the occupied and unoccupied cases.
                    var fixedLinkToSubst = new Semi.E039.E039Link(substLocInfo.LinkToSubst) { ToID = containsSubstInfo.ObjID };

                    combinedInfo.SubstLocInfo = new Semi.E090.E090SubstLocInfo(substLocInfo) { LinkToSubst = fixedLinkToSubst };
                }

                CombinedInfo = combinedInfo;
            }
        }

        #endregion
    }

    #endregion
}
