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

            LogMessageSetTracker.NewItemsAdded += PostAttemptToScrollToEnd;
            LogMessageSetTracker.SetRebuilt += () => 
                                                { 
                                                    if (!LogMessageSetTracker.Pause) 
                                                        PostAttemptToScrollToEnd(); 
                                                };

            IsVisibleChanged += LogView_IsVisibleChanged;

            /// Todo: move me up to top of method
            InitializeComponent();
        }

        public static DependencyProperty SetNameProperty = DependencyProperty.Register("SetName", typeof(string), typeof(LogView), new PropertyMetadata(null, HandleSetNamePropertyChanged));

        public string SetName { get { return (string)GetValue(SetNameProperty); } set { SetValue(SetNameProperty, value); } }

        private static void HandleSetNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            LogView me = d as LogView;

            if (me != null)
                me.LogMessageSetTracker.SetName = (string)e.NewValue;
        }

        void LogView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool) e.NewValue == true && !LogMessageSetTracker.Pause)
            {
                PostAttemptToScrollToEnd();
            }
        }

        void PostAttemptToScrollToEnd()
        {
            System.Threading.SynchronizationContext.Current.Post(o => AttemptToScrollToEnd(), this);
        }

        private void AttemptToScrollToEnd()
        {
            if (lvItems.Items.Count > 0)
            {
                object lastLVItem = lvItems.Items[lvItems.Items.Count - 1];

                lvItems.ScrollIntoView(lastLVItem);
            }
        }

        public AdjustableLogMessageSetTracker LogMessageSetTracker { get; private set; }

        private void HandleClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessageSetTracker.Clear();
        }

        private void HandleScrolltoEndButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptToScrollToEnd();
        }
    }
}
