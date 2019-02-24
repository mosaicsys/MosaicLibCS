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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;

using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace MosaicLib.WPF.Extensions
{
    /// <summary>
    /// This is a set of common semi-generic, Attachable dependancy properties.
    /// <para/>Tag1 ... Tag9
    /// <para/>NPV
    /// <para/>DenyReason (string), DenyReasonSet (IList{string}), OverrideDenyReason (bool)
    /// </summary>
    public static class Attachable
    {
        #region Tag1, Tag2, Tag3, Tag4, Tag5, Tag6, Tag7, Tag8, Tag9 properties

        public static readonly DependencyProperty Tag1Property = DependencyProperty.RegisterAttached("Tag1", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag2Property = DependencyProperty.RegisterAttached("Tag2", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag3Property = DependencyProperty.RegisterAttached("Tag3", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag4Property = DependencyProperty.RegisterAttached("Tag4", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag5Property = DependencyProperty.RegisterAttached("Tag5", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag6Property = DependencyProperty.RegisterAttached("Tag6", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag7Property = DependencyProperty.RegisterAttached("Tag7", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag8Property = DependencyProperty.RegisterAttached("Tag8", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty Tag9Property = DependencyProperty.RegisterAttached("Tag9", typeof(object), typeof(Attachable));

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag1(FrameworkElement obj) { return obj.GetValue(Tag1Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag2(FrameworkElement obj) { return obj.GetValue(Tag2Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag3(FrameworkElement obj) { return obj.GetValue(Tag3Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag4(FrameworkElement obj) { return obj.GetValue(Tag4Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag5(FrameworkElement obj) { return obj.GetValue(Tag5Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag6(FrameworkElement obj) { return obj.GetValue(Tag6Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag7(FrameworkElement obj) { return obj.GetValue(Tag7Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag8(FrameworkElement obj) { return obj.GetValue(Tag8Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetTag9(FrameworkElement obj) { return obj.GetValue(Tag9Property); }

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag1(FrameworkElement obj, object value) { obj.SetValue(Tag1Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag2(FrameworkElement obj, object value) { obj.SetValue(Tag2Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag3(FrameworkElement obj, object value) { obj.SetValue(Tag3Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag4(FrameworkElement obj, object value) { obj.SetValue(Tag4Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag5(FrameworkElement obj, object value) { obj.SetValue(Tag5Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag6(FrameworkElement obj, object value) { obj.SetValue(Tag6Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag7(FrameworkElement obj, object value) { obj.SetValue(Tag7Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag8(FrameworkElement obj, object value) { obj.SetValue(Tag8Property, value); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static void SetTag9(FrameworkElement obj, object value) { obj.SetValue(Tag9Property, value); }

        #endregion

        #region NPV

        /// <summary>
        /// NPV attached/attachable property - applies to all Control types (GCD of ButtonBase and MenuItem - aka the ICommandSource objects)
        /// </summary>
        public static readonly DependencyProperty NPVProperty = DependencyProperty.RegisterAttached("NPV", typeof(INamedValueSet), typeof(Attachable));
        [AttachedPropertyBrowsableForType(typeof(Control))] public static INamedValueSet GetNPV(Control obj) { return (INamedValueSet)obj.GetValue(NPVProperty); }
        [AttachedPropertyBrowsableForType(typeof(Control))] public static void SetNPV(Control obj, INamedValueSet value) { obj.SetValue(NPVProperty, value); }

        #endregion

        #region DenyReason, DenyReasonSet, OverrideEnyReasonSet properties

        /// <summary>
        /// When set to a non-empty string and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip with the assigned string contents.
        /// Note: internally this property makes use of the DenyReasonSetProperty.  Simultanious use of both properties may produce unexpected results.
        /// </summary>
        public static readonly DependencyProperty DenyReasonProperty = DependencyProperty.RegisterAttached("DenyReason", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(OnDenyReasonPropertyChanged));

        /// <summary>
        /// When set to a non-empty IList{string} and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip containing either the first string (if there is only one) or an ItemsControl with each of the strings in it.
        /// Note: internally the DenyReasonProperty is implemented using this one.  Simultanious use of both properties may produced unexpected results.
        /// </summary>
        public static readonly DependencyProperty DenyReasonSetProperty = DependencyProperty.RegisterAttached("DenyReasonSet", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(OnDenyReasonSetPropertyChanged));

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
            ValueContainer newValueVC = ValueContainer.CreateFromObject(e.NewValue);

            string denyReason = newValueVC.GetValue<string>(rethrow: true).MapNullToEmpty();

            obj.SetValue(DenyReasonSetProperty, denyReason.IsNullOrEmpty() ? null : new ReadOnlyIList<string>(denyReason));
        }

        private static void OnDenyReasonSetPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            ValueContainer newValueVC = SpecialCreateVCFromObjectForDenyReasonSet(e.NewValue);
            IList<string> denyReasonSet = newValueVC.GetValue<IList<string>>(rethrow: true).MapNullToEmpty();

            bool overrideDenyReason = ((obj.GetValue(OverrideDenyReasonSetProperty) as bool ?) ?? false);

            UpdateDenyReasonSetAndOverride(obj, denyReasonSet, overrideDenyReason);
        }

        private static void OnOverrideDenyReasonSetPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            ValueContainer newValueVC = ValueContainer.CreateFromObject(e.NewValue);
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
                    fwElem.ClearValue(FrameworkElement.IsEnabledProperty);
                    fwElem.ClearValue(ToolTipService.ShowOnDisabledProperty);
                    fwElem.ClearValue(FrameworkElement.ToolTipProperty);
                }
            }
        }

        #endregion

        #region local private static methods

        public static ValueContainer SpecialCreateVCFromObjectForDenyReasonSet(object obj)
        {
            if (!(obj is String))
            {
                IEnumerable objAsEnumerable = obj as IEnumerable;

                if (objAsEnumerable != null)
                {
                    var objAsArray = objAsEnumerable.SafeToSet().SafeToArray();
                    if (objAsArray.All(item => item is string))
                        return ValueContainer.CreateFromSet(objAsEnumerable.SafeToSet<string>());
                    else
                        return ValueContainer.CreateFromSet(objAsEnumerable.SafeToSet());
                }
            }

            return ValueContainer.CreateFromObject(obj);
        }

        #endregion
    }
}
