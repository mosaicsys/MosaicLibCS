//-------------------------------------------------------------------
/*! @file E090View.xaml.cs
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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

namespace MosaicLib.WPF.Controls
{
    /// <summary>
    /// Interaction logic for E090View.xaml
    /// </summary>
    public partial class E090View : UserControl
    {
        public E090View()
        {
            InitializeComponent();

            Items = new ObservableCollection<Tools.Sets.E090.E090CombinedSubstLocAndSubstInfoTracker>();

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
			view.Filter = ApplyFilterString;            
        }

        private bool ApplyFilterString(object obj)
        {
            Tools.Sets.E090.E090CombinedSubstLocAndSubstInfoTracker tracker = obj as Tools.Sets.E090.E090CombinedSubstLocAndSubstInfoTracker;

            if (tracker == null)
                return false;

            if (filterPredicate == null)
                return true;

            return filterPredicate(tracker.CombinedInfo);
        }

        Func<Tools.Sets.E090.E090CombinedSubstLocAndSubstInfo, bool> filterPredicate = null;

        private void InnerUpdateFilterString(string filterString)
        {
            string[] splitFilterStringArray = filterString.MapNullToEmpty().Split('|').Where(str => str.IsNeitherNullNorEmpty()).ToArray();
            switch (splitFilterStringArray.Length)
            {
                case 0:
                    filterPredicate = null;
                    break;

                case 1:
                    {
                        string splitFilterString = splitFilterStringArray[0];
                        filterPredicate = (combined => (combined.SubstLocInfo.ObjID.Name.IndexOf(splitFilterString, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                        || (combined.SubstInfo.ObjID.Name.IndexOf(splitFilterString, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                        || (combined.SubstInfoFrom.ToString().IndexOf(splitFilterString, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                        );
                    }
                    break;

                default:
                    filterPredicate = (combined => splitFilterStringArray.Any(splitFilterString => (combined.SubstLocInfo.ObjID.Name.IndexOf(splitFilterString, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                                                                    || (combined.SubstInfo.ObjID.Name.IndexOf(splitFilterString, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                                                                    || (combined.SubstInfoFrom.ToString().IndexOf(splitFilterString, StringComparison.InvariantCultureIgnoreCase) != -1)
                                                                                                    ));
                    break;
            }
        }

        public static DependencyProperty SetNameProperty = DependencyProperty.Register("SetName", typeof(string), typeof(E090View), new PropertyMetadata(string.Empty, HandleSetNamePropertyChanged));
        public static DependencyProperty FilterStringProperty = DependencyProperty.Register("FilterString", typeof(string), typeof(E090View), new PropertyMetadata(string.Empty, HandleFilterStringPropertyChanged));
        public static DependencyPropertyKey ItemsPropertyKey = DependencyProperty.RegisterReadOnly("Items", typeof(ObservableCollection<Tools.Sets.E090.E090CombinedSubstLocAndSubstInfoTracker>), typeof(E090View), new PropertyMetadata(null));
        public static DependencyProperty ControlsVisibilityProperty = DependencyProperty.Register("ControlsVisibility", typeof(Visibility), typeof(E090View), new PropertyMetadata(Visibility.Visible));
        public static DependencyProperty SubstLocNameColumnWidthProperty = DependencyProperty.Register("SubstLocNameColumnWidth", typeof(double), typeof(E090View), new PropertyMetadata(100.0));
        public static DependencyProperty FromColumnWidthProperty = DependencyProperty.Register("FromColumnWidth", typeof(double), typeof(E090View), new PropertyMetadata(100.0));
        public static DependencyProperty SubstNameColumnWidthProperty = DependencyProperty.Register("SubstNameColumnWidth", typeof(double), typeof(E090View), new PropertyMetadata(100.0));
        public static DependencyProperty InfoColumnWidthProperty = DependencyProperty.Register("InfoColumnWidth", typeof(double), typeof(E090View), new PropertyMetadata(600.0));
        public static DependencyProperty FilterFieldWidthProperty = DependencyProperty.Register("FilterFieldWidth", typeof(double), typeof(E090View), new PropertyMetadata(120.0));

        public string SetName { get { return (string)GetValue(SetNameProperty); } set { SetValue(SetNameProperty, _setName = value); } }
        public string FilterString { get { return (string)GetValue(FilterStringProperty); } set { SetValue(FilterStringProperty, value); } }
        public ObservableCollection<Tools.Sets.E090.E090CombinedSubstLocAndSubstInfoTracker> Items { get { return _items; } set { SetValue(ItemsPropertyKey, _items = value); } }
        public Visibility ControlsVisibility { get { return (Visibility)GetValue(ControlsVisibilityProperty); } set { SetValue(ControlsVisibilityProperty, value); } }
        public double SubstLocNameColumnWidth { get { return (double)GetValue(SubstLocNameColumnWidthProperty); } set { SetValue(SubstLocNameColumnWidthProperty, value); } }
        public double FromColumnWidth { get { return (double)GetValue(FromColumnWidthProperty); } set { SetValue(FromColumnWidthProperty, value); } }
        public double SubstNameColumnWidth { get { return (double)GetValue(SubstNameColumnWidthProperty); } set { SetValue(SubstNameColumnWidthProperty, value); } }
        public double InfoColumnWidth { get { return (double)GetValue(InfoColumnWidthProperty); } set { SetValue(InfoColumnWidthProperty, value); } }
        public double FilterFieldWidth { get { return (double)GetValue(FilterFieldWidthProperty); } set { SetValue(FilterFieldWidthProperty, value); } }

        private string _setName = string.Empty;
        private ObservableCollection<Tools.Sets.E090.E090CombinedSubstLocAndSubstInfoTracker> _items;

        private static void HandleSetNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((E090View) d).HandleSetNamePropertyChanged((string) e.NewValue);
        }

        private void HandleSetNamePropertyChanged(string newSetName)
        {
            if (_setName == newSetName)
                return;

            _handleSubstLocNamesAdded = _handleSubstLocNamesAdded ?? HandleSubstLocNamesAdded;
            var prevSetTracker = selectedE090TableSetTracker;

            selectedE090TableSetTracker = new Tools.Sets.E090.E090TableSetTrackerWithSubstLocListBuilder(_setName = newSetName);
            selectedE090TableSetTracker.SubstLocNamesAddedNotificationList.OnNotify += _handleSubstLocNamesAdded;

            if (prevSetTracker != null)
                prevSetTracker.SubstLocNamesAddedNotificationList.OnNotify -= _handleSubstLocNamesAdded;

            _items.Clear();

            //HandleSubstLocNamesAdded(this, new[] { "aaLocDoesNotExist" });
        }

        private static void HandleFilterStringPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            E090View me = d as E090View;

            if (me != null)
            {
                me.InnerUpdateFilterString((string)e.NewValue);
                CollectionViewSource.GetDefaultView(me.listView.ItemsSource).Refresh();
            }
        }

        EventHandlerDelegate<string[]> _handleSubstLocNamesAdded;

        private void HandleSubstLocNamesAdded(object sender, string[] substLocNamesArray)
        {
            foreach (var substLocName in substLocNamesArray.MapNullToEmpty())
            {
                var combinedInfoTracker = selectedE090TableSetTracker[substLocName];

                _items.Add(combinedInfoTracker);
            }
        }

        WPF.Tools.Sets.E090.E090TableSetTrackerWithSubstLocListBuilder selectedE090TableSetTracker = null;

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
                case SortKeySelection.SubstLocName: sortDescription = new SortDescription("CombinedInfo.SubstLocInfo.ObjID.Name", sortDirection); break;
                case SortKeySelection.SubstInfoFrom: sortDescription = new SortDescription("CombinedInfo.SubstInfoFrom", sortDirection); break;
                case SortKeySelection.SubstName: sortDescription = new SortDescription("CombinedInfo.SubstInfo.ObjID.Name", sortDirection); break;
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
            SubstLocName,
            SubstInfoFrom,
            SubstName,
        }

        public enum SortDirectionSelection : int
        {
            None = 0,
            Ascending,
            Descending,
        }
    }

    /// <summary>
    /// Supports bindable, one way, conversion of an E090CombinedSubstLocAndSubstInfo into a (background) brush
    /// <para/>The ConvertBack method alsways returns Binding.DoNothing
    /// </summary>
    public class E090ViewCombinedInfoToBrushConverter : Converters.OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var combinedInfo = value as Tools.Sets.E090.E090CombinedSubstLocAndSubstInfo;

            if (combinedInfo == null || combinedInfo.SubstLocInfo.ObjID.IsEmpty || combinedInfo.SubstLocInfo.SLS == Semi.E090.SubstLocState.Undefined || combinedInfo.SubstLocInfo.NotAccessibleReason.IsNeitherNullNorEmpty())
                return siennaBrush;

            if (combinedInfo.SubstInfoFrom == Tools.Sets.E090.SubstInfoFrom.None)
                return lightGrayBrush;

            Color substColor = default(Color);
            float alpha = 1.0f;

            switch (combinedInfo.SubstInfo.InferredSPS)
            {
                case Semi.E090.SubstProcState.NeedsProcessing: substColor = lightSkyBlue; break;
                case Semi.E090.SubstProcState.InProcess: substColor = blue; break;
                case Semi.E090.SubstProcState.ProcessStepCompleted: substColor = blue; break;
                case Semi.E090.SubstProcState.Processed: substColor = green; break;
                case Semi.E090.SubstProcState.Rejected: substColor = red; break;
                case Semi.E090.SubstProcState.Aborted: substColor = orange; break;
                case Semi.E090.SubstProcState.Stopped: substColor = yellow; break;
                case Semi.E090.SubstProcState.Skipped: substColor = gray; break;
                case Semi.E090.SubstProcState.Undefined: substColor = goldenrod; break;
                case Semi.E090.SubstProcState.Lost: substColor = goldenrod; break;
                default: substColor = goldenrod; break;
            }

            if (combinedInfo.SubstInfoFrom == Tools.Sets.E090.SubstInfoFrom.Contains)
                alpha *= 0.5f;
            else if (combinedInfo.SubstInfoFrom == Tools.Sets.E090.SubstInfoFrom.Source || combinedInfo.SubstInfoFrom == Tools.Sets.E090.SubstInfoFrom.Destination)
                alpha *= 0.1f;

            substColor.ScA = alpha;
            substColor.Clamp();

            return new SolidColorBrush(substColor);
        }

        private static readonly TypeConverter colorConverter = TypeDescriptor.GetConverter(typeof(Color));

        private static readonly Color cornSilk = ((Color)colorConverter.ConvertFrom("CornSilk"));
        private static readonly Color lightSkyBlue = ((Color)colorConverter.ConvertFrom("LightSkyBlue"));
        private static readonly Color blue = ((Color)colorConverter.ConvertFrom("Blue"));
        private static readonly Color green = ((Color)colorConverter.ConvertFrom("Green"));
        private static readonly Color yellow = ((Color)colorConverter.ConvertFrom("Yellow"));
        private static readonly Color orange = ((Color)colorConverter.ConvertFrom("Orange"));
        private static readonly Color red = ((Color)colorConverter.ConvertFrom("Red"));

        private static readonly Color lightGray = ((Color)colorConverter.ConvertFrom("LightGray"));
        private static readonly Color gray = ((Color)colorConverter.ConvertFrom("Gray"));
        private static readonly Color darkGray = ((Color)colorConverter.ConvertFrom("DarkGray"));
        private static readonly Color goldenrod = ((Color)colorConverter.ConvertFrom("Goldenrod"));
        private static readonly Color sienna = ((Color)colorConverter.ConvertFrom("Sienna"));

        private static readonly Brush lightGrayBrush = new SolidColorBrush(lightGray);
        private static readonly Brush siennaBrush = new SolidColorBrush(sienna);
    }
}
