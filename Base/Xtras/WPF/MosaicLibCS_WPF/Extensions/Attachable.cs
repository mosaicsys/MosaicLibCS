//-------------------------------------------------------------------
/*! @file ButtonIsPressedObserver.cs
 *  @brief
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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Controls.Primitives;

using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.WPF.Extensions
{
    /// <summary>
    /// This is a set of common semi-generic, Attachable dependancy properties.
    /// <para/>DenyReason (string), DenyReasonSet (IList{string})
    /// </summary>
    public static class Attachable
    {
        public static readonly DependencyProperty DenyReasonProperty = DependencyProperty.RegisterAttached("DenyReason", typeof(string), typeof(Attachable), new FrameworkPropertyMetadata(OnDenyReasonPropertyChanged));
        public static readonly DependencyProperty DenyReasonSetProperty = DependencyProperty.RegisterAttached("DenyReasonSet", typeof(IList<string>), typeof(Attachable), new FrameworkPropertyMetadata(OnDenyReasonSetPropertyChanged));

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static object GetDenyReason(FrameworkElement obj) { return obj.GetValue(DenyReasonProperty); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static void SetDenyReason(FrameworkElement obj, object value) { obj.SetValue(DenyReasonProperty, value); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static object GetDenyReasonSet(FrameworkElement obj) { return obj.GetValue(DenyReasonSetProperty); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static void SetDenyReasonSet(FrameworkElement obj, object value) { obj.SetValue(DenyReasonSetProperty, value); }

        private static void OnDenyReasonPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            string denyReason = e.NewValue as string;
            obj.SetValue(DenyReasonSetProperty, denyReason.IsNullOrEmpty() ? null : new ReadOnlyIList<string>(denyReason));
        }

        private static void OnDenyReasonSetPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            IList<string> denyReasonList = e.NewValue as IList<string>;

            object disabledToolTipValue = null;

            switch (denyReasonList.SafeCount())
            {
                case 0: break;
                case 1: disabledToolTipValue = denyReasonList[0]; break;
                default: disabledToolTipValue = new ItemsControl() { ItemsSource = denyReasonList }; break;
            }

            FrameworkElement fwElem = obj as FrameworkElement;

            if (fwElem != null)
            {
                if (disabledToolTipValue != null)
                {
                    fwElem.SetValue(FrameworkElement.IsEnabledProperty, false);
                    fwElem.SetValue(ToolTipService.ShowOnDisabledProperty, true);
                    fwElem.SetValue(FrameworkElement.ToolTipProperty, disabledToolTipValue);
                }
                else
                {
                    fwElem.SetValue(FrameworkElement.IsEnabledProperty, true);
                    fwElem.SetValue(ToolTipService.ShowOnDisabledProperty, false);
                    fwElem.ClearValue(FrameworkElement.ToolTipProperty);
                }
            }
        }
    }
}
