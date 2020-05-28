//-------------------------------------------------------------------
/*! @file SemiConverters.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2020 Mosaic Systems Inc.
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
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Semi.E039;
using MosaicLib.Semi.E039.Accumulators;

namespace MosaicLib.WPF.Converters.Semi
{
    /// <summary>
    /// Supports bindable, one way, conversion of an IE039Object to and E039AccumulatorInfo object.
    /// <para/>The ConvertBack method alsways returns Binding.DoNothing
    /// </summary>
    public class IE039ObjectToE039AccumulatorInfoConverter : OneWayValueConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new E039AccumulatorInfo(value as IE039Object);
        }
    }
}