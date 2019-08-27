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

using MosaicLib.WPF.Tools.Sets;

namespace MosaicLib.WPF.Controls
{
    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogView : UserControl
    {
        public const int defaultMaximumCapacity = 1000;

        public LogView()
        {
            LogMessageSetTracker = new AdjustableLogMessageSetTracker(maximumCapacity: defaultMaximumCapacity) { LogGate = MosaicLib.Logging.LogGate.Info };

            IsVisibleChanged += LogView_IsVisibleChanged;

            InitializeComponent();
        }

        /// <summary>
        /// This property (getter) gives the current maximum capacity for the LogMessageSetTracker AdjustableLogMessageSetTracker instance that is currently in use.
        /// This property (setter) replaces the LogMessageSetTracker instance with a new instance if the new value does not match the current AdjustableLogMessageSetTracker instances MaximumCapacity.
        /// <para/>Defaults to 1000
        /// </summary>
        public int MaximumCapacity 
        {
            get { return LogMessageSetTracker.MaximumCapacity; }
            set
            {
                var currentSetTracker = LogMessageSetTracker;
                if (currentSetTracker == null || currentSetTracker.MaximumCapacity != value)
                {
                    LogMessageSetTracker = new AdjustableLogMessageSetTracker((currentSetTracker != null) ? currentSetTracker.SetName : null, maximumCapacity: value) 
                    { 
                        LogGate = (currentSetTracker != null) ? currentSetTracker.LogGate : MosaicLib.Logging.LogGate.Info, 
                        FilterString = (currentSetTracker != null) ? currentSetTracker.FilterString : string.Empty,
                    };
                }
            }
        }

        private static DependencyPropertyKey LogMessageSetTrackerPropertyKey = DependencyProperty.RegisterReadOnly("LogMessageSetTracker", typeof(AdjustableLogMessageSetTracker), typeof(LogView), new PropertyMetadata(null));
        public AdjustableLogMessageSetTracker LogMessageSetTracker 
        { 
            get { return _logMessageSetTracker; } 
            private set 
            {
                var prevSetTracker = _logMessageSetTracker;

                SetValue(LogMessageSetTrackerPropertyKey, (_logMessageSetTracker = value)); 

                _postAttemptToScrollToBottom = _postAttemptToScrollToBottom ?? PostAttemptToScrollToBottom;
                _postAttemptToScrollToBottomIfNotPaused = _postAttemptToScrollToBottomIfNotPaused ?? (() =>
                    {
                        if (!LogMessageSetTracker.Pause)
                            PostAttemptToScrollToBottom();
                    });

                if (prevSetTracker != null)
                {
                    prevSetTracker.NewItemsAdded -= _postAttemptToScrollToBottom;
                    prevSetTracker.SetRebuilt -= _postAttemptToScrollToBottomIfNotPaused;

                    BindingOperations.ClearBinding(prevSetTracker, AdjustableLogMessageSetTracker.LogGateProperty);
                    BindingOperations.ClearBinding(prevSetTracker, AdjustableLogMessageSetTracker.FilterStringProperty);
                }

                LogMessageSetTracker.NewItemsAdded += _postAttemptToScrollToBottom;
                LogMessageSetTracker.SetRebuilt += _postAttemptToScrollToBottomIfNotPaused;

                SetBinding(LogGateProperty, new Binding() { Source = LogMessageSetTracker, Path = new PropertyPath(AdjustableLogMessageSetTracker.LogGateProperty), Mode = BindingMode.TwoWay });
                SetBinding(FilterStringProperty, new Binding() { Source = LogMessageSetTracker, Path = new PropertyPath(AdjustableLogMessageSetTracker.FilterStringProperty), Mode = BindingMode.TwoWay });
            } 
        }
        private AdjustableLogMessageSetTracker _logMessageSetTracker;
        private BasicNotificationDelegate _postAttemptToScrollToBottom;
        private BasicNotificationDelegate _postAttemptToScrollToBottomIfNotPaused;

        public static DependencyProperty SetNameProperty = DependencyProperty.Register("SetName", typeof(string), typeof(LogView), new PropertyMetadata(null, HandleSetNamePropertyChanged));
        public static DependencyProperty EnabledSourcesProperty = DependencyProperty.Register("EnabledSources", typeof(object), typeof(LogView), new PropertyMetadata(null, HandleEnabledSourcesPropertyChanged));
        public static DependencyProperty DateColumnWidthProperty = DependencyProperty.Register("DateColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(90.0));
        public static DependencyProperty TypeColumnWidthProperty = DependencyProperty.Register("TypeColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(60.0));
        public static DependencyProperty SourceColumnWidthProperty = DependencyProperty.Register("SourceColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(120.0));
        public static DependencyProperty MesgColumnWidthProperty = DependencyProperty.Register("MesgColumnWidth", typeof(double), typeof(LogView), new PropertyMetadata(450.0));
        public static DependencyProperty LogGateProperty = DependencyProperty.Register("LogGate", typeof(MosaicLib.Logging.LogGate), typeof(LogView), new PropertyMetadata(AdjustableLogMessageSetTracker.DefaultLogGate));
        public static DependencyProperty FilterStringProperty = DependencyProperty.Register("FilterString", typeof(string), typeof(LogView), new PropertyMetadata(""));
        public static DependencyProperty ControlsVisibilityProperty = DependencyProperty.Register("ControlsVisibility", typeof(Visibility), typeof(LogView), new PropertyMetadata(Visibility.Visible));
        public static DependencyProperty FilterFieldWidthProperty = DependencyProperty.Register("FilterFieldWidth", typeof(double), typeof(LogView), new PropertyMetadata(120.0));

        public string SetName { get { return (string)GetValue(SetNameProperty); } set { SetValue(SetNameProperty, value); } }
        /// <summary>When set to an enumerable set of strings, or to a single string (with or without | delimiters) it limits the set of log sources that will be tracked</summary>
        public object EnabledSources { get { return (string)GetValue(EnabledSourcesProperty); } set { SetValue(EnabledSourcesProperty, value); } }
        public double DateColumnWidth { get { return (double)GetValue(DateColumnWidthProperty); } set { SetValue(DateColumnWidthProperty, value); } }
        public double TypeColumnWidth { get { return (double)GetValue(TypeColumnWidthProperty); } set { SetValue(TypeColumnWidthProperty, value); } }
        public double SourceColumnWidth { get { return (double)GetValue(SourceColumnWidthProperty); } set { SetValue(SourceColumnWidthProperty, value); } }
        public double MesgColumnWidth { get { return (double)GetValue(MesgColumnWidthProperty); } set { SetValue(MesgColumnWidthProperty, value); } }
        public MosaicLib.Logging.LogGate LogGate { get { return (MosaicLib.Logging.LogGate)GetValue(LogGateProperty); } set { SetValue(LogGateProperty, value); } }
        public string FilterString { get { return (string)GetValue(FilterStringProperty); } set { SetValue(FilterStringProperty, value); } }
        public Visibility ControlsVisibility { get { return (Visibility)GetValue(ControlsVisibilityProperty); } set { SetValue(ControlsVisibilityProperty, value); } }
        public double FilterFieldWidth { get { return (double)GetValue(FilterFieldWidthProperty); } set { SetValue(FilterFieldWidthProperty, value); } }

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
