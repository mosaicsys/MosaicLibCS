//-------------------------------------------------------------------
/*! @file E84_AtoP_Display.cs
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
    /// Interaction logic for E84_AtoP_Display.xaml
    /// </summary>
    public partial class E84_AtoP_Display : UserControl
    {
        public E84_AtoP_Display()
        {
            InitializeComponent();
        }

        private static readonly DependencyProperty pinsStateDP = DependencyProperty.Register("PinsState", typeof(object), typeof(E84_AtoP_Display));

        public object PinsState { get { return GetValue(pinsStateDP); } set { SetValue(pinsStateDP, value); } }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == pinsStateDP)
            {
                ActiveToPassivePinsState a2pPinsState = new ActiveToPassivePinsState(e.NewValue);

                if (E84_VALID_LED.IsActive != a2pPinsState.VALID)
                    E84_VALID_LED.IsActive = a2pPinsState.VALID;
                if (E84_CS_0_LED.IsActive != a2pPinsState.CS_0)
                    E84_CS_0_LED.IsActive = a2pPinsState.CS_0;
                if (E84_CS_1_LED.IsActive != a2pPinsState.CS_1)
                    E84_CS_1_LED.IsActive = a2pPinsState.CS_1;
                if (E84_TR_REQ_LED.IsActive != a2pPinsState.TR_REQ)
                    E84_TR_REQ_LED.IsActive = a2pPinsState.TR_REQ;
                if (E84_BUSY_LED.IsActive != a2pPinsState.BUSY)
                    E84_BUSY_LED.IsActive = a2pPinsState.BUSY;
                if (E84_COMPT_LED.IsActive != a2pPinsState.COMPT)
                    E84_COMPT_LED.IsActive = a2pPinsState.COMPT;
                if (E84_XferILock_LED.IsActive != a2pPinsState.XferILock)
                    E84_XferILock_LED.IsActive = a2pPinsState.XferILock;

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
    }
}
