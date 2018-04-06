//-------------------------------------------------------------------
/*! @file LogView.xaml.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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

            InitializeComponent();
        }

        public static DependencyProperty SetNameProperty = DependencyProperty.Register("SetName", typeof(string), typeof(LogView), new PropertyMetadata(null, HandleSetNamePropertyChanged));
        public static DependencyProperty EnabledSourcesProperty = DependencyProperty.Register("EnabledSources", typeof(object), typeof(LogView), new PropertyMetadata(null, HandleEnabledSourcesPropertyChanged));

        public string SetName { get { return (string)GetValue(SetNameProperty); } set { SetValue(SetNameProperty, value); } }
        public object EnabledSources { get { return (string)GetValue(EnabledSourcesProperty); } set { SetValue(EnabledSourcesProperty, value); } }

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

        private void AttemptToScrollTo(ScrollTo scrollTo)
        {
            if (listView.Items.Count > 0)
            {
                object showLVItem = listView.Items[(scrollTo == ScrollTo.Bottom) ? listView.Items.Count - 1 : 0];

                listView.ScrollIntoView(showLVItem);
            }
        }

        private enum ScrollTo
        {
            Top,
            Bottom
        }
    }
}
