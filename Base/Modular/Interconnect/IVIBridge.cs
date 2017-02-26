//-------------------------------------------------------------------
/*! @file IVIBridge.cs
 *  @brief Defines a SimpleActivePart derived object that can be used to connect an IVI table to a Modular.Config.IConfig instance.
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2016 Mosaic Systems Inc.  
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
using System.Reflection;
using System.Linq;

using MosaicLib;
using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;
using MosaicLib.Utils.StringMatching;
using MosaicLib.Time;

namespace MosaicLib.Modular.Interconnect.Values
{
	#region IVIBridgeConfig

    /// <summary>
    /// This object is the configuration object that is used with IVIBridge parts.  
    /// It contains all of the information that is needed to define how the bridge will be used.
    /// Generally this information contains optional fields to define the IValuesInterconnection instances that will be used (when non-null),
    /// ...
    /// </summary>
    /// <remarks>
    /// IVI1 -> match using IVA1PropagateMatchRuleSet -> inverseMap using IVA1MapNameFromTo -> IVA2MapNameFromTo -> IVI2
    /// IVI2 -> match using IVA2PropagateMatchRuleSet -> invsereMap using IVA2MapNameFromTo -> IVA1MapNameFromTo -> IVI1
    /// </remarks>
    public class IVIBridgeConfig
    {
        public string PartID { get; set; }

        public TimeSpan MinSyncInterval { get; set; }

        public IValuesInterconnection PartBaseIVI { get; set; }

        public IValuesInterconnection IVI1 { get; set; }
        public IValuesInterconnection IVI2 { get; set; }

        public Logging.MesgType IssueLogMesgType { get; set; }
        public Logging.MesgType ValueUpdateTraceLogMesgType { get; set; }

        public MatchRuleSet IVA1PropagateNameMatchRuleSet { get; set; }      // applies to native IVA names for consideration and inclusion in sync set
        public IMapNameFromTo IVA1MapNameFromTo { get; set; }    // mapping definition used to map from native IVA names and mapped CKA names.  Only names that can be mapped both ways will be included.

        public MatchRuleSet IVA2PropagateNameMatchRuleSet { get; set; }      // applies to native IVA names for consideration and inclusion in sync set
        public IMapNameFromTo IVA2MapNameFromTo { get; set; }    // mapping definition used to map from native IVA names and mapped CKA names.  Only names that can be mapped both ways will be included.

        public IVIBridgeConfig(string partID)
        {
            PartID = partID;
            IssueLogMesgType = Logging.MesgType.Error;
            ValueUpdateTraceLogMesgType = Logging.MesgType.Debug;
        }

        public IVIBridgeConfig(IVIBridgeConfig other)
        {
            PartID = other.PartID;

            MinSyncInterval = other.MinSyncInterval.Clip(TimeSpan.Zero, TimeSpan.FromSeconds(0.2));

            IssueLogMesgType = other.IssueLogMesgType;
            ValueUpdateTraceLogMesgType = other.ValueUpdateTraceLogMesgType;

            PartBaseIVI = other.PartBaseIVI ?? Modular.Interconnect.Values.Values.Instance;
            IVI1 = other.IVI1 ?? Modular.Interconnect.Values.Values.Instance;
            IVI2 = other.IVI2 ?? Modular.Interconnect.Values.Values.Instance;

            if (other.IVA1PropagateNameMatchRuleSet != null)
                IVA1PropagateNameMatchRuleSet = new MatchRuleSet(other.IVA1PropagateNameMatchRuleSet);
            IVA1MapNameFromTo = other.IVA1MapNameFromTo;

            if (other.IVA2PropagateNameMatchRuleSet != null)
                IVA2PropagateNameMatchRuleSet = new MatchRuleSet(other.IVA2PropagateNameMatchRuleSet);
            IVA2MapNameFromTo = other.IVA2MapNameFromTo;
        }
    }

	#endregion
	
	#region IIVIBridge, IVIBridge

    public interface IIVIBridge : IActivePartBase
    {
        /// <summary>
        /// Action Factory Method:  when the part is online, the returned action will perform a single iteration of a full synchronization bridge update step between the pair of IVIs between which this part is configured to replicate changes.
        /// </summary>
        IBasicAction Sync(SyncType syncType = SyncType.From1To2Then2To1);
    }

    /// <summary>
    /// Specifies what transfers are included in the sync (and in what order)
    /// 
    /// </summary>
    public enum SyncType : int
    {
        From1To2Then2To1 = 0,
        From2To1Then1To2,
        From1To2,
        From2To1,
    }

    public class IVIBridge : SimpleActivePartBase, IIVIBridge
    {
        #region Construction and related fields/properties

        public IVIBridge(IVIBridgeConfig config)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1.Build(waitTimeLimit: TimeSpan.FromSeconds((config.MinSyncInterval != TimeSpan.Zero) ? 0.05 : 0.2), partBaseIVI: config.PartBaseIVI))
        {
            BridgeConfig = new IVIBridgeConfig(config);

            useNominalSyncHoldoffTimer = (BridgeConfig.MinSyncInterval != TimeSpan.Zero);
            if (useNominalSyncHoldoffTimer)
                nominalSyncHoldoffTimer.TriggerInterval = BridgeConfig.MinSyncInterval;

            IssueEmitter = Log.Emitter(BridgeConfig.IssueLogMesgType);
            ValueTraceEmitter = Log.Emitter(BridgeConfig.ValueUpdateTraceLogMesgType);

            IVI1 = BridgeConfig.IVI1;   // BridgeConfig Copy constructor converts passed nulls to singleton instance
            IVI2 = BridgeConfig.IVI2;   // BridgeConfig Copy constructor converts passed nulls to singleton instance

            IVI1.NotificationList.AddItem(this);
            IVI2.NotificationList.AddItem(this);

            AddExplicitDisposeAction(() =>
                {
                    IVI1.NotificationList.RemoveItem(this);
                    IVI2.NotificationList.RemoveItem(this);
                });

            PerformMainLoopService();
        }

        private IVIBridgeConfig BridgeConfig { get; set; }
        private IValuesInterconnection IVI1 { get; set; }
        private IValuesInterconnection IVI2 { get; set; }

        private Logging.IMesgEmitter IssueEmitter { get; set; }
        private Logging.IMesgEmitter ValueTraceEmitter { get; set; }

        #endregion

        #region IIVIBridge interface and implementation

        public IBasicAction Sync(SyncType syncType = SyncType.From1To2Then2To1)
        {
            return new BasicActionImpl(actionQ, () => PerformSync(syncType), CurrentMethodName, ActionLoggingReference);
        }

        private string PerformSync(SyncType syncType = SyncType.From1To2Then2To1)
        {
            if (!BaseState.IsOnlineOrAttemptOnline)
                return "Part is not Online [{0}]".CheckedFormat(BaseState);

            ServiceBridge(syncType);

            return string.Empty;
        }

        #endregion

        #region SimpleActivePartBase override methods

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            string rc = base.PerformGoOnlineAction(andInitialize);

            PerformSync();

            return rc;
        }

        protected override string PerformServiceAction(string serviceName)
        {
            if (serviceName == "Sync")
                return PerformSync();

            return base.PerformServiceAction(serviceName);
        }

        protected override void PerformMainLoopService()
        {
            if (BaseState.IsOnline && (!useNominalSyncHoldoffTimer || nominalSyncHoldoffTimer.IsTriggered))
                ServiceBridge(SyncType.From1To2Then2To1);
        }

        #endregion

        #region internal implemenation

        bool useNominalSyncHoldoffTimer = false;
        QpcTimer nominalSyncHoldoffTimer = new QpcTimer();

        private void ServiceBridge(SyncType syncType, bool full = false)
        {
            if (useNominalSyncHoldoffTimer)
                nominalSyncHoldoffTimer.Reset();

            switch (syncType)
            {
                default:
                case SyncType.From1To2Then2To1:
                    Sync1To2(full);
                    Sync2To1(full);
                    break;
                case SyncType.From1To2:
                    Sync1To2(full);
                    break;
                case SyncType.From2To1:
                    Sync2To1(full);
                    break;
                case SyncType.From2To1Then1To2:
                    Sync2To1(full);
                    Sync1To2(full);
                    break;

            }
        }

        private void Sync1To2(bool full = false)
        {
            // service IVI1 table additions:
            if (lastIVI1NamesArrayLength != IVI1.ValueNamesArrayLength || full)
            {
                string[] ivi1ValueNamesArray = IVI1.ValueNamesArray ?? emptyStringArray;
                lastIVI1NamesArrayLength = ivi1ValueNamesArray.Length;

                // check for new IVI additions
                foreach (string iva1NativeName in ivi1ValueNamesArray)
                {
                    if (iva1NameToSyncItemDictionary.ContainsKey(iva1NativeName))
                        continue;

                    bool propagateIVA1Name = BridgeConfig.IVA1PropagateNameMatchRuleSet.MatchesAny(iva1NativeName);

                    string mappedFromIVAName = iva1NativeName;

                    propagateIVA1Name &= (BridgeConfig.IVA1MapNameFromTo == null || BridgeConfig.IVA1MapNameFromTo.Map(iva1NativeName, ref mappedFromIVAName));

                    string mappedToIVA2Name = mappedFromIVAName;
                    propagateIVA1Name &= (BridgeConfig.IVA2MapNameFromTo == null || BridgeConfig.IVA2MapNameFromTo.MapInverse(mappedFromIVAName, ref mappedToIVA2Name));

                    // check if we should add a sync item for this key or if we should indicate that we have seen it and that it will not be synced
                    if (propagateIVA1Name)
                    {
                        IValueAccessor iva1 = IVI1.GetValueAccessor(iva1NativeName);
                        IValueAccessor iva2 = IVI2.GetValueAccessor(mappedToIVA2Name);

                        AddSyncItemAndPerformInitialPropagation(iva1, iva2, iva1NativeName, null, mappedFromIVAName, mappedToIVA2Name);
                    }
                    else
                    {
                        iva1NameToSyncItemDictionary[iva1NativeName] = null;
                    }
                }
            }

            bool syncItemArrayUpdated = UpdateSyncItemAndIVAArraysIfNeeded();

            // service existing IVA1 -> IVA2 items
            if (syncItemArrayUpdated || iva1Array.IsUpdateNeeded() || full)
            {
                bool isAnySetPending = false;
                foreach (SyncItem syncItem in syncItemArray)
                {
                    if (syncItem.iva1.IsUpdateNeeded || full)
                    {
                        syncItem.iva2.ValueContainer = syncItem.iva1.Update().ValueContainer;

                        if (syncItem.iva2.IsSetPending)
                        {
                            ValueTraceEmitter.Emit("Propagation of changed iva1 '{0}' to iva2 '{1}' is pending", syncItem.iva1, syncItem.iva2);
                        }
                        else if (full)
                        {
                            syncItem.iva2.IsSetPending = true;
                            ValueTraceEmitter.Emit("Propagation of changed iva1 '{0}' to iva2 '{1}' is forced pending", syncItem.iva1, syncItem.iva2);
                        }
                        else
                        {
                            ValueTraceEmitter.Emit("Propagation of changed iva1 '{0}' to iva2 '{1}' is not pending (values match)", syncItem.iva1, syncItem.iva2);
                        }
                    }

                    isAnySetPending |= syncItem.iva2.IsSetPending;
                }

                if (isAnySetPending)
                {
                    IVI2.Set(iva2Array, optimize: true);

                    ValueTraceEmitter.Emit("Propagation of pending iva1 values to iva2 is complete");
                }
            }
        }

        private void Sync2To1(bool full = false)
        {
            // service IVI2 table additions:
            if (lastIVI2NamesArrayLength != IVI2.ValueNamesArrayLength || full)
            {
                string[] ivi2ValueNamesArray = IVI2.ValueNamesArray ?? emptyStringArray;
                lastIVI2NamesArrayLength = ivi2ValueNamesArray.Length;

                // check for new IVI additions
                foreach (string iva2NativeName in ivi2ValueNamesArray)
                {
                    if (iva2NameToSyncItemDictionary.ContainsKey(iva2NativeName))
                        continue;

                    bool propagateIVA2Name = BridgeConfig.IVA2PropagateNameMatchRuleSet.MatchesAny(iva2NativeName);

                    string mappedFromIVAName = iva2NativeName;

                    propagateIVA2Name &= (BridgeConfig.IVA2MapNameFromTo == null || BridgeConfig.IVA2MapNameFromTo.Map(iva2NativeName, ref mappedFromIVAName));

                    string mappedToIVA1Name = mappedFromIVAName;
                    propagateIVA2Name &= (BridgeConfig.IVA1MapNameFromTo == null || BridgeConfig.IVA1MapNameFromTo.MapInverse(mappedFromIVAName, ref mappedToIVA1Name));

                    // check if we should add a sync item for this key or if we should indicate that we have seen it and that it will not be synced
                    if (propagateIVA2Name)
                    {
                        IValueAccessor iva2 = IVI2.GetValueAccessor(iva2NativeName);
                        IValueAccessor iva1 = IVI1.GetValueAccessor(mappedToIVA1Name);

                        AddSyncItemAndPerformInitialPropagation(iva1, iva2, mappedToIVA1Name, mappedFromIVAName, null, iva2NativeName);
                    }
                    else
                    {
                        iva2NameToSyncItemDictionary[iva2NativeName] = null;
                    }
                }
            }

            bool syncItemArrayUpdated = UpdateSyncItemAndIVAArraysIfNeeded();

            // service existing IVA2 -> IVA1 items
            if (syncItemArrayUpdated || iva2Array.IsUpdateNeeded() || full)
            {
                bool isAnySetPending = false;
                foreach (SyncItem syncItem in syncItemArray)
                {
                    if (syncItem.iva2.IsUpdateNeeded || full)
                    {
                        syncItem.iva1.ValueContainer = syncItem.iva2.Update().ValueContainer;

                        if (syncItem.iva1.IsSetPending)
                        {
                            ValueTraceEmitter.Emit("Propagation of changed iva2 '{0}' to iva1 '{1}' is pending", syncItem.iva2, syncItem.iva1);
                        }
                        else if (full)
                        {
                            syncItem.iva1.IsSetPending = true;
                            ValueTraceEmitter.Emit("Propagation of changed iva2 '{0}' to iva1 '{1}' is forced pending", syncItem.iva2, syncItem.iva1);
                        }
                        else
                        {
                            ValueTraceEmitter.Emit("Propagation of changed iva2 '{0}' to iva1 '{1}' is not pending (values match)", syncItem.iva2, syncItem.iva1);
                        }
                    }
                
                    isAnySetPending |= syncItem.iva1.IsSetPending;
                }

                if (isAnySetPending)
                {
                    IVI1.Set(iva1Array, optimize: true);

                    ValueTraceEmitter.Emit("Propagation of pending iva2 values to iva1 is complete");
                }
            }
        }

        private bool UpdateSyncItemAndIVAArraysIfNeeded()
        {
            bool syncItemArrayUpdated = false;

            if (syncItemArray == null)
            {
                syncItemArray = syncItemList.ToArray();
                iva1Array = syncItemArray.Select(syncItem => syncItem.iva1).ToArray();
                iva2Array = syncItemArray.Select(syncItem => syncItem.iva2).ToArray();
                syncItemArrayUpdated = true;
            }

            return syncItemArrayUpdated;
        }

        private SyncItem AddSyncItemAndPerformInitialPropagation(IValueAccessor iva1, IValueAccessor iva2, string iva1LookupName, string iva1FromIVA2MappedName, string iva2FromIVA1MappedName, string iva2LookupName)
        {
            SyncItem syncItem = new SyncItem() { iva1 = iva1, iva2 = iva2, iva1LookupName = iva1LookupName, iva1FromIVA2MappedName = iva1FromIVA2MappedName, iva2FromIVA1MappedName = iva2FromIVA1MappedName, iva2LookupName = iva2LookupName };

            syncItemList.Add(syncItem);
            syncItemArray = null;
            iva1Array = null;

            // add syncItem to both maps using both original names and found names, in case target is using name mapping and has applied it to this name.
            iva1NameToSyncItemDictionary[iva1LookupName] = syncItem;
            iva1NameToSyncItemDictionary[iva1.Name] = syncItem;
            iva2NameToSyncItemDictionary[iva2LookupName] = syncItem;
            iva2NameToSyncItemDictionary[iva2.Name] = syncItem;

            iva1Array = null;
            iva2Array = null;

            if (iva1.HasValueBeenSet)
            {
                ValueContainer vc = iva1.ValueContainer;
                ValueTraceEmitter.Emit("Propagating initial iva1 '{0}' to iva2 '{1}'", iva1, iva2);
                iva2.Set(vc);
            }
            else if (iva2.HasValueBeenSet)
            {
                ValueContainer vc = iva2.ValueContainer;
                ValueTraceEmitter.Emit("Propagating initial iva2 '{0}' to iva1 '{1}'", iva2, iva1);
                iva1.Set(vc);
            }

            return syncItem;
        }

        private string[] lastIVI1NamesArray = emptyStringArray;
        private int lastIVI1NamesArrayLength = 0;

        private string[] lastIVI2NamesArray = emptyStringArray;
        private int lastIVI2NamesArrayLength = 0;

        private static readonly string[] emptyStringArray = new string[0];

        private class SyncItem
        {
            public string iva1LookupName;           // this is the name that was looked up to get the iva
            public string iva1FromIVA2MappedName;    // this is the pseudo native IVA name (obtained by two level mapping from native cka name)
            public string iva2FromIVA1MappedName;    // this is the pseudo native CKA name (obtained by two level mapping from native iva name)
            public string iva2LookupName;        // this is the name that was looked up to get the cka.

            public IValueAccessor iva1;             // the IVA for this sync item
            public IValueAccessor iva2;             // the IVA for this sync item
        }

        Dictionary<string, SyncItem> iva1NameToSyncItemDictionary = new Dictionary<string, SyncItem>();
        Dictionary<string, SyncItem> iva2NameToSyncItemDictionary = new Dictionary<string, SyncItem>();

        private List<SyncItem> syncItemList = new List<SyncItem>();
        private SyncItem[] syncItemArray = null;
        private IValueAccessor[] iva1Array = null;
        private IValueAccessor[] iva2Array = null;

        #endregion
    }

	#endregion
}