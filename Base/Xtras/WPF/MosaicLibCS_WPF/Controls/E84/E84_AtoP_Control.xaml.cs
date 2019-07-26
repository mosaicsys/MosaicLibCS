//-------------------------------------------------------------------
/*! @file E84_AtoP_Control.xaml.cs
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

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Semi.E084;
using MosaicLib.Modular.Common;

namespace MosaicLib.WPF.Controls.E84
{
    /// <summary>
    /// Interaction logic for E84_AtoP_Control.xaml
    /// </summary>
    public partial class E84_AtoP_Control : UserControl
    {
        public E84_AtoP_Control()
        {
            InitializeComponent();
        }

        private static readonly DependencyProperty pinsStateDP = DependencyProperty.Register("PinsState", typeof(object), typeof(E84_AtoP_Control));

        public object PinsState { get { return GetValue(pinsStateDP); } set { SetValue(pinsStateDP, value); } }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == pinsStateDP)
            {
                ActiveToPassivePinsState a2pPinsState = new ActiveToPassivePinsState(e.NewValue);

                if (E84_VALID_CB.IsChecked != a2pPinsState.VALID)
                    E84_VALID_CB.IsChecked = a2pPinsState.VALID;
                if (E84_CS0_CB.IsChecked != a2pPinsState.CS_0)
                    E84_CS0_CB.IsChecked = a2pPinsState.CS_0;
                if (E84_CS1_CB.IsChecked != a2pPinsState.CS_1)
                    E84_CS1_CB.IsChecked = a2pPinsState.CS_1;
                if (E84_TR_REQ_CB.IsChecked != a2pPinsState.TR_REQ)
                    E84_TR_REQ_CB.IsChecked = a2pPinsState.TR_REQ;
                if (E84_BUSY_CB.IsChecked != a2pPinsState.BUSY)
                    E84_BUSY_CB.IsChecked = a2pPinsState.BUSY;
                if (E84_COMPT_CB.IsChecked != a2pPinsState.COMPT)
                    E84_COMPT_CB.IsChecked = a2pPinsState.COMPT;
                if (E84_XferILock_CB.IsChecked != a2pPinsState.XferILock)
                    E84_XferILock_CB.IsChecked = a2pPinsState.XferILock;

                UpdateLabelContents(a2pPinsState);
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        private void UpdateLabelContents(ActiveToPassivePinsState state)
        {
            string pwStr = "${0:X4}".CheckedFormat(unchecked((int) state.PackedWord));

            if (pwStr != (packedWordLabel.Content as string))
            {
                packedWordLabel.Content = pwStr;
            }
        }

        private void E084_AtoP_CheckBox_Clicked(object sender, RoutedEventArgs e)
        {
            ActiveToPassivePinsState a2pPinsState = new ActiveToPassivePinsState(PinsState);
            ActiveToPassivePinBits entryPackedWord = a2pPinsState.PackedWord;

            a2pPinsState.CS_0 = E84_CS0_CB.IsChecked.GetValueOrDefault();
            a2pPinsState.CS_1 = E84_CS1_CB.IsChecked.GetValueOrDefault();
            a2pPinsState.VALID = E84_VALID_CB.IsChecked.GetValueOrDefault();
            a2pPinsState.TR_REQ = E84_TR_REQ_CB.IsChecked.GetValueOrDefault();
            a2pPinsState.BUSY = E84_BUSY_CB.IsChecked.GetValueOrDefault();
            a2pPinsState.COMPT = E84_COMPT_CB.IsChecked.GetValueOrDefault();
            a2pPinsState.XferILock = E84_XferILock_CB.IsChecked.GetValueOrDefault();
            a2pPinsState.IFaceName = "From:E084_AtoP_CheckBox_Clicked";

            UpdateLabelContents(a2pPinsState);

            if (entryPackedWord != a2pPinsState.PackedWord)
            {
                PinsState = a2pPinsState as IActiveToPassivePinsState;
            }
        }
    }
}
