﻿//-------------------------------------------------------------------
/*! @file IVIView.xaml.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2018 Mosaic Systems Inc., All rights reserved
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
using System.Windows.Shapes;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using System.ComponentModel;

namespace MosaicLib.WPF.Controls
{
    /// <summary>
    /// Interaction logic for IVIView.xaml
    /// </summary>
    public partial class IVIView : UserControl
    {
        public IVIView()
        {
            IsVisibleChanged += IVIView_IsVisibleChanged;

            InitializeComponent();

            Items = new ObservableCollection<IVAWrapper>();

            timer.TickNotificationList.AddItem(() => Service());

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
			view.Filter = ApplyIVANameFilter;            
        }

        void IVIView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue as bool? == true)
            {
                Service(evenIfNotVisible: true);
                if (runTimerToken == null)
                    runTimerToken = timer.GetRunTimerToken(Fcns.CurrentClassLeafName);
            }
            else if (runTimerToken != null)
            {
                Fcns.DisposeOfObject(ref runTimerToken);
            }
        }

        private bool ApplyIVANameFilter(object obj)
        {
            IVAWrapper ivaWrapper = obj as IVAWrapper;

            if (ivaWrapper == null)
                return false;

            if (filterPredicate == null)
                return true;

            return filterPredicate(ivaWrapper);
        }

        Func<IVAWrapper, bool> filterPredicate = null;

        private void InnerUpdateFilterString(string filterString, bool enableFilterOnVCasSMLString)
        {
            string[] splitFilterStringArray = filterString.MapNullToEmpty().Split('|').Where(str => str.IsNeitherNullNorEmpty()).ToArray();
            switch (splitFilterStringArray.Length)
            {
                case 0:
                    filterPredicate = null;
                    break;

                case 1:
                    {
                        string splitFilterStringItem = splitFilterStringArray[0];
                        filterPredicate = (wrapper => (wrapper.Name.IndexOf(splitFilterStringItem, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                        || (enableFilterOnVCasSMLString && wrapper.VCasSMLString.IndexOf(splitFilterStringItem, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                        );
                    }
                    break;

                default:
                    filterPredicate = (wrapper => splitFilterStringArray.Any(splitFilterStringItem => (wrapper.Name.IndexOf(splitFilterStringItem, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                                                                        || (enableFilterOnVCasSMLString && wrapper.VCasSMLString.IndexOf(splitFilterStringItem, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                                                                        ));
                    break;
            }
        }

        private static readonly Timers.ISharedDispatcherTimer timer = Timers.SharedDispatcherTimerFactory.GetSharedTimer(5.0);
        private IDisposable runTimerToken = null;

        public static DependencyProperty IVINameProperty = DependencyProperty.Register("IVIName", typeof(string), typeof(IVIView), new PropertyMetadata(string.Empty, HandleSetNamePropertyChanged));
        public static DependencyProperty NameColumnWidthProperty = DependencyProperty.Register("NameColumnWidth", typeof(double), typeof(IVIView), new PropertyMetadata(90.0));
        public static DependencyProperty SeqNumColumnWidthProperty = DependencyProperty.Register("SeqNumColumnWidth", typeof(double), typeof(IVIView), new PropertyMetadata(20.0));
        public static DependencyProperty ValueColumnWidthProperty = DependencyProperty.Register("ValueColumnWidth", typeof(double), typeof(IVIView), new PropertyMetadata(200.0));
        public static DependencyProperty PauseProperty = DependencyProperty.Register("Pause", typeof(bool), typeof(IVIView), new PropertyMetadata(false, HandlePausePropertyChanged));
        public static DependencyProperty FilterStringProperty = DependencyProperty.Register("FilterString", typeof(string), typeof(IVIView), new PropertyMetadata(string.Empty, HandleFilterStringPropertyChanged));
        public static DependencyPropertyKey ItemsPropertyKey = DependencyProperty.RegisterReadOnly("Items", typeof(ObservableCollection<IVAWrapper>), typeof(IVIView), new PropertyMetadata(null));
        public static DependencyProperty ControlsVisibilityProperty = DependencyProperty.Register("ControlsVisibility", typeof(Visibility), typeof(IVIView), new PropertyMetadata(Visibility.Visible));
        public static DependencyProperty FilterFieldWidthProperty = DependencyProperty.Register("FilterFieldWidth", typeof(double), typeof(IVIView), new PropertyMetadata(120.0));
        public static DependencyProperty EnableFilterOnVCasSMLStringProperty = DependencyProperty.Register("EnableFilterOnVCasSMLString", typeof(bool), typeof(IVIView), new PropertyMetadata(true, HandleEnableFilterOnVCasSMLStringPropertyChanged));

        public string IVIName { get { return (string)GetValue(IVINameProperty); } set { SetValue(IVINameProperty, _ivaName = value); } }
        public double NameColumnWidth { get { return (double)GetValue(NameColumnWidthProperty); } set { SetValue(NameColumnWidthProperty, value); } }
        public double SeqNumColumnWidth { get { return (double)GetValue(SeqNumColumnWidthProperty); } set { SetValue(SeqNumColumnWidthProperty, value); } }
        public double ValueColumnWidth { get { return (double)GetValue(ValueColumnWidthProperty); } set { SetValue(ValueColumnWidthProperty, value); } }
        public bool Pause { get { return (bool)GetValue(PauseProperty); } set { SetValue(PauseProperty, _isPaused = value); } }
        public string FilterString { get { return (string)GetValue(FilterStringProperty); } set { SetValue(FilterStringProperty, value); } }
        public ObservableCollection<IVAWrapper> Items { get { return _items; } set { SetValue(ItemsPropertyKey, _items = value);  } }
        public Visibility ControlsVisibility { get { return (Visibility)GetValue(ControlsVisibilityProperty); } set { SetValue(ControlsVisibilityProperty, value); } }
        public double FilterFieldWidth { get { return (double)GetValue(FilterFieldWidthProperty); } set { SetValue(FilterFieldWidthProperty, value); } }
        public bool EnableFilterOnVCasSMLString { get { return (bool)GetValue(EnableFilterOnVCasSMLStringProperty); } set { SetValue(EnableFilterOnVCasSMLStringProperty, value); } }

        private string _ivaName = string.Empty;
        private bool _isPaused;
        private ObservableCollection<IVAWrapper> _items;

        private static void HandleSetNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            IVIView me = d as IVIView;

            if (me != null)
            {
                me._ivaName = ((string)e.NewValue).MapNullToEmpty();
                me.Service(forceRebuild: true);
            }
        }

        private static void HandlePausePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            IVIView me = d as IVIView;

            if (me != null)
            {
                bool newPaused = (bool) e.NewValue;

                me._isPaused = newPaused;
                if (!me._isPaused)
                    me.Service();
            }
        }

        private static void HandleFilterStringPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            IVIView me = d as IVIView;

            if (me != null)
            {
                me.InnerUpdateFilterString((string)e.NewValue, me.EnableFilterOnVCasSMLString);
                CollectionViewSource.GetDefaultView(me.listView.ItemsSource).Refresh();
            }
        }

        private static void HandleEnableFilterOnVCasSMLStringPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            IVIView me = d as IVIView;

            if (me != null)
            {
                me.InnerUpdateFilterString(me.FilterString, (bool)e.NewValue);
                CollectionViewSource.GetDefaultView(me.listView.ItemsSource).Refresh();
            }
        }

        private void Service(bool evenIfNotVisible = false, bool forceUpdateIVAWrappers = false, bool forceRebuild = false)
        {
            if (_isPaused && !forceRebuild)
                return;

            if (!IsVisible && !(evenIfNotVisible || forceRebuild))
                return;

            bool checkForAdditions = false;
            bool checkForChanges = false;

            if (forceRebuild || selectedIVI == null)
            {
                selectedIVI = Values.GetTable(_ivaName, addNewTableIfMissing: true);
                ivaWrapperList.Clear();
                ivaList.Clear();
                lastIVIGlobalSeqNum = 0;

                checkForChanges = checkForAdditions = (selectedIVI != null);
            }
            else if (selectedIVI != null && lastIVIGlobalSeqNum != selectedIVI.GlobalSeqNum)
            {
                checkForChanges = true;
                checkForAdditions = (selectedIVI.ValueNamesArrayLength > ivaList.Count);
            }

            if (checkForAdditions)
            {
                var newNamesArray = selectedIVI.GetValueNamesRange(ivaList.Count);
                var newIVAsArray = newNamesArray.Select(name => selectedIVI.GetValueAccessor(name)).ToArray();
                var newIVAWrappersArray = newIVAsArray.Select(iva => new IVAWrapper(iva)).ToArray();

                ivaList.AddRange(newIVAsArray);
                ivaWrapperList.AddRange(newIVAWrappersArray);

                var items = Items;
                newIVAWrappersArray.DoForEach(wrapper => items.Add(wrapper));
            }

            if (checkForChanges)
            {
                if (ivaList.Array.IsUpdateNeeded() || evenIfNotVisible)
                {
                    selectedIVI.Update(ivaList.Array);

                    ivaWrapperList.Array.DoForEach(wrapper => wrapper.Service(forceUpdate: forceUpdateIVAWrappers));
                }
            }
        }

        IValuesInterconnection selectedIVI = null;
        uint lastIVIGlobalSeqNum = 0;

        IListWithCachedArray<IVAWrapper> ivaWrapperList = new IListWithCachedArray<IVAWrapper>();
        IListWithCachedArray<IValueAccessor> ivaList = new IListWithCachedArray<IValueAccessor>();

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var columnHeaderUserClickedOn = (GridViewColumnHeader) sender;
            string columnHeaderTagAsString = columnHeaderUserClickedOn.Tag.ToString();

            var nextSortKeySelection = ValueContainer.CreateFromObject(columnHeaderUserClickedOn.Tag).GetValue<SortKeySelection>(rethrow: false);
            var nextSortDirectionSelection = currentSortDirectionSelection;

            if (nextSortKeySelection != currentSortKeySelection)
            {
                nextSortDirectionSelection = SortDirectionSelection.Ascending;
            }
            else
            {
                switch (currentSortDirectionSelection)
                {
                    case SortDirectionSelection.None: nextSortDirectionSelection = SortDirectionSelection.Ascending; break;
                    case SortDirectionSelection.Ascending: nextSortDirectionSelection = SortDirectionSelection.Descending; break;
                    case SortDirectionSelection.Descending: nextSortDirectionSelection = SortDirectionSelection.None; break;
                    default: nextSortDirectionSelection = SortDirectionSelection.None; break;
                }
            }

            InnerUpdateSorting(nextSortKeySelection, nextSortDirectionSelection);
        }

        SortKeySelection currentSortKeySelection;
        SortDirectionSelection currentSortDirectionSelection;

        private void InnerUpdateSorting(SortKeySelection nextSortKeySelection, SortDirectionSelection nextSortDirectionSelection)
        {
            if (nextSortDirectionSelection == SortDirectionSelection.None)
                nextSortKeySelection = SortKeySelection.None;

            var sortDirection = (nextSortDirectionSelection == SortDirectionSelection.Ascending) ? ListSortDirection.Ascending : ListSortDirection.Descending;
            SortDescription? sortDescription = null;

            switch (nextSortKeySelection)
            {
                case SortKeySelection.None: break;
                case SortKeySelection.Name: sortDescription = new SortDescription("Name", sortDirection); break;
                case SortKeySelection.SeqNum: sortDescription = new SortDescription("ValueSeqNum", sortDirection); break;
                default: break;
            }

            if (sortDescription == null)
            {
                if (listView.Items.SortDescriptions.Count > 0)
                {
                    listView.Items.SortDescriptions.Clear();
                    currentSortKeySelection = SortKeySelection.None;
                    currentSortDirectionSelection = SortDirectionSelection.None;
                }
            }
            else
            {
                if (listView.Items.SortDescriptions.Count == 0)
                    listView.Items.SortDescriptions.Add(sortDescription ?? default(SortDescription));
                else
                    listView.Items.SortDescriptions[0] = sortDescription ?? default(SortDescription);
                currentSortKeySelection = nextSortKeySelection;
                currentSortDirectionSelection = nextSortDirectionSelection;
            }
        }

        public enum SortKeySelection : int
        {
            None = 0,
            Name,
            SeqNum,
        }

        public enum SortDirectionSelection : int
        {
            None = 0,
            Ascending,
            Descending,
        }
    }

    public class IVAWrapper : DependencyObject
    {
        private static readonly DependencyPropertyKey NamePropertyKey = DependencyProperty.RegisterReadOnly("Name", typeof(string), typeof(IVAWrapper), new PropertyMetadata(null));
        private static readonly DependencyPropertyKey IDPropertyKey = DependencyProperty.RegisterReadOnly("ID", typeof(int), typeof(IVAWrapper), new PropertyMetadata(0));
        private static readonly DependencyPropertyKey ValueSeqNumPropertyKey = DependencyProperty.RegisterReadOnly("ValueSeqNum", typeof(uint), typeof(IVAWrapper), new PropertyMetadata(0u));
        private static readonly DependencyPropertyKey VCPropertyKey = DependencyProperty.RegisterReadOnly("VC", typeof(ValueContainer), typeof(IVAWrapper), new PropertyMetadata(ValueContainer.Empty));
        private static readonly DependencyPropertyKey VCasSMLStringPropertyKey = DependencyProperty.RegisterReadOnly("VCasSMLString", typeof(string), typeof(IVAWrapper), new PropertyMetadata(ValueContainer.Empty.ToStringSML()));
        private static readonly DependencyPropertyKey MetaDataSeqNumPropertyKey = DependencyProperty.RegisterReadOnly("MetaDataSeqNum", typeof(uint), typeof(IVAWrapper), new PropertyMetadata(0u));
        private static readonly DependencyPropertyKey MetaDataPropertyKey = DependencyProperty.RegisterReadOnly("MetaData", typeof(INamedValueSet), typeof(IVAWrapper), new PropertyMetadata(null));

        public string Name { get { return IVA.Name; } }
        public int ID { get { return IVA.ID; } }
        public uint ValueSeqNum { get { return IVA.ValueSeqNum; } }
        public ValueContainer VC { get { return IVA.VC; } }
        public uint MetaDataSeqNum { get { return IVA.MetaDataSeqNum; } }
        public INamedValueSet MetaData { get { return IVA.MetaData; } }
        public string VCasSMLString { get; private set; }

        public IValueAccessor IVA { get; private set; }

        public IVAWrapper(IValueAccessor iva)
        {
            IVA = iva;
            SetValue(NamePropertyKey, Name);
            SetValue(IDPropertyKey, ID);
            Service(forceUpdate: true);
        }

        uint lastValueSeqNum = 0;
        uint lastMetaDataSeqNum = 0;

        public void Service(bool forceUpdate = false)
        {
            if (lastValueSeqNum != ValueSeqNum || forceUpdate)
            {
                SetValue(ValueSeqNumPropertyKey, lastValueSeqNum = ValueSeqNum);
                SetValue(VCPropertyKey, VC);
                SetValue(VCasSMLStringPropertyKey, VCasSMLString = VC.ToStringSML());
            }

            if (lastMetaDataSeqNum != MetaDataSeqNum || forceUpdate)
            {
                SetValue(MetaDataSeqNumPropertyKey, lastMetaDataSeqNum = MetaDataSeqNum);
                SetValue(MetaDataPropertyKey, MetaData);
            }
        }
    }
}
