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

            if (lm != null && ((targetType == typeof(string)) || (targetType == typeof(object))))
            {
                if (lm.NamedValueSet == null && lm.Data == null)
                    return lm.MesgEscaped;

                LogMessageContentCombiningSelection selection = LogMessageContentCombiningSelection.Default;
                if (parameter != null)
                    selection = ValueContainer.CreateFromObject(parameter).GetValue<LogMessageContentCombiningSelection>(rethrow: true);
                
                selection = selection.MapDefaultTo(LogMessageContentCombiningSelection.IncludeNonEmptyNVSAndNonNullDataInHex);

                bool addNVS = ((selection.IsSet(LogMessageContentCombiningSelection.IncludeNonEmptyNVS) && !lm.NamedValueSet.IsNullOrEmpty()) || (selection.IsSet(LogMessageContentCombiningSelection.IncludeNVS) && lm.NamedValueSet != null));
                bool addData = (selection.IsSet(LogMessageContentCombiningSelection.IncludeNonNullDataInHex) && lm.Data != null);
                bool addThreadIDs = selection.IsSet(LogMessageContentCombiningSelection.IncludeThreadIDs);
                bool addThreadName = (selection.IsSet(LogMessageContentCombiningSelection.IncludeNonEmptyThreadName) && !lm.ThreadName.IsNullOrEmpty());
                bool excludeMesg = selection.IsSet(LogMessageContentCombiningSelection.ExcludeMesg);

                if (!addNVS && !addData && !excludeMesg)
                    return lm.MesgEscaped;

                StringBuilder sb = new StringBuilder(!excludeMesg ? lm.MesgEscaped : string.Empty);

                if (addNVS)
                {
                    sb.AppendWithDelimiter(" ", lm.NamedValueSet.SafeToStringSML());
                }

                if (addData)
                {
                    if (!excludeMesg)
                        sb.CheckedAppendFormatWithDelimiter(" ", "Data:[{0}]", ByteArrayTranscoders.HexStringTranscoder.Encode(lm.Data));
                    else
                        sb.AppendWithDelimiter(" ", ByteArrayTranscoders.HexStringTranscoder.Encode(lm.Data));
                }

                if (addThreadIDs)
                {
                    if (!excludeMesg)
                        sb.AppendWithDelimiter(" ", "Thread:");

                    sb.CheckedAppendFormat("{0:d4}/${1:x4}", lm.ThreadID, lm.Win32ThreadID);

                    if (addThreadName)
                        sb.CheckedAppendFormatWithDelimiter(" ", "'{0}'", lm.ThreadName);
                }
                else if (addThreadName)
                {
                    if (!excludeMesg)
                        sb.CheckedAppendFormatWithDelimiter(" ", "ThreadName:'{0}'", lm.ThreadName);
                    else
                        sb.CheckedAppendFormatWithDelimiter(" ", "'{0}'", lm.ThreadName);
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
    /// <para/>Default (0x00), ExcludeMesg (0x01), IncludeNVS (0x02), IncludeNonEmptyNVS (0x04), IncludeNonNullDataInHex (0x08), IncludeThreadIDs (0x10), IncludeNonEmptyThreadName (0x20)
    /// IncludeNonEmptyNVSAndDataInHex (0x06)
    /// <para/>Default will be processed as IncludeNonEmptyNVSAndDataInHex.
    /// </summary>
    [Flags]
    public enum LogMessageContentCombiningSelection : int
    {
        Default = 0x00,
        ExcludeMesg = 0x01,
        IncludeNVS = 0x02,
        IncludeNonEmptyNVS = 0x04,
        IncludeNonNullDataInHex = 0x08,
        IncludeThreadIDs = 0x10,
        IncludeNonEmptyThreadName = 0x20,
        IncludeNonEmptyNVSAndNonNullDataInHex = (IncludeNonEmptyNVS | IncludeNonNullDataInHex),
        IncludeOnlyNonEmptyNVSAndNonNullDataInHex = (ExcludeMesg | IncludeNonEmptyNVS | IncludeNonNullDataInHex),
        IncludeOnlyNonEmptyNVS = (ExcludeMesg | IncludeNonEmptyNVS),
        IncludeOnlyNonNullDataInHex = (ExcludeMesg | IncludeNonNullDataInHex),
        IncludeOnlyTheadIDsAndNonEmptyThreadName = (ExcludeMesg | IncludeNonEmptyThreadName | IncludeThreadIDs),
    }
}
