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
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
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

        public TimeSpan MinLookAtLaterInterval { get; set; }

        public IValuesInterconnection PartBaseIVI { get; set; }

        public IValuesInterconnection IVI { get; set; }
        public IConfig Config { get; set; }
        public IReferenceSet<ConfigKeyAccessCopy> ReferenceSet { get; set; }

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

        /// <summary>Selects the action logging config to be used by the ConfigBridge</summary>
        public ActionLoggingConfig ActionLoggingConfig { get; set; }

        /// <summary>When true (the default) the bridge will merge the config key's metadata into the corresponding IVA's meta data.</summary>
        public bool PropagateConfigKeyMetaDataToIVA { get; set; }

        public ConfigIVIBridgeConfig(string partID)
        {
            PartID = partID;
            MinSyncInterval = (0.2).FromSeconds();
            MinLookAtLaterInterval = (10.0).FromSeconds();
            IssueLogMesgType = Logging.MesgType.Error;
            ValueUpdateTraceLogMesgType = Logging.MesgType.Debug;
            UseEnsureExists = true;
            ActionLoggingConfig = ActionLoggingConfig.Debug_Debug_Trace_Trace;
            PropagateConfigKeyMetaDataToIVA = true;
        }

        public ConfigIVIBridgeConfig(ConfigIVIBridgeConfig other)
        {
            PartID = other.PartID;

            MinSyncInterval = other.MinSyncInterval.Clip(TimeSpan.Zero, TimeSpan.FromSeconds(0.2));
            MinLookAtLaterInterval = other.MinLookAtLaterInterval;

            IssueLogMesgType = other.IssueLogMesgType;
            ValueUpdateTraceLogMesgType = other.ValueUpdateTraceLogMesgType;
            UseEnsureExists = other.UseEnsureExists;
            DefaultConfigKeyProviderName = other.DefaultConfigKeyProviderName;

            PartBaseIVI = other.PartBaseIVI ?? other.IVI ?? Modular.Interconnect.Values.Values.Instance;
            IVI = other.IVI ?? Modular.Interconnect.Values.Values.Instance;
            Config = other.Config ?? Modular.Config.Config.Instance;
            ReferenceSet = other.ReferenceSet;

            if (other.IVAPropagateNameMatchRuleSet != null)
                IVAPropagateNameMatchRuleSet = new MatchRuleSet(other.IVAPropagateNameMatchRuleSet);
            IVAMapNameFromTo = other.IVAMapNameFromTo;

            if (other.CKAPropagateKeyMatchRuleSet != null)
                CKAPropagateKeyMatchRuleSet = new MatchRuleSet(other.CKAPropagateKeyMatchRuleSet);
            CKAPropagateFilterPredicate = other.CKAPropagateFilterPredicate;
            CKAMapNameFromTo = other.CKAMapNameFromTo;

            ActionLoggingConfig = other.ActionLoggingConfig;
            PropagateConfigKeyMetaDataToIVA = other.PropagateConfigKeyMetaDataToIVA;
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
            : base(config.PartID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(waitTimeLimit: TimeSpan.FromSeconds((config.MinSyncInterval != TimeSpan.Zero) ? 0.05: 0.2), partBaseIVI : config.PartBaseIVI, disableBusyBehavior: true))
        {
            BridgeConfig = new ConfigIVIBridgeConfig(config);

            ActionLoggingReference.Config = BridgeConfig.ActionLoggingConfig;

            useNominalSyncHoldoffTimer = (BridgeConfig.MinSyncInterval != TimeSpan.Zero);
            if (useNominalSyncHoldoffTimer)
                nominalSyncHoldoffTimer.TriggerInterval = BridgeConfig.MinSyncInterval;

            IssueEmitter = Log.Emitter(BridgeConfig.IssueLogMesgType);
            ValueTraceEmitter = Log.Emitter(BridgeConfig.ValueUpdateTraceLogMesgType);

            IVI = BridgeConfig.IVI;         // BridgeConfig Copy constructor converts passed nulls to singleton instance
            Config = BridgeConfig.Config;   // BridgeConfig Copy constructor converts passed nulls to singleton instance
            ReferenceSet = BridgeConfig.ReferenceSet;

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
        private IReferenceSet<ConfigKeyAccessCopy> ReferenceSet { get; set; }

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

            ServiceBridge(forSync: true);

            return string.Empty;
        }

        #endregion

        #region SimpleActivePartBase override methods (PerformGoOnlineAction, PerformServiceAction, PerformMainLoopService)

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            string rc = base.PerformGoOnlineAction(andInitialize);

            ServiceBridge();

            return rc;
        }

        protected override string PerformServiceActionEx(IProviderFacet ipf, string serviceName, INamedValueSet npv)
        {
            switch (serviceName)
            {
                case "Sync": 
                    return PerformSync();

                case "SetKey":
                    {
                        string key = npv["key"].VC.GetValue<string>(rethrow: true);
                        ValueContainer value = npv["value"].VC;
                        string comment = npv["comment"].VC.GetValue<string>(rethrow: false, defaultValue: null).MapNullTo("{0} operation has been performed using the {1} part".CheckedFormat(serviceName, PartID));
                        bool? ensureExists = npv["ensureExists"].VC.GetValue<bool?>(rethrow: false);

                        IConfigKeyAccess icka = Config.GetConfigKeyAccess(key, ensureExists: ensureExists, defaultValue: value);

                        if (icka == null)
                            return "Internal: GetConfigKeyAccess generated null ICKA for key '{0}'".CheckedFormat(key);

                        if (!icka.IsUsable)
                            return "Key lookup for '{0}' gave error: {1}".CheckedFormat(key, icka.ResultCode);

                        string ec = icka.SetValue(value, commentStr: comment);

                        ServiceBridge();

                        return ec.MapNullTo("[Internal: final ec was null]");
                    }

                case "SetKeys":
                    {
                        string [] keys = npv["keys"].VC.GetValue<string []>(rethrow: true);
                        ValueContainer [] values = npv["values"].VC.GetValue<ValueContainer []>(rethrow: true);
                        string comment = npv["comment"].VC.GetValue<string>(rethrow: false, defaultValue: null).MapNullTo("{0} operation has been performed using the {1} part".CheckedFormat(serviceName, PartID));
                        bool? ensureExists = npv["ensureExists"].VC.GetValue<bool?>(rethrow: false);

                        if (keys.SafeLength() != values.SafeLength())
                            return "Given keys and values were not the same length";

                        if (keys.SafeLength() == 0)
                            return "No keys were given to change";

                        IConfigKeyAccess[] ickaArray = keys.Select((key, idx) => Config.GetConfigKeyAccess(key, ensureExists: ensureExists, defaultValue: values[idx])).ToArray(); 

                        if (ickaArray.Any(icka => icka == null))
                            return "Internal: GetConfigKeyAccess generated null ICKA for one or more keys in [{0}]".CheckedFormat(string.Join(",", keys));

                        var notFoundKeys = ickaArray.Where(icka => !icka.IsUsable).Select(icka => icka.Key).ToArray();
                        if (!notFoundKeys.IsNullOrEmpty())
                            return "Key lookup for keys '{0}' failed".CheckedFormat(string.Join(",", notFoundKeys));

                        string ec = Config.SetValues(ickaArray.Select((icka, idx) => KVP.Create(icka, values[idx])).ToArray(), comment);

                        ServiceBridge();

                        return ec.MapNullTo("[Internal: final ec was null]");
                    }

                default: 
                    return base.PerformServiceAction(serviceName);
            }
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

        Dictionary<string, AttemptToAddSyncItemForIVAInfo> lookAtLaterDictionary = new Dictionary<string, AttemptToAddSyncItemForIVAInfo>();
        bool useLookAtLaterTimer = false;
        QpcTimer lookAtLaterTimer = new QpcTimer();

        private struct AttemptToAddSyncItemForIVAInfo
        {
            public string ivaNativeName;
            public string mappedFromIVAName;
            public string mappedToCKAKeyName;
            public IValueAccessor iva;
        }

        private void ServiceBridge(bool forSync = false)
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
                    if (ivaNameToSyncItemDictionary.ContainsKey(ivaNativeName) || lookAtLaterDictionary.ContainsKey(ivaNativeName))
                        continue;

                    bool propagateIVAName = BridgeConfig.IVAPropagateNameMatchRuleSet.MatchesAny(ivaNativeName);

                    string mappedFromIVAName = ivaNativeName;

                    propagateIVAName &= (BridgeConfig.IVAMapNameFromTo == null || BridgeConfig.IVAMapNameFromTo.Map(ivaNativeName, ref mappedFromIVAName));

                    string mappedToCKAKeyName = mappedFromIVAName;
                    propagateIVAName &= (BridgeConfig.CKAMapNameFromTo == null || BridgeConfig.CKAMapNameFromTo.MapInverse(mappedFromIVAName, ref mappedToCKAKeyName));

                    // check if we should add a sync item for this key or if we should indicate that we have seen it and that it will not be synced
                    if (propagateIVAName)
                    {
                        var attemptToAddSyncItemInfo = new AttemptToAddSyncItemForIVAInfo()
                        {
                            ivaNativeName = ivaNativeName,
                            mappedFromIVAName = mappedFromIVAName,
                            mappedToCKAKeyName = mappedToCKAKeyName,
                            iva = IVI.GetValueAccessor(ivaNativeName),
                        };

                        if (!AttemptToAddSyncItemForIVA(attemptToAddSyncItemInfo, requireUpdateNeeded: false))
                        {
                            lookAtLaterDictionary[ivaNativeName] = attemptToAddSyncItemInfo;

                            Log.Debug.Emit("IVA [{0}] has been added to look at later list", attemptToAddSyncItemInfo.iva);

                            useLookAtLaterTimer = !BridgeConfig.MinLookAtLaterInterval.IsZero();
                            if (useLookAtLaterTimer)
                                lookAtLaterTimer.StartIfNeeded(BridgeConfig.MinLookAtLaterInterval);
                        }
                    }
                    else
                    {
                        ivaNameToSyncItemDictionary[ivaNativeName] = null;
                    }
                }
            }

            // if we have lookAtLater items and the corresponding timer has triggered then 
            if (lookAtLaterDictionary.Count > 0 && (!useLookAtLaterTimer || lookAtLaterTimer.IsTriggered || forSync))
            {
                foreach (var item in lookAtLaterDictionary.Values.ToArray())
                {
                    if (AttemptToAddSyncItemForIVA(item, requireUpdateNeeded: true))
                    {
                        Log.Debug.Emit("IVA [{0}] has been removed from the look at later list", item.iva);

                        lookAtLaterDictionary.Remove(item.ivaNativeName);
                    }
                }

                if (lookAtLaterDictionary.Count == 0)
                    lookAtLaterTimer.Stop();
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
                foreach (var syncItem in syncItemArray)
                {
                    if (syncItem.iva.IsUpdateNeeded)
                    {
                        ValueContainer vc = syncItem.iva.Update().VC;

                        if (!vc.IsEqualTo(syncItem.icka.VC))
                        {
                            ValueTraceEmitter.Emit("Propagating iva change '{0}' to cka '{1}'", syncItem.iva, syncItem.icka);
                            syncItem.icka.SetValue(vc, "{0}: Propagating value change from iva '{1}'".CheckedFormat(PartID, syncItem.iva), autoUpdate: false);
                            syncItem.UpdateCopyInSet(ReferenceSet);
                        }
                        else
                        {
                            ValueTraceEmitter.Emit("iva '{0}' updated, value matches cka '{1}'", syncItem.iva, syncItem.icka);
                        }
                    }
                }
            }

            // service existing CKA -> IVA items
            bool serviceMDChanges = (lastConfigSeqNums.KeyMetaDataChangeSeqNum != configSeqNum.KeyMetaDataChangeSeqNum && BridgeConfig.PropagateConfigKeyMetaDataToIVA);

            if (lastConfigSeqNums.ChangeSeqNum != configSeqNum.ChangeSeqNum || serviceMDChanges)
            {
                foreach (var syncItem in syncItemArray)
                {
                    int entryMDSeqNum = syncItem.icka.MetaDataSeqNum;
                    int entryValueSeqNum = syncItem.icka.ValueSeqNum;

                    if (syncItem.icka.UpdateValue())
                    {
                        ValueContainer vc = syncItem.icka.VC;

                        if (!vc.IsEqualTo(syncItem.iva.VC))
                        {
                            ValueTraceEmitter.Emit("Propagating cka change '{0}' to iva '{1}'", syncItem.icka, syncItem.iva);
                            syncItem.iva.Set(vc);
                            syncItem.UpdateCopyInSet(ReferenceSet);

                            if (BridgeConfig.PropagateConfigKeyMetaDataToIVA)
                                syncItem.ServiceMDPropagation(ValueTraceEmitter);
                        }
                        else
                        {
                            if (entryValueSeqNum != syncItem.icka.ValueSeqNum)
                                ValueTraceEmitter.Emit("cka '{0}' updated, value already matched iva '{1}'", syncItem.icka, syncItem.iva);

                            if (BridgeConfig.PropagateConfigKeyMetaDataToIVA)
                                syncItem.ServiceMDPropagation(ValueTraceEmitter);
                        }
                    }
                }
            }

            // update lastConfigSeqNum as a whole
            if (!lastConfigSeqNums.Equals(configSeqNum))
                lastConfigSeqNums = configSeqNum;
        }

        /// <summary>
        /// This method contains the work required to verify that an IVA is ready for a corresponding CKA before attempting to find and/or create the CKA.
        /// This method returns true if it created and added a sync item for the IVA/CKA pair, otherwise it returns false.
        /// if the requireUpdateNeeded flag is true then the method will only attempt to even check if the IVA is ready if its IsUpdateNeeded flag is set (aka it has been set since we last checked)
        /// </summary>
        private bool AttemptToAddSyncItemForIVA(AttemptToAddSyncItemForIVAInfo item, bool requireUpdateNeeded = true)
        {
            if (requireUpdateNeeded && !item.iva.IsUpdateNeeded)
                return false;

            item.iva.Update();

            // we only attempt to propagate items to config that have been explicitly set and which currently have a non-empty value.
            if (item.iva.HasValueBeenSet && !item.iva.VC.IsEmpty)
            {
                IConfigKeyAccess cka = Config.GetConfigKeyAccess(new ConfigKeyAccessSpec()
                    {
                        Key = item.mappedToCKAKeyName,
                        Flags = new ConfigKeyAccessFlags() { MayBeChanged = true, EnsureExists = BridgeConfig.UseEnsureExists, DefaultProviderName = BridgeConfig.DefaultConfigKeyProviderName },
                    }, item.iva.VC);

                AddSyncItemAndPerformInitialPropagation(item.iva, cka, item.ivaNativeName, null, item.mappedFromIVAName, item.mappedToCKAKeyName);

                return true;
            }
            else
            {
                return false;
            }
        }

        private SyncItem AddSyncItemAndPerformInitialPropagation(IValueAccessor iva, IConfigKeyAccess cka, string ivaLookupName, string ivaFromCkaMappedName, string ckaFromIvaMappedName, string ckaLookupKeyName)
        {
            SyncItem syncItem = new SyncItem() 
            { 
                iva = iva, 
                icka = cka, 
                ivaLookupName = ivaLookupName, 
                ivaFromCkaMappedName = ivaFromCkaMappedName, 
                ckaFromIvaMappedName = ckaFromIvaMappedName, 
                ckaLookupKeyName = ckaLookupKeyName 
            };

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

            if (BridgeConfig.PropagateConfigKeyMetaDataToIVA)
                syncItem.ServiceMDPropagation(ValueTraceEmitter, force: true);

            syncItem.UpdateCopyInSet(ReferenceSet);

            return syncItem;
        }

        private string[] lastIVINamesArray = emptyStringArray;
        private int lastIVINamesArrayLength = 0;

        private ConfigSubscriptionSeqNums lastConfigSeqNums = new ConfigSubscriptionSeqNums();

        private static readonly string[] emptyStringArray = EmptyArrayFactory<string>.Instance;

        private class SyncItem
        {
            public string ivaLookupName;           // this is the name that was looked up to get the iva
            public string ivaFromCkaMappedName;    // this is the pseudo native IVA name (obtained by two level mapping from native cka name)
            public string ckaFromIvaMappedName;    // this is the pseudo native CKA name (obtained by two level mapping from native iva name)
            public string ckaLookupKeyName;        // this is the name that was looked up to get the cka.

            public IValueAccessor iva;             // the IVA for this sync item
            public IConfigKeyAccess icka;          // the ICKA for this sync item

            public ConfigKeyAccessCopy ckac;       // the last published ckac to the reference set for this item  
            public long ckacLastPublishedSeqNumInSet;

            public void UpdateCopyInSet(IReferenceSet<ConfigKeyAccessCopy> referenceSet)
            {
                if (referenceSet != null)
                {
                    ckac = new ConfigKeyAccessCopy(icka);

                    if (ckacLastPublishedSeqNumInSet != 0)
                        ckacLastPublishedSeqNumInSet = referenceSet.RemoveBySeqNumsAndAddItems(new[] { ckacLastPublishedSeqNumInSet }, ckac);
                    else
                        ckacLastPublishedSeqNumInSet = referenceSet.RemoveBySeqNumsAndAddItems(EmptyArrayFactory<long>.Instance, ckac);
                }
            }

            public int ckacLastServicedMDSeqNum = 0;

            public void ServiceMDPropagation(Logging.IMesgEmitter emitter, bool force = false)
            {
                if (ckacLastServicedMDSeqNum != icka.MetaDataSeqNum || force)
                {
                    iva.SetMetaData(icka.MetaData);
                    ckacLastServicedMDSeqNum = icka.MetaDataSeqNum;

                    emitter.Emit("Merged MetaData from icka:{0} into iva:{1}", icka, iva);
                }
            }
        }

        Dictionary<string, SyncItem> ivaNameToSyncItemDictionary = new Dictionary<string, SyncItem>();
        Dictionary<string, SyncItem> configKeyNameToSyncItemDictionary = new Dictionary<string, SyncItem>();

        // Note: the following is explicitly not using a IListWithCachedArray because the ivaArray is also generated at the same time that the syncItemArray is generated.
        private List<SyncItem> syncItemList = new List<SyncItem>();
        private SyncItem[] syncItemArray = null;
        private IValueAccessor[] ivaArray = null;

        #endregion
    }

	#endregion
}