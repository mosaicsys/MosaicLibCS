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
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;

namespace MosaicLib.WPF.Converters
{
    /// <summary>
    /// Supports bindable conversion between LogGate values and their corresonding string values.  Makes use of ValueContainer's built in converters for these types.
    /// </summary>
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

    /// <summary>
    /// Supports bindable, one way, conversion of an ILogMessage to a string by optionally concatinating the message's Mesg property with string formatted versions of its NamedValueSet and Data properties.
    /// <seealso cref="LogMessageContentCombiningSelection"/> which defines the available flags/rules that may be used to perform this concatination.
    /// </summary>
    public class LogMessageContentCombiningConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            MosaicLib.Logging.ILogMessage lm = value as MosaicLib.Logging.ILogMessage;

            if (lm != null && targetType == typeof(string))
            {
                if (lm.NamedValueSet == null && lm.Data == null)
                    return lm.Mesg;

                LogMessageContentCombiningSelection selection = LogMessageContentCombiningSelection.Default;
                if (parameter != null && parameter is LogMessageContentCombiningSelection)
                    selection = (LogMessageContentCombiningSelection)parameter;
                
                selection = selection.MapDefaultTo(LogMessageContentCombiningSelection.IncludeNonEmptyNVSAndDataInHex);

                bool addNVS = ((selection.IsSet(LogMessageContentCombiningSelection.IncludeNonEmptyNVS) && !lm.NamedValueSet.IsNullOrEmpty()) || (selection.IsSet(LogMessageContentCombiningSelection.IncludeNVS) && lm.NamedValueSet != null));
                bool addData = (selection.IsSet(LogMessageContentCombiningSelection.IncludeDataInHex) && lm.Data != null);

                if (!addNVS && !addData)
                    return lm.Mesg;

                StringBuilder sb = new StringBuilder(lm.Mesg);

                if (addNVS)
                {
                    sb.Append(" ");
                    sb.Append(lm.NamedValueSet.ToStringSML());
                }

                if (addData)
                {
                    sb.CheckedAppendFormat(" Data:[{0}]", ByteArrayTranscoders.HexStringTranscoder.Encode(lm.Data));
                }

                return sb.ToString();
            }

            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Selection control values for use with the LogContentCombiningConverter.
    /// <para/>Default (0x00), IncludeNVS (0x01), IncludeNonEmptyNVS (0x02), IncludeDataInHex (0x04), IncludeNonEmptyNVSAndDataInHex (0x06)
    /// <para/>Default will be processed as IncludeNonEmptyNVSAndDataInHex.
    /// </summary>
    [Flags]
    public enum LogMessageContentCombiningSelection : int
    {
        Default = 0x00,
        IncludeNVS = 0x01,
        IncludeNonEmptyNVS = 0x02,
        IncludeDataInHex = 0x04,
        IncludeNonEmptyNVSAndDataInHex = 0x06,
    }
}
