//-------------------------------------------------------------------
/*! @file Window1.xaml.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2019 Mosaic Systems Inc., All rights reserved
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
using System.Linq;
using System.Text;
using System.Globalization;
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

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Modular.Part;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Reflection.Attributes;

using MosaicLib.WPF.Interconnect;

using Modular = MosaicLib.Modular;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Remoting;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Semi.E039;

namespace HostCycle
{
    public interface IServiceable
    {
        void Service();
    }

    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Logging.Logger Logger;

        protected List<IActivePartBase> partsList = new List<IActivePartBase>();

        private IValuesInterconnection IVI { get; set; }
        private WPFValueInterconnectAdapter WVA { get; set; }

        public Window1()
        {
            Logger = new Logging.Logger("Window1", Logging.LogGate.All);

            InitializeComponent();

            System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();
            var currentExecAssyFullNameSplit = currentExecAssy.FullName.Split(' ').Select(item => item.Trim(',')).ToArray();
            var fullVersion = currentExecAssy.GetInformationalVersion(fallbackValue: currentExecAssyFullNameSplit.SafeAccess(1));
            Title = "{0} [{1}]".CheckedFormat(currentExecAssyFullNameSplit.SafeAccess(0), fullVersion);

            IVI = Values.Instance;
            WVA = new WPFValueInterconnectAdapter(IVI);
            DataContext = WVA;

            IConfig configInstance = Config.Instance;

            // add host cycle part to partslist
            int numLPs = 1;

            var hostCycleKeyPrefix = "Config.HostCycle.";
            var hostCycleKeys = configInstance.SearchForKeys(new MosaicLib.Utils.StringMatching.MatchRuleSet(MosaicLib.Utils.StringMatching.MatchType.Prefix, hostCycleKeyPrefix));
            var portSettingsNVS = new NamedValueSet(hostCycleKeys.Select(key => configInstance.GetConfigKeyAccessOnce(key)).Select(icka => new NamedValue(icka.Key.RemovePrefixIfNeeded(hostCycleKeyPrefix), icka.VC))).MakeReadOnly();
            // we expect these keys to generally include the following: HostName, PortNum, DeviceID, HeaderTraceMesgType, MesgTraceMesgType, HighRateMesgTraceMesgType

            hostCycle = new HostCyclePart(portSettingsNVS: portSettingsNVS, numLPs: numLPs);

            partsList.Add(hostCycle);

            SetupE039TreeView();
        }

        HostCyclePart hostCycle;

        bool firstActivation = true;

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (firstActivation)
            {
                foreach (IActivePartBase part in partsList)
                {
                    if (part.BaseState == null || !part.BaseState.IsOnline)
                        part.CreateGoOnlineAction(false).Start();        // only issue a go online action if the part is not already online
                }

                StartTimers();

                firstActivation = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopTimers();

            base.OnClosed(e);

            List<IClientFacet> offlineActionList = new List<IClientFacet>();

            foreach (IActivePartBase part in partsList)
            {
                IClientFacet action = part.CreateGoOfflineAction();
                offlineActionList.Add(action);
                action.Start();
            }

            foreach (IClientFacet action in offlineActionList)
                action.WaitUntilComplete();

            foreach (IActivePartBase part in partsList)
                part.StopPart();
        }

        #region Timer Related

        private DispatcherTimer timer10Hz = null;

        void StartTimers()
        {
            timer10Hz = new DispatcherTimer(DispatcherPriority.Normal, this.Dispatcher);
            timer10Hz.Interval = TimeSpan.FromSeconds(0.10);
            timer10Hz.Tick += TimerEvent10Hz_Callback;
            timer10Hz.Start();
        }

        void StopTimers()
        {
            timer10Hz.Stop();
            timer10Hz = null;
        }

        List<IServiceable> Timer10HzServiceList = new List<IServiceable>();

        void TimerEvent10Hz_Callback(object sender, EventArgs e) 
        {
            foreach (IServiceable s in Timer10HzServiceList)
                s.Service();

            WVA.Service();
        }

        #endregion

        #region E039 related

        void SetupE039TreeView()
        {
            typeNameSetAccessor = WVA[$"{hostCycle.PartID}.E039.TypeNameSet"];

            HandleTypeNameSetUpdate(typeNameSetAccessor.VC.GetValue<IList<string>>(rethrow: false));

            typeNameSetAccessor.UpdateNotificationList.OnNotify += HandleTypeNameSetUpdate;

            e039SetTracker = MosaicLib.WPF.Tools.Sets.SetTracker.GetSetTracker("E039Set");
            e039SetTracker.AddSetDeltasEventHandler((o, sd) => HandleSetDelta(sd), HandleSetDelta, delayPriming: true);
        }

        WPFValueAccessor typeNameSetAccessor;

        private void HandleSetDelta(ISetDelta setDelta)
        {
            if (setDelta == null)
                return;

            if (setDelta.ClearSetAtStart)
            {
                e039TreeView.Items.Clear();
                typeTrackerDictionary.Clear();

                HandleTypeNameSetUpdate(typeNameSetAccessor.VC.GetValue<IList<string>>(rethrow: false));
            }

            foreach (var rrItem in setDelta.RemoveRangeItems)
            {
                var removeSeqNumRange = Enumerable.Range(0, rrItem.Count).Select(offset => rrItem.RangeStartSeqNum + offset);
                var removeObjTrackerRange = removeSeqNumRange.Select(seqNum => objTrackerBySeqNumDictionary.SafeTryGetValue(seqNum)).WhereIsNotDefault();

                scanRemoveObjectTrackerByFullNameDictionary.SafeAddRange(removeObjTrackerRange.Select(ot => KVP.Create(ot.fullName, ot)));
            }

            foreach (var arItem in setDelta.AddRangeItems)
            {
                var addOrUpdateItemSet = arItem.RangeObjects.Select((obj, offset) => new AddOrUpdateItem()
                {
                    seqNum = arItem.RangeStartSeqNum + offset,
                    obj = obj as IE039Object,
                });

                foreach (var item in addOrUpdateItemSet.Where(item => item.obj != null))
                {
                    scanRemoveObjectTrackerByFullNameDictionary.Remove(item.obj.ID.FullName);
                    scanAddAndUptateItemList.Add(item);
                }
            }

            foreach (var removeKVP in scanRemoveObjectTrackerByFullNameDictionary)
            {
                var objTracker = removeKVP.Value;
                if (objTracker != null)
                {
                    var typeTracker = objTracker.typeTracker;

                    typeTracker.objTrackerDictionary.Remove(objTracker.obj.ID.Name);
                    typeTracker.treeViewItem.Items.Remove(objTracker.treeViewItem);

                    typeTracker.UpdateHeader();

                    objTrackerBySeqNumDictionary.Remove(objTracker.seqNum);
                }
            }

            scanRemoveObjectTrackerByFullNameDictionary.Clear();

            foreach (var addOrUpdateItem in scanAddAndUptateItemList)
            {
                var typeTracker = typeTrackerDictionary.SafeTryGetValue(addOrUpdateItem.obj.ID.Type);
                if (typeTracker == null)
                    typeTracker = AddNewTypeTracker(addOrUpdateItem.obj.ID.Type);

                var objTracker = typeTracker.objTrackerDictionary.SafeTryGetValue(addOrUpdateItem.obj.ID.Name);

                if (objTracker == null)
                {
                    objTracker = new ObjTracker()
                    {
                        seqNum = addOrUpdateItem.seqNum,
                        id = addOrUpdateItem.obj.ID,
                        fullName = addOrUpdateItem.obj.ID.FullName,
                        obj = addOrUpdateItem.obj,
                        typeTracker = typeTracker,
                        treeViewItem = new TreeViewItem()
                        {
                            IsExpanded = false,
                            Header = addOrUpdateItem.obj.ID.Name,
                        },
                    }.UpdateHeader();

                    typeTracker.objTrackerDictionary[objTracker.obj.ID.Name] = objTracker;
                    typeTracker.treeViewItem.Items.Add(objTracker.treeViewItem);

                    typeTracker.UpdateHeader();
                }
                else
                {
                    // remove the object at its old seqNum
                    objTrackerBySeqNumDictionary.Remove(objTracker.seqNum);

                    objTracker.seqNum = addOrUpdateItem.seqNum;
                    objTracker.obj = addOrUpdateItem.obj;
                    objTracker.UpdateHeader();

                    typeTracker.UpdateHeader();     // in case this object changes also changes the type header.
                }

                // add the object at its new seqNum
                objTrackerBySeqNumDictionary[objTracker.seqNum] = objTracker;
            }

            scanAddAndUptateItemList.Clear();
        }

        Dictionary<string, ObjTracker> scanRemoveObjectTrackerByFullNameDictionary = new Dictionary<string, ObjTracker>();
        List<AddOrUpdateItem> scanAddAndUptateItemList = new List<AddOrUpdateItem>();

        private struct AddOrUpdateItem
        {
            public long seqNum;
            public IE039Object obj;
        }

        private void HandleTypeNameSetUpdate(object source, WPFValueAccessor.UpdateEventArgs eventArgs)
        {
            HandleTypeNameSetUpdate(eventArgs.VC.GetValue<IList<string>>(rethrow: false));
        }

        private void HandleTypeNameSetUpdate(IList<string> typeNameSet)
        {
            foreach (var typeName in typeNameSet.MapNullToEmpty())
            {
                if (!typeTrackerDictionary.ContainsKey(typeName))
                    AddNewTypeTracker(typeName);
            }
        }

        private TypeTracker AddNewTypeTracker(string typeName)
        {
            var typeTracker = new TypeTracker()
            {
                typeName = typeName,
                treeViewItem = new TreeViewItem()
                {
                    IsExpanded = false,
                },
            }.UpdateHeader();

            typeTrackerDictionary[typeName] = typeTracker;

            e039TreeView.Items.Add(typeTracker.treeViewItem);

            return typeTracker;
        }

        MosaicLib.WPF.Tools.Sets.SetTracker e039SetTracker;
        Dictionary<string, TypeTracker> typeTrackerDictionary = new Dictionary<string, TypeTracker>();
        Dictionary<long, ObjTracker> objTrackerBySeqNumDictionary = new Dictionary<long, ObjTracker>();

        private class TypeTracker
        {
            public string typeName;
            public TreeViewItem treeViewItem;
            public Dictionary<string, ObjTracker> objTrackerDictionary = new Dictionary<string, ObjTracker>();

            public TypeTracker UpdateHeader()
            {
                treeViewItem.ToolTip = 
                treeViewItem.Header = "{0} [{1} objects, maxSeqNum:{2}]".CheckedFormat(typeName, objTrackerDictionary.Count, objTrackerDictionary.Select(kvp => kvp.Value.seqNum).Concat(0).Max());
                return this;
            }
        };

        private class ObjTracker
        {
            public long seqNum;
            public E039ObjectID id;
            public string fullName;
            public IE039Object obj;
            public TypeTracker typeTracker;
            public TreeViewItem treeViewItem;

            public ObjTracker UpdateHeader()
            {
                treeViewItem.ToolTip =
                treeViewItem.Header = "{0}  seq:{1}  {2}".CheckedFormat(obj.ID.Name, seqNum, obj.Attributes.SafeToStringSML());
                return this;
            }
        };

        #endregion

        #region Routed command handler (RoutedServiceCommand_Executed, RoutedServiceCommand_CanExecute)

        private void RoutedServiceCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RoutedCommand rcmd = (RoutedCommand)e.Command;
            Control c = e.Source as Control;

            string cmdParam = e.Parameter.SafeToString();
            string[] splitArray = cmdParam.Split(new [] {' '}, 2, StringSplitOptions.RemoveEmptyEntries);
            string partName = splitArray.SafeAccess(0, defaultValue: "");
            string rest = splitArray.SafeAccess(1, defaultValue: "");

            var npv = MosaicLib.WPF.Extensions.Attachable.GetNPV(c);

            var part = MosaicLib.Modular.Interconnect.Parts.Parts.Instance.FindPart(partName, throwOnNotFound: true);

            part.CreateServiceAction(rest, namedParamValues: npv).Start();

            e.Handled = true;
        }

        private void RoutedServiceCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        #endregion
    }

    public class MesgAvailToAckDenyReasonConverter : MosaicLib.WPF.Converters.OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var vc = ValueContainer.CreateFromObject(value);
            if (vc.GetValue<bool?>(rethrow:false) ?? false)
                return "";
            return "No new message is available to acknowledge";
        }
    }

    public class SendResultConverter : MosaicLib.WPF.Converters.OneWayValueConverterBase
    {
        private static readonly IValueConverter colorNameToSolidColorBrushConverter = new MosaicLib.WPF.Converters.ColorNameToSolidColorBrushConverter();
        private static readonly IValueConverter vcToStringSMLConverter = new MosaicLib.WPF.Converters.VCToStringSMLConverter();

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var vc = ValueContainer.CreateFromObject(value);
            var acka10 = vc.GetValue<MosaicLib.Semi.E005.ACKC10?>(rethrow: false);
            var strValue = vc.GetValue<string>(rethrow: false);

            switch ((string)parameter)
            {
                case "SolidColorBrush":
                    switch (acka10 ?? MosaicLib.Semi.E005.ACKC10.Invalid)
                    {
                        case MosaicLib.Semi.E005.ACKC10.AcceptedForDisplay: return colorNameToSolidColorBrushConverter.Convert("Green", targetType, null, culture);
                        case MosaicLib.Semi.E005.ACKC10.MessageWillNotBeDisplayed: return colorNameToSolidColorBrushConverter.Convert("Red", targetType, null, culture);
                        case MosaicLib.Semi.E005.ACKC10.TerminalNotAvailable: return colorNameToSolidColorBrushConverter.Convert("Purple", targetType, null, culture);
                    }

                    switch (strValue.MapNullToEmpty())
                    {
                        case "None": return colorNameToSolidColorBrushConverter.Convert("Gray", targetType, null, culture);
                        case "Sending": return colorNameToSolidColorBrushConverter.Convert("Blue", targetType, null, culture);
                        case "Sent": return colorNameToSolidColorBrushConverter.Convert("DarkGreen", targetType, null, culture);
                        default: break;
                    }

                    if (strValue.StartsWith("Error"))
                       return colorNameToSolidColorBrushConverter.Convert("Pink", targetType, null, culture);

                    return colorNameToSolidColorBrushConverter.Convert(strValue, targetType, null, culture);

                case "ToolTip":
                    if (acka10 != null)
                        return $"ACKA10 {acka10}";

                    return vcToStringSMLConverter.Convert(vc, targetType, null, culture);

                default:
                    return colorNameToSolidColorBrushConverter.Convert(strValue, targetType, null, culture);
            }
        }
    }
}
