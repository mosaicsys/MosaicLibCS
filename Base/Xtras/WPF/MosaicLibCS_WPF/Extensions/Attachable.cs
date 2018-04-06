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
    /// <para/>DenyReason (string), DenyReasonSet (IList{string}), OverrideDenyReason (bool)
    /// </summary>
    public static class Attachable
    {
        /// <summary>
        /// When set to a non-empty string and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip with the assigned string contents.
        /// Note: internally this property makes use of the DenyReasonSetProperty.  Simultanious use of both properties may produce unexpected results.
        /// </summary>
        public static readonly DependencyProperty DenyReasonProperty = DependencyProperty.RegisterAttached("DenyReason", typeof(string), typeof(Attachable), new FrameworkPropertyMetadata(OnDenyReasonPropertyChanged));

        /// <summary>
        /// When set to a non-empty IList{string} and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip containing either the first string (if there is only one) or an ItemsControl with each of the strings in it.
        /// Note: internally the DenyReasonProperty is implemented using this one.  Simultanious use of both properties may produced unexpected results.
        /// </summary>
        public static readonly DependencyProperty DenyReasonSetProperty = DependencyProperty.RegisterAttached("DenyReasonSet", typeof(IList<string>), typeof(Attachable), new FrameworkPropertyMetadata(OnDenyReasonSetPropertyChanged));

        /// <summary>
        /// When set to true this property will have the effect of suppressing the effects of a DenyReasonProperty or the DenyReasonSet property that is attached to the same FrameworkElement so that the element remains enabled even when the deny reason or deny reason set are non-empty.
        /// </summary>
        public static readonly DependencyProperty OverrideDenyReasonSetProperty = DependencyProperty.RegisterAttached("OverrideDenyReasonSet", typeof(bool), typeof(Attachable), new FrameworkPropertyMetadata(OnOverrideDenyReasonSetPropertyChanged));

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static object GetDenyReason(FrameworkElement obj) { return obj.GetValue(DenyReasonProperty); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static void SetDenyReason(FrameworkElement obj, object value) { obj.SetValue(DenyReasonProperty, value); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static object GetDenyReasonSet(FrameworkElement obj) { return obj.GetValue(DenyReasonSetProperty); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static void SetDenyReasonSet(FrameworkElement obj, object value) { obj.SetValue(DenyReasonSetProperty, value); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static object GetOverrideDenyReasonSet(FrameworkElement obj) { return obj.GetValue(DenyReasonSetProperty); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static void SetOverrideDenyReasonSet(FrameworkElement obj, object value) { obj.SetValue(DenyReasonSetProperty, value); }

        private static void OnDenyReasonPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            string denyReason = e.NewValue as string;
            obj.SetValue(DenyReasonSetProperty, denyReason.IsNullOrEmpty() ? null : new ReadOnlyIList<string>(denyReason));
        }

        private static void OnDenyReasonSetPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            IList<string> denyReasonSet = e.NewValue as IList<string>;

            bool overrideDenyReason = ((obj.GetValue(OverrideDenyReasonSetProperty) as bool ?) ?? false);

            UpdateDenyReasonSetAndOverride(obj, denyReasonSet, overrideDenyReason);
        }

        private static void OnOverrideDenyReasonSetPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            bool overrideDenyReason = (e.NewValue as bool?) ?? false;

            IList<string> denyReasonSet = obj.GetValue(DenyReasonSetProperty) as IList<string>;

            UpdateDenyReasonSetAndOverride(obj, denyReasonSet, overrideDenyReason);
        }

        private static void UpdateDenyReasonSetAndOverride(DependencyObject obj, IList<string> denyReasonSet, bool overrideDenyReason)
        {
            object disabledToolTipValue = null;

            switch (denyReasonSet.SafeCount())
            {
                case 0: break;
                case 1: disabledToolTipValue = (!overrideDenyReason ? denyReasonSet[0] : null); break;
                default: disabledToolTipValue = (!overrideDenyReason ? (new ItemsControl() { ItemsSource = denyReasonSet }) : null); break;
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
