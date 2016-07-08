//-------------------------------------------------------------------
/*! @file E84_PassiveSide_CombinedControl.cs
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using MosaicLib.Modular.Common;

namespace MosaicLib.WPF.Controls.E84
{
    /// <summary>
    /// Interaction logic for E84_PassiveSide_CombinedControl.xaml
    /// </summary>
    public partial class E84_PassiveSide_CombinedControl : UserControl
    {
        public E84_PassiveSide_CombinedControl()
        {
            InitializeComponent();
        }

        private static readonly DependencyProperty passiveToActiveStateDP = DependencyProperty.Register("PassiveToActivePinsState", typeof(object), typeof(E84_PassiveSide_CombinedControl));
        private static readonly DependencyProperty activeToPassiveStateDP = DependencyProperty.Register("ActiveToPassivePinsState", typeof(object), typeof(E84_PassiveSide_CombinedControl));

        public object PassiveToActivePinsState { get { return GetValue(passiveToActiveStateDP); } set { SetValue(passiveToActiveStateDP, value); } }
        public object ActiveToPassivePinsState { get { return GetValue(activeToPassiveStateDP); } set { SetValue(activeToPassiveStateDP, value); } }
    }
}
