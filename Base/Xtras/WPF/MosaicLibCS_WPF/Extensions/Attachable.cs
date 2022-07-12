//-------------------------------------------------------------------
/*! @file Attachable.cs
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

        public static void SetTag1(FrameworkElement obj, object value) { obj.SetValue(Tag1Property, value); }
        public static void SetTag2(FrameworkElement obj, object value) { obj.SetValue(Tag2Property, value); }
        public static void SetTag3(FrameworkElement obj, object value) { obj.SetValue(Tag3Property, value); }
        public static void SetTag4(FrameworkElement obj, object value) { obj.SetValue(Tag4Property, value); }
        public static void SetTag5(FrameworkElement obj, object value) { obj.SetValue(Tag5Property, value); }
        public static void SetTag6(FrameworkElement obj, object value) { obj.SetValue(Tag6Property, value); }
        public static void SetTag7(FrameworkElement obj, object value) { obj.SetValue(Tag7Property, value); }
        public static void SetTag8(FrameworkElement obj, object value) { obj.SetValue(Tag8Property, value); }
        public static void SetTag9(FrameworkElement obj, object value) { obj.SetValue(Tag9Property, value); }

        #endregion

        #region InheritableTag1 .. InheritableTag9 properties

        public static readonly DependencyProperty InheritableTag1Property = DependencyProperty.RegisterAttached("InheritableTag1", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag2Property = DependencyProperty.RegisterAttached("InheritableTag2", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag3Property = DependencyProperty.RegisterAttached("InheritableTag3", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag4Property = DependencyProperty.RegisterAttached("InheritableTag4", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag5Property = DependencyProperty.RegisterAttached("InheritableTag5", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag6Property = DependencyProperty.RegisterAttached("InheritableTag6", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag7Property = DependencyProperty.RegisterAttached("InheritableTag7", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag8Property = DependencyProperty.RegisterAttached("InheritableTag8", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty InheritableTag9Property = DependencyProperty.RegisterAttached("InheritableTag9", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag1(FrameworkElement obj) { return obj.GetValue(InheritableTag1Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag2(FrameworkElement obj) { return obj.GetValue(InheritableTag2Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag3(FrameworkElement obj) { return obj.GetValue(InheritableTag3Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag4(FrameworkElement obj) { return obj.GetValue(InheritableTag4Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag5(FrameworkElement obj) { return obj.GetValue(InheritableTag5Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag6(FrameworkElement obj) { return obj.GetValue(InheritableTag6Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag7(FrameworkElement obj) { return obj.GetValue(InheritableTag7Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag8(FrameworkElement obj) { return obj.GetValue(InheritableTag8Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetInheritableTag9(FrameworkElement obj) { return obj.GetValue(InheritableTag9Property); }

        public static void SetInheritableTag1(FrameworkElement obj, object value) { obj.SetValue(InheritableTag1Property, value); }
        public static void SetInheritableTag2(FrameworkElement obj, object value) { obj.SetValue(InheritableTag2Property, value); }
        public static void SetInheritableTag3(FrameworkElement obj, object value) { obj.SetValue(InheritableTag3Property, value); }
        public static void SetInheritableTag4(FrameworkElement obj, object value) { obj.SetValue(InheritableTag4Property, value); }
        public static void SetInheritableTag5(FrameworkElement obj, object value) { obj.SetValue(InheritableTag5Property, value); }
        public static void SetInheritableTag6(FrameworkElement obj, object value) { obj.SetValue(InheritableTag6Property, value); }
        public static void SetInheritableTag7(FrameworkElement obj, object value) { obj.SetValue(InheritableTag7Property, value); }
        public static void SetInheritableTag8(FrameworkElement obj, object value) { obj.SetValue(InheritableTag8Property, value); }
        public static void SetInheritableTag9(FrameworkElement obj, object value) { obj.SetValue(InheritableTag9Property, value); }

        #endregion

        #region Group, CancellationGroup property

        public static readonly DependencyProperty GroupProperty = DependencyProperty.RegisterAttached("Group", typeof(object), typeof(Attachable));
        public static readonly DependencyProperty CancellationGroupProperty = DependencyProperty.RegisterAttached("CancellationGroup", typeof(object), typeof(Attachable));

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetGroup(FrameworkElement obj) { return obj.GetValue(GroupProperty); }
        public static void SetGroup(FrameworkElement obj, object value) { obj.SetValue(GroupProperty, value); }

        [AttachedPropertyBrowsableForType(typeof(Control))] public static object GetCancellationGroup(Control obj) { return obj.GetValue(GroupProperty); }
        public static void SetCancellationGroup(Control obj, object value) { obj.SetValue(GroupProperty, value); }

        #endregion

        #region NPV

        /// <summary>
        /// NPV attached/attachable property - applies to all Control types (GCD of ButtonBase and MenuItem - aka the ICommandSource objects)
        /// </summary>
        public static readonly DependencyProperty NPVProperty = DependencyProperty.RegisterAttached("NPV", typeof(INamedValueSet), typeof(Attachable));

        [AttachedPropertyBrowsableForType(typeof(Control))] public static INamedValueSet GetNPV(Control obj) { return (INamedValueSet)obj.GetValue(NPVProperty); }
        public static void SetNPV(Control obj, INamedValueSet value) { obj.SetValue(NPVProperty, value); }

        #endregion

        #region DenyReasonSet, DenyReason, DenyReason2, DenyReason3, DenyReason4, DenyReason5, OverrideEnyReasonSet, DenyReasonFallbackToolTip

        /// <summary>
        /// When set to a non-empty IList{string} and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip containing either the first string (if there is only one) or an ItemsControl with each of the strings in it.
        /// <para/>When multiple DenyReasonYYY properties have non-empty signaling contents, this infrastructure will use the first non-empty one evaluated in the order DenyReasonSet, DenyReason, DenyReason2, DenyReason3, DenyReason4, DenyReason5
        /// </summary>
        public static readonly DependencyProperty DenyReasonSetProperty = DependencyProperty.RegisterAttached("DenyReasonSet", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.DenyReasonSet, obj, ea)));

        /// <summary>
        /// When set to a non-empty string and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip with the assigned string contents.
        /// <para/>When multiple DenyReasonYYY properties have non-empty signaling contents, this infrastructure will use the first non-empty one evaluated in the order DenyReasonSet, DenyReason, DenyReason2, DenyReason3, DenyReason4, DenyReason5
        /// </summary>
        public static readonly DependencyProperty DenyReasonProperty = DependencyProperty.RegisterAttached("DenyReason", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.DenyReason, obj, ea)));

        /// <summary>
        /// When set to a non-empty string, or array of non-empty strings, and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip with the assigned string contents.
        /// <para/>When multiple DenyReasonYYY properties have non-empty signaling contents, this infrastructure will use the first non-empty one evaluated in the order DenyReasonSet, DenyReason, DenyReason2, DenyReason3, DenyReason4, DenyReason5
        /// </summary>
        public static readonly DependencyProperty DenyReason2Property = DependencyProperty.RegisterAttached("DenyReason2", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.DenyReason2, obj, ea)));

        /// <summary>
        /// When set to a non-empty string, or array of non-empty strings, and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip with the assigned string contents.
        /// <para/>When multiple DenyReasonYYY properties have non-empty signaling contents, this infrastructure will use the first non-empty one evaluated in the order DenyReasonSet, DenyReason, DenyReason2, DenyReason3, DenyReason4, DenyReason5
        /// </summary>
        public static readonly DependencyProperty DenyReason3Property = DependencyProperty.RegisterAttached("DenyReason3", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.DenyReason3, obj, ea)));

        /// <summary>
        /// When set to a non-empty string, or array of non-empty strings, and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip with the assigned string contents.
        /// <para/>When multiple DenyReasonYYY properties have non-empty signaling contents, this infrastructure will use the first non-empty one evaluated in the order DenyReasonSet, DenyReason, DenyReason2, DenyReason3, DenyReason4, DenyReason5
        /// </summary>
        public static readonly DependencyProperty DenyReason4Property = DependencyProperty.RegisterAttached("DenyReason4", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.DenyReason4, obj, ea)));

        /// <summary>
        /// When set to a non-empty string, or array of non-empty strings, and when the OverrideDenyReasonProperty has not been set to true, this property will disable the attached FrameworkElement and will attached a ShowOnDisabled ToolTip with the assigned string contents.
        /// <para/>When multiple DenyReasonYYY properties have non-empty signaling contents, this infrastructure will use the first non-empty one evaluated in the order DenyReasonSet, DenyReason, DenyReason2, DenyReason3, DenyReason4, DenyReason5
        /// </summary>
        public static readonly DependencyProperty DenyReason5Property = DependencyProperty.RegisterAttached("DenyReason5", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.DenyReason5, obj, ea)));

        /// <summary>
        /// When set to true this property will have the effect of suppressing the effects of a DenyReasonProperty or the DenyReasonSet property that is attached to the same FrameworkElement so that the element remains enabled even when the deny reason or deny reason set are non-empty.
        /// </summary>
        public static readonly DependencyProperty OverrideDenyReasonSetProperty = DependencyProperty.RegisterAttached("OverrideDenyReasonSet", typeof(bool), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.OverrideDenyReasonSet, obj, ea)));

        /// <summary>
        /// When set/bound to a non-null value this DP defines the tooltip contents that are to be displayed when ther is no active deny reason in use.
        /// </summary>
        public static readonly DependencyProperty DenyReasonFallbackToolTipProperty = DependencyProperty.RegisterAttached("DenyReasonFallbackToolTip", typeof(object), typeof(Attachable), new FrameworkPropertyMetadata((obj, ea) => OnDenyReasonItemChanged(ChangedItem.DenyReasonFallbackToolTip, obj, ea)));

        /// <summary>
        /// private DP that is used to record the initial tooltip contents so that DR attached properties can easily be used with objects that have a statically defined tooltip.
        /// </summary>
        private static readonly DependencyProperty DRSavedInitialToolTipProperty = DependencyProperty.RegisterAttached("DRSavedInitialToolTip", typeof(Tuple<object>), typeof(Attachable));

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetDenyReasonSet(FrameworkElement obj) { return obj.GetValue(DenyReasonSetProperty); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetDenyReason(FrameworkElement obj) { return obj.GetValue(DenyReasonProperty); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetOverrideDenyReasonSet(FrameworkElement obj) { return obj.GetValue(DenyReasonSetProperty); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetDenyReasonFallbackToolTip(FrameworkElement obj) { return obj.GetValue(DenyReasonFallbackToolTipProperty); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetDenyReason2(FrameworkElement obj) { return obj.GetValue(DenyReason2Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetDenyReason3(FrameworkElement obj) { return obj.GetValue(DenyReason3Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetDenyReason4(FrameworkElement obj) { return obj.GetValue(DenyReason4Property); }
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))] public static object GetDenyReason5(FrameworkElement obj) { return obj.GetValue(DenyReason5Property); }

        public static void SetDenyReasonSet(FrameworkElement obj, object value) { obj.SetValue(DenyReasonSetProperty, value); }
        public static void SetDenyReason(FrameworkElement obj, object value) { obj.SetValue(DenyReasonProperty, value); }
        public static void SetOverrideDenyReasonSet(FrameworkElement obj, object value) { obj.SetValue(DenyReasonSetProperty, value); }
        public static void SetDenyReasonFallbackToolTip(FrameworkElement obj, object value) { obj.SetValue(DenyReasonFallbackToolTipProperty, value); }
        public static void SetDenyReason2(FrameworkElement obj, object value) { obj.SetValue(DenyReason2Property, value); }
        public static void SetDenyReason3(FrameworkElement obj, object value) { obj.SetValue(DenyReason3Property, value); }
        public static void SetDenyReason4(FrameworkElement obj, object value) { obj.SetValue(DenyReason4Property, value); }
        public static void SetDenyReason5(FrameworkElement obj, object value) { obj.SetValue(DenyReason5Property, value); }

        private enum ChangedItem : int
        {
            DenyReasonSet,
            DenyReason,
            DenyReason2,
            DenyReason3,
            DenyReason4,
            DenyReason5,
            OverrideDenyReasonSet,
            DenyReasonFallbackToolTip,
        }

        private static void OnDenyReasonItemChanged(ChangedItem changedItem, DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            object denyReasonSet = obj.GetValue(DenyReasonSetProperty);
            object denyReason = obj.GetValue(DenyReasonProperty);
            object denyReason2 = obj.GetValue(DenyReason2Property);
            object denyReason3 = obj.GetValue(DenyReason3Property);
            object denyReason4 = obj.GetValue(DenyReason4Property);
            object denyReason5 = obj.GetValue(DenyReason5Property);

            bool overrideDenyReason = ((obj.GetValue(OverrideDenyReasonSetProperty) as bool?) ?? false);
            object fallbackToolTip = obj.GetValue(DenyReasonFallbackToolTipProperty);

            switch (changedItem)
            {
                case ChangedItem.DenyReasonSet: denyReasonSet = e.NewValue; break;
                case ChangedItem.DenyReason: denyReason = e.NewValue; break;
                case ChangedItem.DenyReason2: denyReason2 = e.NewValue; break;
                case ChangedItem.DenyReason3: denyReason3 = e.NewValue; break;
                case ChangedItem.DenyReason4: denyReason4 = e.NewValue; break;
                case ChangedItem.DenyReason5: denyReason5 = e.NewValue; break;
                case ChangedItem.OverrideDenyReasonSet: overrideDenyReason = ((e.NewValue as bool?) ?? false); break;
                case ChangedItem.DenyReasonFallbackToolTip: fallbackToolTip = e.NewValue; break;
                default: break;
            }

            object[] drsObjArray = ConvertToArrayOfDenyReasonObjects(denyReasonSet);
            object[] drObjArray = ConvertToArrayOfDenyReasonObjects(denyReason);
            object[] dr2ObjArray = ConvertToArrayOfDenyReasonObjects(denyReason2);
            object[] dr3ObjArray = ConvertToArrayOfDenyReasonObjects(denyReason3);
            object[] dr4ObjArray = ConvertToArrayOfDenyReasonObjects(denyReason4);
            object[] dr5ObjArray = ConvertToArrayOfDenyReasonObjects(denyReason5);

            object[] denyReasonObjArray = drsObjArray ?? drObjArray ?? dr2ObjArray ?? dr3ObjArray ?? dr4ObjArray ?? dr5ObjArray ?? EmptyArrayFactory<object>.Instance;

            {
                var initialToolTipValue = obj.GetValue(DRSavedInitialToolTipProperty) as Tuple<object>;
                if (initialToolTipValue == null)
                {
                    obj.SetValue(DRSavedInitialToolTipProperty, initialToolTipValue = Tuple.Create<object>(obj.GetValue(FrameworkElement.ToolTipProperty)));
                }

                object disabledToolTipValue = null;

                switch (denyReasonObjArray.Length)
                {
                    case 0: break;
                    case 1: disabledToolTipValue = (!overrideDenyReason ? denyReasonObjArray[0] : null); break;
                    default: disabledToolTipValue = (!overrideDenyReason ? (new ItemsControl() { ItemsSource = denyReasonObjArray }) : null); break;
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

                        object normalToolTipValue = fallbackToolTip ?? (initialToolTipValue != null ? initialToolTipValue.Item1 : null);

                        if (normalToolTipValue != null)
                            fwElem.SetValue(FrameworkElement.ToolTipProperty, normalToolTipValue);
                        else
                            fwElem.ClearValue(FrameworkElement.ToolTipProperty);
                    }
                }
            }
        }

        private static object[] ConvertToArrayOfDenyReasonObjects(object objIn)
        {
            object[] objArrayOut = null;

            if (objIn is string)
                objArrayOut = new object[] { objIn };
            else if (objIn is IEnumerable)
                objArrayOut = (objIn as IEnumerable).SafeToArray<object>();
            else
                objArrayOut = new object[] { objIn };

            // filter out all of the nulls and empty strings from the list
            objArrayOut = objArrayOut.Where(obj => !(obj == null || (obj as string) == string.Empty)).ToArray();

            // map any resulting empty array to null.
            return objArrayOut.MapEmptyToNull();
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
