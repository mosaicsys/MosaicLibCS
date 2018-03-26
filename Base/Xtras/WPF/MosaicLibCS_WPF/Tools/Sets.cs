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
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.WPF.Tools.Sets
{
    #region SetTracker

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
    public class SetTracker : DependencyObject
    {
        /// <summary>Gives the default update rate for set trackers</summary>
        public const double DefaultUpdateRate = 5.0;
        public const int DefaultMaximumItemsPerIteration = 0;

        #region static factory method : GetSetTracker(SetID)

        private static Dictionary<string, SetTracker> setNameToTrackerDictionary = new Dictionary<string, SetTracker>();
        private static Dictionary<string, SetTracker> setUUIDToTrackerDictionary = new Dictionary<string, SetTracker>();

        public static SetTracker GetSetTracker(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate, int defaultMaximumItemsPerIteration = DefaultMaximumItemsPerIteration)
        {
            SetTracker setTracker = null;
            string setName = setID.Name.Sanitize();

            if (!setID.UUID.IsNullOrEmpty())
                setTracker = setUUIDToTrackerDictionary.SafeTryGetValue(setID.UUID);
            else
                setTracker = setNameToTrackerDictionary.SafeTryGetValue(setName);

            if (setTracker == null && !setName.IsNullOrEmpty())
            {
                setTracker = new SetTracker(setID, isi: isi, defaultUpdateRate: defaultUpdateRate, defaultMaximumItemsPerIteration: defaultMaximumItemsPerIteration);

                if (!setNameToTrackerDictionary.ContainsKey(setName))
                    setNameToTrackerDictionary[setName] = setTracker;

                if (!setID.UUID.IsNullOrEmpty())
                    setUUIDToTrackerDictionary[setID.UUID] = setTracker;
            }

            return setTracker;
        }

        #endregion

        public SetTracker(SetID setID, ISetsInterconnection isi = null, double defaultUpdateRate = DefaultUpdateRate, int defaultMaximumItemsPerIteration = DefaultMaximumItemsPerIteration)
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

            System.Threading.SynchronizationContext.Current.Post((o) => AttemptToStartTracking(), this);
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

        public SetTracker AddSetDeltasEventHandler(EventHandlerDelegate<ISetDelta> handler, Action<ISetDelta> setPrimingDelegate = null)
        {
            if (_trackingSet != null && setPrimingDelegate != null)
            {
                var trackingSetSeqNumRangeInfo = _trackingSet.ItemListSeqNumRangeInfo;

                SetDelta<MosaicLib.Logging.ILogMessage> syntheticInitializationSetDelta = new SetDelta<MosaicLib.Logging.ILogMessage>()
                {
                    SetID = _trackingSet.SetID,
                    ClearSetAtStart = true,
                    SourceSetCapacity = _trackingSet.Capacity,
                    SourceUpdateState = _trackingSet.UpdateState,
                    addRangeItemList = new List<SetDeltaAddContiguousRangeItem<MosaicLib.Logging.ILogMessage>>()
                     {
                         new SetDeltaAddContiguousRangeItem<MosaicLib.Logging.ILogMessage>()
                         {
                             RangeStartIndex = 0,
                             RangeStartSeqNum = trackingSetSeqNumRangeInfo.First,
                             rangeObjectList = _trackingSet.SafeToSet<MosaicLib.Logging.ILogMessage>().ToList(),      // log messages are never sparse so we only need one contiguous range item for all of the current messages.
                         }
                     }
                };

                setPrimingDelegate(syntheticInitializationSetDelta);
            }

            SetDeltasEventNotificationList.OnNotify += handler;

            return this;
        }

        public void AttemptToStartTracking()
        {
            if (_referenceSet == null)
            {
                var foundReferenceSet = SetID.FindFirstSet(ISI);

                if (foundReferenceSet != null)
                {
                    ReferenceSet = foundReferenceSet;
                    TrackingSet = foundReferenceSet.CreateTrackingSet();
                }
            }

            if (sharedDispatcherTimerToken == null)
            {
                sharedDispatcherTimerToken = Timers.SharedDispatcherTimerFactory.GetAndStartSharedTimer(UpdateRate, ClientName, onTickNotifyDelegate: () => UpdateSet());
            }
        }

        QpcTimer relookupHoldoffTimer = new QpcTimer() { TriggerIntervalInSec = 1.0, AutoReset = true }.Start();

        public bool UpdateSet(bool forceFullUpdate = false)
        {
            if (_trackingSet == null)
            {
                if (relookupHoldoffTimer.IsTriggered)
                    AttemptToStartTracking();
            }
            else if (_trackingSet.IsUpdateNeeded || _trackingSet.IsUpdateInProgress || forceFullUpdate)
            {
                var setDelta = _trackingSet.PerformUpdateIteration(forceFullUpdate ? 0 : _maximumItemsPerIteration, generateSetDelta: true);

                _notifyOnSetUpdateList.Notify();
                _setDeltasEventNotificationList.Notify(setDelta);

                return true;
            }

            _setDeltasEventNotificationList.Notify(null);

            return false;
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
        public SetNewestItemPicker(string setName, Func<TItemType, TKeyType> keySelector, TKeyType keyValue)
            : this(new SetID(setName, generateUUIDForNull: false), keySelector, keyValue)
        { }

        public SetNewestItemPicker(SetID setID, Func<TItemType, TKeyType> keySelector, TKeyType keyValue)
        {
            this.setID = setID;
            this.keySelector = keySelector;
            this.keyValue = keyValue;

            rootSetTracker = SetTracker.GetSetTracker(setID);

            rootSetTracker.AddSetDeltasEventHandler(HandleOnNotifySetDeltasEvent, setPrimingDelegate: primingSetDelta => HandleOnNotifySetDeltasEvent(rootSetTracker, primingSetDelta));
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
            Modular.Interconnect.Sets.SetDelta<TItemType> setDelta = eventArgs as Modular.Interconnect.Sets.SetDelta<TItemType>;

            if (setDelta != null)
            {
                TItemType lastMatchedItem = default(TItemType);
                int matchCount = 0;

                var addRangeItemList = setDelta.addRangeItemList;
                int addRangeItemListCount = addRangeItemList.SafeCount();

                for (int addRangeIdx = 0; addRangeIdx < addRangeItemListCount; addRangeIdx++)
                {
                    var addRangeItem = addRangeItemList[addRangeIdx];

                    var rangeItemList = addRangeItem.rangeObjectList;
                    int rangeItemListCount = rangeItemList.SafeCount();

                    for (int rangeItemIdx = 0; rangeItemIdx < rangeItemListCount; rangeItemIdx++)
                    {
                        TItemType testItem = rangeItemList[rangeItemIdx];

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
        public SetNewestItemGroupPicker(string setName, Func<TItemType, TKeyType> keySelector, TKeyType[] groupKeyValuesArray)
            : this(new SetID(setName, generateUUIDForNull: false), keySelector, groupKeyValuesArray)
        { }

        /// <summary>
        /// Constructor.  Accepts <paramref name="setID"/>, <paramref name="keySelector"/> and <paramref name="groupKeyValuesArray"/>.
        /// Finds the SetTracker for the given <paramref name="setID"/> and builds a dictinary of key values to group item indecies which will be used to identify set items that are to be picked and where they are to be put into the array of group items.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Will be thrown if any of the given values in the <paramref name="groupKeyValuesArray"/> are null or are otherwise incompatible with the use of the Dictionary.Add method</exception>
        /// <exception cref="System.ArgumentException">Will be thrown if there are any duplicates in the given <paramref name="groupKeyValuesArray"/>, or if the Dictionary.Add method believes that they are duplicates.</exception>
        public SetNewestItemGroupPicker(SetID setID, Func<TItemType, TKeyType> keySelector, TKeyType[] groupKeyValuesArray)
        {
            this.setID = setID;
            this.keySelector = keySelector;
            this.groupKeyValuesArray = groupKeyValuesArray ?? Utils.Collections.EmptyArrayFactory<TKeyType>.Instance;

            this.groupKeyValuesArray.DoForEach((keyValue, idx) => keyToIndexDictionary.Add(keyValue, idx));

            rootSetTracker = SetTracker.GetSetTracker(setID);

            referenceGroupArray = new TItemType[maxGroupIndex = groupKeyValuesArray.Length];

            GroupArray = referenceGroupArray.SafeToArray();

            rootSetTracker.AddSetDeltasEventHandler(HandleOnNotifySetDeltasEvent, setPrimingDelegate: primingSetDelta => HandleOnNotifySetDeltasEvent(rootSetTracker, primingSetDelta));
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
            Modular.Interconnect.Sets.SetDelta<TItemType> setDelta = eventArgs as Modular.Interconnect.Sets.SetDelta<TItemType>;

            if (setDelta != null)
            {
                int matchCount = 0;

                var addRangeItemList = setDelta.addRangeItemList;
                int addRangeItemListCount = addRangeItemList.SafeCount();

                for (int addRangeIdx = 0; addRangeIdx < addRangeItemListCount; addRangeIdx++)
                {
                    var addRangeItem = addRangeItemList[addRangeIdx];

                    var rangeItemList = addRangeItem.rangeObjectList;
                    int rangeItemListCount = rangeItemList.SafeCount();

                    for (int rangeItemIdx = 0; rangeItemIdx < rangeItemListCount; rangeItemIdx++)
                    {
                        TItemType testItem = rangeItemList[rangeItemIdx];
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
                    GroupArray = referenceGroupArray.SafeToArray();
            }
        }

        private static DependencyPropertyKey GroupPropertyKey = DependencyProperty.RegisterReadOnly("GroupArray", typeof(TItemType[]), typeof(SetNewestItemGroupPicker<TItemType, TKeyType>), new PropertyMetadata(null));

        public TItemType [] GroupArray { get { return _groupArray; } set { SetValue(GroupPropertyKey, _groupArray = value); } }
        private TItemType [] _groupArray;
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
            : this(new SetID(setName, generateUUIDForNull: false), maximumCapacity: maximumCapacity, allowItemsToBeRemoved: allowItemsToBeRemoved)
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

        private void HandleNewSetID(SetID setID)
        {
            if (handleOnNotifySetDeltasEventHandler == null)
                handleOnNotifySetDeltasEventHandler = HandleOnNotifySetDeltasEvent;

            if (rootSetTracker != null)
                rootSetTracker.SetDeltasEventNotificationList.OnNotify -= handleOnNotifySetDeltasEventHandler;

            rootSetTracker = SetTracker.GetSetTracker(setID);

            setCollector = new TrackingSet<MosaicLib.Logging.ILogMessage>(setID, setType: SetType.Tracking, initialCapacity: maximumCapacity);

            RegenerateAdjustableSet();

            rootSetTracker.AddSetDeltasEventHandler(handleOnNotifySetDeltasEventHandler, setPrimingDelegate: primingSetDelta => setCollector.ApplyDeltas(primingSetDelta));
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

                    addItemFilter = (lm) => ((lm != null)
                                             && (useLogGate.IsTypeEnabled(lm.MesgType))
                                             && (sourceFilterHashSet == null || sourceFilterHashSet.Contains(lm.LoggerName))
                                             && (filterString.IsNullOrEmpty() || ((lm.MesgType.ToString().IndexOf(filterString, StringComparison.CurrentCultureIgnoreCase) >= 0) || lm.LoggerName.IndexOf(filterString, StringComparison.CurrentCultureIgnoreCase) >= 0) || (lm.Mesg.IndexOf(filterString, StringComparison.CurrentCultureIgnoreCase) >= 0))
                                             );

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
                    int itemsAdded = 0;

                    if (totalCollectedAddItems > 0)
                    {
                        collectedSetDeltaListWhilePaused.DoForEach(collectedSetDelta => setCollector.ApplyDeltas(collectedSetDelta));

                        itemsAdded += totalCollectedAddItems;

                        ClearCollectedSetDeltas();
                    }

                    if (eventArgs != null)
                    {
                        setCollector.ApplyDeltas(eventArgs);
                        itemsAdded = eventArgs.TotalAddedItemCount;
                    }

                    adjustableSet.PerformUpdateIteration();
                    TotalCount = setCollector.Count;

                    if (itemsAdded > 0)
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

        // properties to add: FilterString, EnabledSources and update ResetAdjustableSet to correctly implement these various filter options.

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
}
