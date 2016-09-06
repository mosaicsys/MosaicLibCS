﻿//-------------------------------------------------------------------
/*! @file ButtonIsPressedObserver.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved.
 * Copyright (c) 2016 Mosaic Systems Inc., All rights reserved.
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
using System.Text;
using System.Windows.Controls;
using MosaicLib.Utils;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.ComponentModel;

namespace MosaicLib.WPF.Extensions
{
    public static class IsPressedObserver
    {
        public static readonly DependencyProperty observerDP
            = DependencyProperty.RegisterAttached("Observe", typeof(bool), typeof(IsPressedObserver),
                                                    new FrameworkPropertyMetadata(OnObserveChanged));
        public static readonly DependencyProperty isPressedDP
            = DependencyProperty.RegisterAttached("IsPressed", typeof(bool), typeof(IsPressedObserver));

        public static bool GetObserve(FrameworkElement obj) { return (bool)obj.GetValue(observerDP); }
        public static void SetObserve(FrameworkElement obj, bool value) { obj.SetValue(observerDP, value); }

        public static bool GetIsPressed(FrameworkElement obj) { return (bool)obj.GetValue(isPressedDP); }
        public static void SetIsPressed(FrameworkElement obj, bool value) { obj.SetValue(isPressedDP, value); }

        private static void OnObserveChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            ButtonBase b = obj as ButtonBase;

            if (b != null)
            {
                var isPressedDescriptor = DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase));
                if ((bool)e.NewValue)
                {
                    isPressedDescriptor.AddValueChanged(b, IsPressedChanged);
                    UpdateObservedIsPresent(b);
                }
                else
                {
                    isPressedDescriptor.RemoveValueChanged(b, IsPressedChanged);
                }
            }
        }

        static void IsPressedChanged(object sender, EventArgs e)
        {
            UpdateObservedIsPresent(sender as ButtonBase);
        }

        static void UpdateObservedIsPresent(ButtonBase b)
        {
            if (b != null)
            {
                b.SetCurrentValue(isPressedDP, b.IsPressed);
            }
        }
    }
}