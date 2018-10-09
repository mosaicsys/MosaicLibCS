//-------------------------------------------------------------------
/*! @file LogView.xaml.cs
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
using MosaicLib.Utils;
using MosaicLib.Modular.Interconnect.Sets;

using MosaicLib.WPF.Logging;
using MosaicLib.WPF.Tools.Sets;

namespace MosaicLib.WPF.Controls
{
    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogView : UserControl
    {
        public LogView()
        {
            LogMessageSetTracker = new AdjustableLogMessageSetTracker() { LogGate = MosaicLib.Logging.LogGate.Info };

            LogMessageSetTracker.NewItemsAdded += PostAttemptToScrollToBottom;
            LogMessageSetTracker.SetRebuilt += () =>
                                                {
                                                    if (!LogMessageSetTracker.Pause)
                                                        PostAttemptToScrollToBottom();
                                                };

            IsVisibleChanged += LogView_IsVisibleChanged;

            SetBinding(LogGateProperty, new Binding() { Source = LogMessageSetTracker, Path = new PropertyPath(AdjustableLogMessageSetTracker.LogGateProperty), Mode = BindingMode.TwoWay });
            SetBinding(FilterStringProperty, new Binding() { Source = LogMessageSetTracker, Path = new PropertyPath(AdjustableLogMessageSetTracker.FilterStringProperty), Mode = BindingMode.TwoWay });

            InitializeComponent();
        }

        public static DependencyProperty SetNameProperty = DependencyProperty.Register("SetName", typeof(string), typeof(LogView), new PropertyMetadata(null, HandleSetNamePropertyChanged));
        public static DependencyProperty EnabledSourcesProperty = DependencyProperty.Register("EnabledSources", typeof(object), typeof(LogView), new PropertyMetadata(null, HandleEnabledSourcesPropertyChanged));
        public static DependencyProperty DateColumnWidthProperty = DependencyProperty.Register("DateColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(90.0));
        public static DependencyProperty TypeColumnWidthProperty = DependencyProperty.Register("TypeColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(60.0));
        public static DependencyProperty SourceColumnWidthProperty = DependencyProperty.Register("SourceColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(120.0));
        public static DependencyProperty MesgColumnWidthProperty = DependencyProperty.Register("MesgColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(450.0));
        public static DependencyProperty LogGateProperty = DependencyProperty.Register("LogGate", typeof(MosaicLib.Logging.LogGate), typeof(LogView), new PropertyMetadata(AdjustableLogMessageSetTracker.DefaultLogGate));
        public static DependencyProperty FilterStringProperty = DependencyProperty.Register("FilterString", typeof(string), typeof(LogView), new PropertyMetadata(""));
        public static DependencyProperty ControlsVisibilityProperty = DependencyProperty.Register("ControlsVisibility", typeof(Visibility), typeof(LogView), new PropertyMetadata(Visibility.Visible));

        public string SetName { get { return (string)GetValue(SetNameProperty); } set { SetValue(SetNameProperty, value); } }
        public object EnabledSources { get { return (string)GetValue(EnabledSourcesProperty); } set { SetValue(EnabledSourcesProperty, value); } }
        public double DateColumnWidth { get { return (double)GetValue(DateColumnWidthProperty); } set { SetValue(DateColumnWidthProperty, value); } }
        public double TypeColumnWidth { get { return (double)GetValue(TypeColumnWidthProperty); } set { SetValue(TypeColumnWidthProperty, value); } }
        public double SourceColumnWidth { get { return (double)GetValue(SourceColumnWidthProperty); } set { SetValue(SourceColumnWidthProperty, value); } }
        public double MesgColumnWidth { get { return (double)GetValue(MesgColumnWidthProperty); } set { SetValue(MesgColumnWidthProperty, value); } }
        public MosaicLib.Logging.LogGate LogGate { get { return (MosaicLib.Logging.LogGate)GetValue(LogGateProperty); } set { SetValue(LogGateProperty, value); } }
        public string FilterString { get { return (string)GetValue(FilterStringProperty); } set { SetValue(FilterStringProperty, value); } }
        public Visibility ControlsVisibility { get { return (Visibility)GetValue(ControlsVisibilityProperty); } set { SetValue(ControlsVisibilityProperty, value); } }

        private static void HandleSetNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            LogView me = d as LogView;

            if (me != null)
                me.LogMessageSetTracker.SetName = (string)e.NewValue;
        }

        private static void HandleEnabledSourcesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            LogView me = d as LogView;

            if (me != null)
                me.LogMessageSetTracker.EnabledSources = e.NewValue;
        }

        void LogView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool) e.NewValue == true && !LogMessageSetTracker.Pause)
            {
                PostAttemptToScrollToBottom();
            }
        }

        void PostAttemptToScrollToBottom()
        {
            System.Threading.SynchronizationContext.Current.Post(o => AttemptToScrollTo(ScrollTo.Bottom), this);
        }

        public AdjustableLogMessageSetTracker LogMessageSetTracker { get; private set; }

        private void HandleClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessageSetTracker.Clear();
        }

        private void HandleScrolltoTopButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptToScrollTo(ScrollTo.Top);
        }

        private void HandleScrolltoBottomButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptToScrollTo(ScrollTo.Bottom);
        }

        public void AttemptToScrollTo(ScrollTo scrollTo)
        {
            if (listView.Items.Count > 0)
            {
                object showLVItem = listView.Items[(scrollTo == ScrollTo.Bottom) ? listView.Items.Count - 1 : 0];

                listView.ScrollIntoView(showLVItem);
            }
        }

        public enum ScrollTo
        {
            Top,
            Bottom
        }
    }
}
