//-------------------------------------------------------------------
/*! @file E84_PtoA_Control.cs
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
    /// Interaction logic for E84_PtoA_Control.xaml
    /// </summary>
    public partial class E84_PtoA_Control : UserControl
    {
        public E84_PtoA_Control()
        {
            InitializeComponent();
        }

        private static readonly DependencyProperty pinsStateDP = DependencyProperty.Register("PinsState", typeof(object), typeof(E84_PtoA_Control));

        public object PinsState { get { return GetValue(pinsStateDP); } set { SetValue(pinsStateDP, value); } }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == pinsStateDP)
            {
                PassiveToActivePinsState p2aPinsState = new PassiveToActivePinsState(e.NewValue);

                if (E84_L_REQ_CB.IsChecked != p2aPinsState.L_REQ)
                    E84_L_REQ_CB.IsChecked = p2aPinsState.L_REQ;
                if (E84_U_REQ_CB.IsChecked != p2aPinsState.U_REQ)
                    E84_U_REQ_CB.IsChecked = p2aPinsState.U_REQ;
                if (E84_READY_CB.IsChecked != p2aPinsState.READY)
                    E84_READY_CB.IsChecked = p2aPinsState.READY;
                if (E84_HO_AVBL_CB.IsChecked != p2aPinsState.HO_AVBL)
                    E84_HO_AVBL_CB.IsChecked = p2aPinsState.HO_AVBL;
                if (E84_ES_CB.IsChecked != p2aPinsState.ES)
                    E84_ES_CB.IsChecked = p2aPinsState.ES;

                UpdateLabelContents(p2aPinsState);
            }
            else
            {
                base.OnPropertyChanged(e);
            }
        }

        private void UpdateLabelContents(PassiveToActivePinsState state)
        {
            string pwStr = "${0:X4}".CheckedFormat(unchecked((int)state.PackedWord));

            if (pwStr != (packedWordLabel.Content as string))
            {
                packedWordLabel.Content = pwStr;
            }
        }

        private void E084_PtoA_CheckBox_Clicked(object sender, RoutedEventArgs e)
        {
            PassiveToActivePinsState p2aPinsState = new PassiveToActivePinsState(PinsState);
            PassiveToActivePinBits entryPackedWord = p2aPinsState.PackedWord;

            p2aPinsState.L_REQ = E84_L_REQ_CB.IsChecked.GetValueOrDefault();
            p2aPinsState.U_REQ = E84_U_REQ_CB.IsChecked.GetValueOrDefault();
            p2aPinsState.READY = E84_READY_CB.IsChecked.GetValueOrDefault();
            p2aPinsState.HO_AVBL = E84_HO_AVBL_CB.IsChecked.GetValueOrDefault();
            p2aPinsState.ES = E84_ES_CB.IsChecked.GetValueOrDefault();
            p2aPinsState.IFaceName = "From:E084_PtoA_CheckBox_Clicked";

            UpdateLabelContents(p2aPinsState);

            if (entryPackedWord != p2aPinsState.PackedWord)
            {
                PinsState = p2aPinsState as IPassiveToActivePinsState;
            }
        }
    }
}
