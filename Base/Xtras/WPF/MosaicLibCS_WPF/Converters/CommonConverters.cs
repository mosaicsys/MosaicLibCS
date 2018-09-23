//-------------------------------------------------------------------
/*! @file CommonConverters.cs
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
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;
using System.Collections;

namespace MosaicLib.WPF.Converters
{
    /// <summary>
    /// Supports bindable, one way, conversion of an object to a string by calling SafeToString() on it.
    /// <para/>The ConvertBack method alsways returns Binding.DoNothing
    /// </summary>
    public class ObjectToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.SafeToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Supports bindable, one way, conversion of an object to a string by creating a value container for the given object and then calling ToStringSML on it.
    /// <para/>The ConvertBack method alsways returns Binding.DoNothing
    /// </summary>
    public class VCToStringSMLConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
			return ValueContainer.CreateFromObject(value).ToStringSML();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Supports bindable, one way, conversion of any IEnumerable object to a string using String.Join(", ", set.Select(o.ToString()))
    /// </summary>
    public class SetToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IEnumerable set = value as IEnumerable;
            string delimiter = (parameter as string) ?? ", ";

            return String.Join(delimiter, set.SafeToSet().Select(o => o.SafeToString()));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
