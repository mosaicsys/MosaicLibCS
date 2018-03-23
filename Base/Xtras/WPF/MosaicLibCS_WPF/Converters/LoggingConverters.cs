//-------------------------------------------------------------------
/*! @file LoggingConverters.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2018 Mosaic Systems Inc.
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
using System.Windows;
using System.Windows.Data;
using System.Globalization;

using MosaicLib.Modular.Common;

namespace MosaicLib.WPF.Converters
{
	public class LogGateStringConverter: IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
            ValueContainer vc = ValueContainer.CreateFromObject(value);

            if (targetType == typeof(string) || targetType == typeof(object))
                return vc.GetValue<string>(rethrow: false);
            else if (targetType == typeof(MosaicLib.Logging.LogGate))
                return vc.GetValue<MosaicLib.Logging.LogGate>(rethrow: false);
            else
                return Binding.DoNothing;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
            return Convert(value, targetType, parameter, culture);
        }
	}
}
