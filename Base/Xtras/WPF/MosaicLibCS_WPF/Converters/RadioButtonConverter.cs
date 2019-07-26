//-------------------------------------------------------------------
/*! @file RadioButtonConverter.cs
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
using System.Windows;
using System.Windows.Data;
using System.Globalization;

using MosaicLib.Utils;

namespace MosaicLib.WPF.Converters
{
	public class RadioButtonConverter: IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
            var valueStr = value.SafeToString().Replace(", ", ",");
            var parameterStr = parameter.SafeToString().Replace(", ", ",");

            return valueStr == parameterStr;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
            var parameterStr = parameter.SafeToString().Replace(", ", ",");

			if (object.Equals(value, true))
                return parameterStr;
			else
				return Binding.DoNothing;
		}
	}

}
