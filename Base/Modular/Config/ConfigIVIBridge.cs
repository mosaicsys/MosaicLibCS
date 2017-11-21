//-------------------------------------------------------------------
/*! @file ConfigIVIBridge.cs
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

namespace MosaicLib.Modular.Config
{
	#region ConfigIVIBridgeConfig

    /// <summary>
    /// This object is the configuration object that is used with ConfigIVIBridge parts.  
    /// It contains all of the information that is needed to define how the bridge will be used.
    /// Generally this information contains optional fields to define the IValuesInterconnection and IConfig instances that will be used (when non-null),
    /// ...
    /// </summary>
    /// <remarks>
    /// IVI -> match using IVAPropagateMatchRuleSet -> inverseMap using IVAMapNameFromTo -> CKAMapNameFromTo -> GetConfigKeyAccess (ensure exists) (push value back on first lookup if it had existed)
    /// IConfig -> match using CKAPropagateMatchRuleSet -> invsereMap using CKAMapNameFromTo -> IVAMapNameFromTo -> GetValueAccessor and set with default value.
    /// </remarks>
    public class ConfigIVIBridgeConfig
    {
        public string PartID { get; set; }

        public TimeSpan MinSyncInterval { get; set; }

        public IValuesInterconnection PartBaseIVI { get; set; }

        public IValuesInterconnection IVI { get; set; }
        public IConfig Config { get; set; }

        public Logging.MesgType IssueLogMesgType { get; set; }
        public Logging.MesgType ValueUpdateTraceLogMesgType { get; set; }

        public bool UseEnsureExists { get; set; }
        public string DefaultConfigKeyProviderName { get; set; }

        /// <summary>applies to native IVA names for consideration and inclusion in sync set</summary>
        public MatchRuleSet IVAPropagateNameMatchRuleSet { get; set; }

        /// <summary>mapping definition used to map from native IVA names and mapped CKA names.  Only names that can be mapped both ways will be included.</summary>
        public IMapNameFromTo IVAMapNameFromTo { get; set; }

        /// <summary>applies in conjunction with CKAPropagateFilterPredicate to native CKA names for consideration and inclusion in sync set.</summary>
        public MatchRuleSet CKAPropagateKeyMatchRuleSet { get; set; }

        /// <summary>applies in conjuction with CKAPropagateKeyMatchRuleSet to CKA objects for consideration and inclusion in sync set.</summary>
        public ConfigKeyFilterPredicate CKAPropagateFilterPredicate { get; set; }

        /// <summary>mapping definition used to map from native CKA names to mapped IVA names.  Only names that can be mapped both ways will be included.</summary>
        public IMapNameFromTo CKAMapNameFromTo { get; set; }

        public ConfigIVIBridgeConfig(string partID)
        {
            PartID = partID;
            MinSyncInterval = (0.2).FromSeconds();
            IssueLogMesgType = Logging.MesgType.Error;
            ValueUpdateTraceLogMesgType = Logging.MesgType.Debug;
            UseEnsureExists = true;
        }

        public ConfigIVIBridgeConfig(ConfigIVIBridgeConfig other)
        {
            PartID = other.PartID;

            MinSyncInterval = other.MinSyncInterval.Clip(TimeSpan.Zero, TimeSpan.FromSeconds(0.2));

            IssueLogMesgType = other.IssueLogMesgType;
            ValueUpdateTraceLogMesgType = other.ValueUpdateTraceLogMesgType;
            UseEnsureExists = other.UseEnsureExists;
            DefaultConfigKeyProviderName = other.DefaultConfigKeyProviderName;

            PartBaseIVI = other.PartBaseIVI ?? other.IVI ?? Modular.Interconnect.Values.Values.Instance;
            IVI = other.IVI ?? Modular.Interconnect.Values.Values.Instance;
            Config = other.Config ?? Modular.Config.Config.Instance;

            if (other.IVAPropagateNameMatchRuleSet != null)
                IVAPropagateNameMatchRuleSet = new MatchRuleSet(other.IVAPropagateNameMatchRuleSet);
            IVAMapNameFromTo = other.IVAMapNameFromTo;

            if (other.CKAPropagateKeyMatchRuleSet != null)
                CKAPropagateKeyMatchRuleSet = new MatchRuleSet(other.CKAPropagateKeyMatchRuleSet);
            CKAPropagateFilterPredicate = other.CKAPropagateFilterPredicate;
            CKAMapNameFromTo = other.CKAMapNameFromTo;
        }
    }

	#endregion
	
	#region IConfigIVIBridge, ConfigIVIBridge

    public interface IConfigIVIBridge : IActivePartBase
    {
        /// <summary>
        /// Action Factory Method:  when the part is online, the returned action will perform a single iteration of a full synchronization bridge update step between the IVI and the IConfig instances between which this part is configured to replicate changes.
        /// </summary>
        IBasicAction Sync();
    }

    public class ConfigIVIBridge : SimpleActivePartBase, IConfigIVIBridge
    {
        #region Construction and related fields/properties

        public ConfigIVIBridge(ConfigIVIBridgeConfig config)
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion1.Build(waitTimeLimit: TimeSpan.FromSeconds((config.MinSyncInterval != TimeSpan.Zero) ? 0.05: 0.2), partBaseIVI : config.PartBaseIVI))
        {
            BridgeConfig = new ConfigIVIBridgeConfig(config);

            useNominalSyncHoldoffTimer = (BridgeConfig.MinSyncInterval != TimeSpan.Zero);
            if (useNominalSyncHoldoffTimer)
                nominalSyncHoldoffTimer.TriggerInterval = BridgeConfig.MinSyncInterval;

            IssueEmitter = Log.Emitter(BridgeConfig.IssueLogMesgType);
            ValueTraceEmitter = Log.Emitter(BridgeConfig.ValueUpdateTraceLogMesgType);

            IVI = BridgeConfig.IVI;         // BridgeConfig Copy constructor converts passed nulls to singleton instance
            Config = BridgeConfig.Config;   // BridgeConfig Copy constructor converts passed nulls to singleton instance

            IVI.NotificationList.AddItem(this);
            Config.ChangeNotificationList.AddItem(this);

            AddExplicitDisposeAction(() =>
                {
                    IVI.NotificationList.RemoveItem(this);
                    Config.ChangeNotificationList.RemoveItem(this);
                });

            PerformMainLoopService();
        }

        private ConfigIVIBridgeConfig BridgeConfig { get; set; }
        private IValuesInterconnection IVI { get; set; }
        private IConfig Config { get; set; }

        private Logging.IMesgEmitter IssueEmitter { get; set; }
        private Logging.IMesgEmitter ValueTraceEmitter { get; set; }

        #endregion

        #region IConfigIVIBridge interface and implementation

        public IBasicAction Sync()
        {
            return new BasicActionImpl(actionQ, PerformSync, CurrentMethodName, ActionLoggingReference);
        }

        private string PerformSync()
        {
            if (!BaseState.IsOnline)
                return "Part is not Online [{0}]".CheckedFormat(BaseState);

            ServiceBridge();

            return string.Empty;
        }

        #endregion

        #region SimpleActivePartBase override methods

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            string rc = base.PerformGoOnlineAction(andInitialize);

            ServiceBridge();

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
                ServiceBridge();
        }

        #endregion

        #region internal implemenation

        bool useNominalSyncHoldoffTimer = false;
        QpcTimer nominalSyncHoldoffTimer = new QpcTimer();

        private void ServiceBridge()
        {
            if (useNominalSyncHoldoffTimer)
                nominalSyncHoldoffTimer.Reset();

            // service IVI table additions:
            if (lastIVINamesArrayLength != IVI.ValueNamesArrayLength)
            {
                string[] iviValueNamesArray = IVI.ValueNamesArray ?? emptyStringArray;
                lastIVINamesArrayLength = iviValueNamesArray.Length;

                // check for new IVI additions
                foreach (string ivaNativeName in iviValueNamesArray)
                {
                    if (ivaNameToSyncItemDictionary.ContainsKey(ivaNativeName))
                        continue;

                    bool propagateIVAName = BridgeConfig.IVAPropagateNameMatchRuleSet.MatchesAny(ivaNativeName);

                    string mappedFromIVAName = ivaNativeName;

                    propagateIVAName &= (BridgeConfig.IVAMapNameFromTo == null || BridgeConfig.IVAMapNameFromTo.Map(ivaNativeName, ref mappedFromIVAName));

                    string mappedToCKAKeyName = mappedFromIVAName;
                    propagateIVAName &= (BridgeConfig.CKAMapNameFromTo == null || BridgeConfig.CKAMapNameFromTo.MapInverse(mappedFromIVAName, ref mappedToCKAKeyName));

                    // check if we should add a sync item for this key or if we should indicate that we have seen it and that it will not be synced
                    if (propagateIVAName)
                    {
                        IValueAccessor iva = IVI.GetValueAccessor(ivaNativeName);
                        IConfigKeyAccess cka = Config.GetConfigKeyAccess(new ConfigKeyAccessSpec() 
                                                                            { 
                                                                                Key = mappedToCKAKeyName,
                                                                                Flags = new ConfigKeyAccessFlags() { MayBeChanged = true, EnsureExists = BridgeConfig.UseEnsureExists, DefaultProviderName = BridgeConfig.DefaultConfigKeyProviderName }, 
                                                                            }
                                                                         , iva.VC);

                        AddSyncItemAndPerformInitialPropagation(iva, cka, ivaNativeName, null, mappedFromIVAName, mappedToCKAKeyName);
                    }
                    else
                    {
                        ivaNameToSyncItemDictionary[ivaNativeName] = null;
                    }
                }
            }

            // service CKA table addition:
            ConfigSubscriptionSeqNums configSeqNum = Config.SeqNums;

            if (lastConfigSeqNums.KeyAddedSeqNum != configSeqNum.KeyAddedSeqNum || lastConfigSeqNums.EnsureExistsSeqNum != configSeqNum.EnsureExistsSeqNum)
            {
                string[] configKeyNamesArray = Config.SearchForKeys();   // find all of the current keys

                foreach (string configKeyName in (configKeyNamesArray ?? emptyStringArray))
                {
                    if (configKeyNameToSyncItemDictionary.ContainsKey(configKeyName))
                        continue;

                    bool propagateConfigKeyName = BridgeConfig.CKAPropagateKeyMatchRuleSet.MatchesAny(configKeyName);

                    IConfigKeyAccess cka = (propagateConfigKeyName ? Config.GetConfigKeyAccess(new ConfigKeyAccessSpec() { Key = configKeyName, Flags = new ConfigKeyAccessFlags() { MayBeChanged = true } }) : null);

                    propagateConfigKeyName &= (BridgeConfig.CKAPropagateFilterPredicate == null || (cka != null && BridgeConfig.CKAPropagateFilterPredicate(cka.Key, cka.MetaData, cka.VC)));

                    string mappedFromConfigKeyName = configKeyName;
                    propagateConfigKeyName &= (BridgeConfig.CKAMapNameFromTo == null || BridgeConfig.CKAMapNameFromTo.MapInverse(configKeyName, ref mappedFromConfigKeyName));

                    string mappedToIVAName = mappedFromConfigKeyName;
                    propagateConfigKeyName &= (BridgeConfig.IVAMapNameFromTo == null || BridgeConfig.IVAMapNameFromTo.Map(mappedFromConfigKeyName, ref mappedToIVAName));

                    if (propagateConfigKeyName)
                    {
                        IValueAccessor iva = IVI.GetValueAccessor(mappedToIVAName);

                        AddSyncItemAndPerformInitialPropagation(iva, cka, mappedToIVAName, mappedFromConfigKeyName, null, configKeyName);
                    }
                    else
                    {
                        configKeyNameToSyncItemDictionary[configKeyName] = null;
                    }
                }
            }
            
            bool syncItemArrayUpdated = false;
            if (syncItemArray == null)
            {
                syncItemArray = syncItemList.ToArray();
                ivaArray = syncItemArray.Select(syncItem => syncItem.iva).ToArray();
            }

            // service existing IVA -> CKA items
            if (syncItemArrayUpdated || ivaArray.IsUpdateNeeded())
            {
                foreach (SyncItem syncItem in syncItemArray)
                {
                    if (syncItem.iva.IsUpdateNeeded)
                    {
                        ValueContainer vc = syncItem.iva.Update().VC;

                        if (!vc.IsEqualTo(syncItem.cka.VC))
                        {
                            ValueTraceEmitter.Emit("Propagating iva change '{0}' to cka '{1}'", syncItem.iva, syncItem.cka);
                            syncItem.cka.SetValue(vc, "{0}: Propagating value change from iva '{1}'".CheckedFormat(PartID, syncItem.iva), autoUpdate: false);
                        }
                        else
                        {
                            ValueTraceEmitter.Emit("iva '{0}' updated, value matches cka '{1}'", syncItem.iva, syncItem.cka);
                        }
                    }
                }
            }

            // service existing CKA -> IVA items
            if (lastConfigSeqNums.ChangeSeqNum != configSeqNum.ChangeSeqNum)
            {
                foreach (SyncItem syncItem in syncItemArray)
                {
                    if (syncItem.cka.UpdateValue())
                    {
                        ValueContainer vc = syncItem.cka.VC;

                        if (!vc.IsEqualTo(syncItem.iva.VC))
                        {
                            ValueTraceEmitter.Emit("Propagating cka change '{0}' to iva '{1}'", syncItem.cka, syncItem.iva);
                            syncItem.iva.Set(vc);
                        }
                        else
                        {
                            ValueTraceEmitter.Emit("cka '{0}' updated, value matches iva '{1}'", syncItem.cka, syncItem.iva);
                        }
                    }
                }
            }

            // update lastConfigSeqNum as a whole
            if (!lastConfigSeqNums.Equals(configSeqNum))
                lastConfigSeqNums = configSeqNum;
        }

        private SyncItem AddSyncItemAndPerformInitialPropagation(IValueAccessor iva, IConfigKeyAccess cka, string ivaLookupName, string ivaFromCkaMappedName, string ckaFromIvaMappedName, string ckaLookupKeyName)
        {
            SyncItem syncItem = new SyncItem() { iva = iva, cka = cka, ivaLookupName = ivaLookupName, ivaFromCkaMappedName = ivaFromCkaMappedName, ckaFromIvaMappedName = ckaFromIvaMappedName, ckaLookupKeyName = ckaLookupKeyName };

            syncItemList.Add(syncItem);
            syncItemArray = null;
            ivaArray = null;

            // add syncItem to both maps using both original names and found names, in case target is using name mapping and has applied it to this name.
            ivaNameToSyncItemDictionary[ivaLookupName] = syncItem;
            ivaNameToSyncItemDictionary[iva.Name] = syncItem;
            configKeyNameToSyncItemDictionary[ckaLookupKeyName] = syncItem;
            configKeyNameToSyncItemDictionary[cka.Key] = syncItem;

            ivaArray = null;

            if (cka.HasValue)
            {
                ValueContainer vc = cka.VC;
                if (!iva.VC.IsEqualTo(vc))
                {
                    ValueTraceEmitter.Emit("Propagating initial cka '{0}' to iva '{1}'", cka, iva);
                    iva.Set(vc);
                }
                else
                {
                    ValueTraceEmitter.Emit("Initial cka '{0}' matches initial iva '{1}'", cka, iva);
                }
            }
            else if (iva.HasValueBeenSet)
            {
                ValueContainer vc = iva.VC;
                ValueTraceEmitter.Emit("Propagating initial iva '{0}' to cka '{1}'", iva, cka);
                cka.SetValue(vc, "{0}: Propagating initial value from iva '{1}'".CheckedFormat(PartID, iva));
            }

            return syncItem;
        }

        private string[] lastIVINamesArray = emptyStringArray;
        private int lastIVINamesArrayLength = 0;

        private ConfigSubscriptionSeqNums lastConfigSeqNums = new ConfigSubscriptionSeqNums();

        private static readonly string[] emptyStringArray = new string[0];

        private class SyncItem
        {
            public string ivaLookupName;           // this is the name that was looked up to get the iva
            public string ivaFromCkaMappedName;    // this is the pseudo native IVA name (obtained by two level mapping from native cka name)
            public string ckaFromIvaMappedName;    // this is the pseudo native CKA name (obtained by two level mapping from native iva name)
            public string ckaLookupKeyName;        // this is the name that was looked up to get the cka.

            public IValueAccessor iva;             // the IVA for this sync item
            public IConfigKeyAccess cka;           // the CKA for this sync item
        }

        Dictionary<string, SyncItem> ivaNameToSyncItemDictionary = new Dictionary<string, SyncItem>();
        Dictionary<string, SyncItem> configKeyNameToSyncItemDictionary = new Dictionary<string, SyncItem>();

        private List<SyncItem> syncItemList = new List<SyncItem>();
        private SyncItem[] syncItemArray = null;
        private IValueAccessor[] ivaArray = null;

        #endregion
    }

	#endregion
}