//-------------------------------------------------------------------
/*! @file SMLPD.cs
 *  @brief This file provides common definitions that relate to the use of object that support SML Public Domain output formatting
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2022 Mosaic Systems Inc.
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

using MosaicLib.Modular.Common;
using MosaicLib.Semi.E005.Data;
using MosaicLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mosaic.ToolsLib.Semi.SMLPD
{
    public class SMLPDFormatterSettings
    {
        /// <summary>
        ///  Default constructor
        /// </summary>
        public SMLPDFormatterSettings() { }

        /// <summary>
        ///  Copy constructor
        /// </summary>
        public SMLPDFormatterSettings(SMLPDFormatterSettings settings) 
        {
            Indent = settings.Indent;
            LevelIndentStr = settings.LevelIndentStr;
            MessageCloseString = settings.MessageCloseString;
            Rethrow = settings.Rethrow;
            NominalElementLineSplitLength = settings.NominalElementLineSplitLength;
            MessageBodyIndentLevel = settings.MessageBodyIndentLevel;
            LinePrefix = settings.LinePrefix;
        }

        /// <summary>
        /// Set to true to produce indented output.
        /// Defaults to false.
        /// </summary>
        public bool Indent { get; set; }

        /// <summary>
        /// Selects the string that is repeated at the front of each line to reach a given indent level.
        /// Defaults to two space characters ["  "]
        /// </summary>
        public string LevelIndentStr { get; set; } = "  ";

        /// <summary>
        /// Defines the final string that is used to close out the top level item.  Defaults to the period character ["."].
        /// </summary>
        public string MessageCloseString { get; set; } = ".";

        /// <summary>
        /// Defines the value that is passed to the rethrow parameters in the various <see cref="ValueContainer"/> related methods that are used here.
        /// Defaults to true.
        /// When this is set to false then the calls to the <see cref="SMLPDFormatter.Format(ValueContainer, System.IO.TextWriter, int, bool)"/> method 
        /// which internally throw and exception will output a summary of the exception to the given text writer.
        /// </summary>
        public bool Rethrow { get; set; } = true;

        /// <summary>
        /// Defines the nominal line content length for a single element at which point its contents may be split accross multiple lines.
        /// At present this logic is only implemented for string output.
        /// For string output this value is nominal since it gives the minimum length at which splitting will be triggered but, as it is only 
        /// applied on character boundaries, the resulting lines may each be longer than the given value since the width of hex formatted characters are larger than 1 in the formatted output.
        /// </summary>
        public int NominalElementLineSplitLength { get; set; } = 80;

        /// <summary>
        /// Gives the indent level that is used for message bodys.
        /// </summary>
        public int MessageBodyIndentLevel { get; set; } = 1;

        /// <summary>
        /// prefix for text lines - often used to indent an entire block.  Defaults to the empty string [""].
        /// </summary>
        public string LinePrefix { get; set; } = "";
    }

    /// <summary>
    /// This class provides a helper object type that supports formatting standard E005 Data <see cref="ValueContainer"/>
    /// contents using a nominal version of the public domain SECS Message Log or SECS Message Language (SML) approach.
    /// Instances of this class can be used to generate both indented and unindented output.
    /// </summary>
    public class SMLPDFormatter
    {
        public SMLPDFormatterSettings Settings { get; set; } = new SMLPDFormatterSettings() { Indent = true };

        /// <summary>
        /// Formats the given <paramref name="message"/> into the given <paramref name="tw"/> using the current object's settings.
        /// </summary>
        public void Format(MosaicLib.Semi.E005.IMessage message, System.IO.TextWriter tw, bool includeLinePrefixForFirstLine = false)
        {
            try
            {
                if (message == null)
                    return;

                if (includeLinePrefixForFirstLine)
                    tw.Write(Settings.LinePrefix);

                var sf = message.SF;
                tw.Write($"S{sf.StreamByte}F{sf.FunctionByte}");
                if (sf.ReplyExpected)
                    tw.Write(" [W]");

                var contentBytes = message.ContentBytes;

                if (!contentBytes.IsNullOrEmpty())
                {
                    ValueContainer vc = ValueContainer.Empty.ConvertFromE005Data(contentBytes, throwOnException: true);

                    InnerStartNewLineOrAddSpace(tw, Settings.MessageBodyIndentLevel);

                    InnerFormat(vc, tw, Settings.MessageBodyIndentLevel, true);
                }

                InnerStartNewLineOrAddSpace(tw, 0);

                tw.Write(Settings.MessageCloseString);
            }
            catch (System.Exception ex)
            {
                if (Settings.Rethrow)
                    throw;

                tw.WriteLine($"{Fcns.CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
            }
        }

        /// <summary>
        /// Formats the given <paramref name="vc"/> into the given <paramref name="tw"/> using the current object's settings.
        /// </summary>
        public void Format(ValueContainer vc, System.IO.TextWriter tw, int startingIndentLevel = 0, bool includeLinePrefixForFirstLine = false)
        {
            try
            {
                if (!vc.IsEmpty)
                {
                    if (includeLinePrefixForFirstLine)
                        tw.Write(Settings.LinePrefix);

                    InnerFormat(vc, tw, startingIndentLevel, true);
                }
            }
            catch (System.Exception ex)
            {
                if (Settings.Rethrow)
                    throw;

                tw.WriteLine($"{Fcns.CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
            }
        }

        private void InnerFormat(ValueContainer vc, System.IO.TextWriter tw, int currentIndentLevel, bool isOutermostCall)
        {
            if (!isOutermostCall)
            {
                if (Settings.Indent && currentIndentLevel > 0)
                    InnerStartNewLine(tw, currentIndentLevel);
                else
                    tw.Write(" ");
            }

            switch (vc.cvt)
            {
                case ContainerStorageType.Bo: tw.Write($"<Bo {vc.u.b.MapToInt()}>"); break;
                case ContainerStorageType.Bi: 
                    if (vc.o is BiArray)
                        InnerFormat(vc.o, tw, currentIndentLevel);
                    else
                       tw.Write($"<Bi 0x{vc.u.bi:x2}>"); 
                    break;
                case ContainerStorageType.I1: tw.Write($"<I1 {vc.u.i8}>"); break;
                case ContainerStorageType.I2: tw.Write($"<I2 {vc.u.i16}>"); break;
                case ContainerStorageType.I4: tw.Write($"<I4 {vc.u.i32}>"); break;
                case ContainerStorageType.I8: tw.Write($"<I8 {vc.u.i64}>"); break;
                case ContainerStorageType.U1: tw.Write($"<U1 {vc.u.u8}>"); break;
                case ContainerStorageType.U2: tw.Write($"<U2 {vc.u.u16}>"); break;
                case ContainerStorageType.U4: tw.Write($"<U4 {vc.u.u32}>"); break;
                case ContainerStorageType.U8: tw.Write($"<U8 {vc.u.u64}>"); break;
                case ContainerStorageType.F4: tw.Write($"<F4 {vc.u.f32}>"); break;
                case ContainerStorageType.F8: tw.Write($"<F8 {vc.u.f64}>"); break;

                case ContainerStorageType.A: InnerFormat(vc.o as string, tw, currentIndentLevel); break;

                case ContainerStorageType.L: InnerFormat(vc.GetValueL(rethrow: Settings.Rethrow), tw, currentIndentLevel); break;
                case ContainerStorageType.LS: InnerFormat(vc.GetValueL(rethrow: Settings.Rethrow), tw, currentIndentLevel); break;

                case ContainerStorageType.NVS: InnerFormat(vc.GetValueNVS(rethrow: Settings.Rethrow), tw, currentIndentLevel); break;
                case ContainerStorageType.NV: InnerFormat(vc.GetValueNV(rethrow: Settings.Rethrow), tw, currentIndentLevel); break;

                case ContainerStorageType.Object: InnerFormat(vc.o, tw, currentIndentLevel); break;

                default: InnerFormat(vc.ToStringSML(), tw, currentIndentLevel); break;
            }
        }

        private void InnerFormat(string s, System.IO.TextWriter tw, int currentIndentLevel)
        {
            s = s.MapNullToEmpty();
            var sLength = s.Length;
            var isBasicAscii = s.IsBasicAscii();

            if (sLength == 0)
            {
                tw.Write("<A>");
            }
            else if (isBasicAscii && !Settings.Indent)
            {
                tw.Write($"<A [{s.Length}] \"{s}\">");
            }
            else
            {
                var hasWideChars = !isBasicAscii && !s.IsByteSerializable();

                var leadInStr = $"<{(hasWideChars ? "W" : "A")} [{sLength}]";
                tw.Write(leadInStr);

                int linePos = leadInStr.Length;
                bool inQuote = false;
                bool wasSplit = false;
                var sLengthMinusOne = sLength - 1; ;

                for (int index = 0; index <= sLengthMinusOne; index++)
                {
                    var ch = s[index];
                    var isLastChar = (index == sLengthMinusOne);

                    if (ch.IsBasicAscii() && ch != '"')
                    {
                        if (!inQuote)
                        {
                            tw.Write(" \"");
                            inQuote = true;
                            linePos += 2;
                        }
                        tw.Write(ch);
                        linePos += 1;
                    }
                    else
                    {
                        if (inQuote)
                        {
                            tw.Write('"');
                            inQuote = false;
                            linePos += 1;
                        }

                        var hexStr = $" 0x{(int)ch:x2}";
                        tw.Write(hexStr);
                        linePos += hexStr.Length;
                    }

                    if (!isLastChar && Settings.Indent && linePos >= Settings.NominalElementLineSplitLength)
                    {
                        if (inQuote)
                        {
                            tw.Write('"');
                            inQuote = false;
                        }

                        InnerStartNewLine(tw, currentIndentLevel);

                        // add two extra spaces so that the extra line content runs end up under the '[' in the leadin string.
                        tw.Write("  ");

                        linePos = 2;

                        wasSplit = true;
                    }
                }

                if (inQuote)
                {
                    tw.Write('"');
                }

                if (wasSplit)
                    InnerStartNewLine(tw, currentIndentLevel);

                tw.Write(">");
            }
        }

        private void InnerFormat(object o, System.IO.TextWriter tw, int currentIndentLevel)
        {
            Type oType = o?.GetType();
            if (oType == typeof(sbyte[])) InnerFormatArray((sbyte[])o, "I1", tw);
            else if (oType == typeof(short[])) InnerFormatArray((short[])o, "I2", tw);
            else if (oType == typeof(int[])) InnerFormatArray((int[])o, "I4", tw);
            else if (oType == typeof(long[])) InnerFormatArray((long[])o, "I8", tw);
            else if (oType == typeof(byte[])) InnerFormatArray((byte[])o, "U1", tw);
            else if (oType == typeof(ushort[])) InnerFormatArray((ushort[])o, "U2", tw);
            else if (oType == typeof(uint[])) InnerFormatArray((uint[])o, "U4", tw);
            else if (oType == typeof(ulong[])) InnerFormatArray((ulong[])o, "U8", tw);
            else if (oType == typeof(float[])) InnerFormatArray((float[])o, "F4", tw);
            else if (oType == typeof(double[])) InnerFormatArray((double[])o, "F8", tw);
            else if (oType == typeof(bool[]) && o is bool[] boArray) InnerFormatArray(boArray.Select(bo => bo.MapToInt()).ToArray(), "Bo", tw);
            else if (oType == typeof(BiArray) && o is BiArray biArray) InnerFormatArray((byte[])biArray, "Bi", tw, " 0x{0:x2}");
            else InnerFormat($"[Object:{o}]", tw, currentIndentLevel);
        }

        private void InnerFormatArray<TItemType>(TItemType[] array, string tokenType, System.IO.TextWriter tw, string fmtStr = null)
        {
            if (array.Length == 0)
            {
                tw.Write($"<{tokenType}>");
            }
            else
            {
                tw.Write($"<{tokenType} [{array.Length}]");

                if (fmtStr.IsNullOrEmpty())
                {
                    foreach (var item in array)
                        tw.Write($" {item}");
                }
                else
                {
                    foreach (var item in array)
                        tw.Write(fmtStr.CheckedFormat(item));
                }

                tw.Write(">");
            }
        }

        private void InnerFormat(IList<ValueContainer> vcList, System.IO.TextWriter tw, int currentIndentLevel)
        {
            if (vcList.IsNullOrEmpty())
            {
                tw.Write($"<L>");
            }
            else
            {
                tw.Write($"<L [{vcList.Count}]");

                var nextIndentLevel = currentIndentLevel + 1;

                foreach (var vc in vcList)
                {
                    InnerFormat(vc, tw, nextIndentLevel, false);
                }

                if (Settings.Indent)
                    InnerStartNewLine(tw, currentIndentLevel);

                tw.Write(">");
            }
        }

        private void InnerFormat(INamedValueSet nvs, System.IO.TextWriter tw, int currentIndentLevel)
        {
            var vcList = nvs.Select(nv => ValueContainer.CreateL(ToSet(nv))).ToList();

            InnerFormat(vcList, tw, currentIndentLevel);
        }

        private void InnerFormat(INamedValue nv, System.IO.TextWriter tw, int currentIndentLevel)
        {
            InnerFormat(ToSet(nv).ToList(), tw, currentIndentLevel); ;
        }

        private IEnumerable<ValueContainer> ToSet(INamedValue nv)
        {
            yield return (nv?.Name ?? "").CreateVC();
            if (nv?.VC.IsEmpty == false)
                yield return nv.VC;
        }

        private void InnerStartNewLineOrAddSpace(System.IO.TextWriter tw, int currentIndentLevel, string spaceStr = " ")
        {
            if (Settings.Indent)
                InnerStartNewLine(tw, currentIndentLevel);
            else
                tw.Write(spaceStr);
        }

        private void InnerStartNewLine(System.IO.TextWriter tw, int currentIndentLevel)
        {
            tw.WriteLine();
            tw.Write(Settings.LinePrefix);
            for (int index = 0; index < currentIndentLevel; index++)
                tw.Write(Settings.LevelIndentStr);
        }
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// EM uses a <see cref="SMLPDFormatter"/> to format the given <paramref name="vc"/> 
        /// into a <see cref="StringBuilderTextWriter"/> and then returns the string that was produced by the formatting operation.
        /// </summary>
        /// <param name="vc">Gives the value that is to be converted to a SMLPD string.</param>
        /// <param name="indent">When this is set to true it selects use of indented output</param>
        /// <param name="rethrow">Selects whether rethrow is passed as true or false to the internal ValueContainer Get methods</param>
        /// <param name="newLineStr">When non-null this is used to override the default and specify the the value used for the <see cref="System.IO.TextWriter.NewLine"/> property that is used during the format operation.</param>
        /// <param name="levelIndentStr">When non-null this selects the level indent string to use.  When null the existing <see cref="SMLPDFormatterSettings.LevelIndentStr"/> default value is used.</param>
        /// <param name="nominalElementLineSplitLength">When non-null this selects the length for a single line element at which the element may be split onto further lines.  When null th exisitng <see cref="SMLPDFormatterSettings.NominalElementLineSplitLength"/> default value is used.</param>
        public static string ToStringSMLPD(this ValueContainer vc, bool indent = false, bool rethrow = false, string newLineStr = null, string levelIndentStr = null, int? nominalElementLineSplitLength = null)
        {
            var sbtw = new StringBuilderTextWriter();

            if (newLineStr != null)
                sbtw.NewLine = newLineStr;

            var settings = new SMLPDFormatterSettings()
            {
                Indent = indent,
                Rethrow = rethrow,
            };

            if (levelIndentStr != null)
                settings.LevelIndentStr = levelIndentStr;

            if (nominalElementLineSplitLength != null)
                settings.NominalElementLineSplitLength = nominalElementLineSplitLength ?? default;

            var smlPDFormatter = new SMLPDFormatter() { Settings = settings };

            smlPDFormatter.Format(vc, sbtw);

            return sbtw.ToString();
        }

        /// <summary>
        /// EM uses a <see cref="SMLPDFormatter"/> to format the given <paramref name="message"/> 
        /// into a <see cref="StringBuilderTextWriter"/> and then returns the string that was produced by the formatting operation.
        /// </summary>
        /// <param name="message">Gives the message that is to be converted to a SMLPD string.</param>
        /// <param name="indent">When this is set to true it selects use of indented output</param>
        /// <param name="rethrow">Selects whether rethrow is passed as true or false to the internal ValueContainer Get methods</param>
        /// <param name="newLineStr">When non-null this is used to override the default and specify the the value used for the <see cref="System.IO.TextWriter.NewLine"/> property that is used during the format operation.</param>
        /// <param name="levelIndentStr">When non-null this selects the level indent string to use.  When null the existing <see cref="SMLPDFormatterSettings.LevelIndentStr"/> default value is used.</param>
        /// <param name="nominalElementLineSplitLength">When non-null this selects the length for a single line element at which the element may be split onto further lines.  When null th exisitng <see cref="SMLPDFormatterSettings.NominalElementLineSplitLength"/> default value is used.</param>
        public static string ToStringSMLPD(this MosaicLib.Semi.E005.IMessage message, bool indent = false, bool rethrow = false, string newLineStr = null, string levelIndentStr = null, int? nominalElementLineSplitLength = null)
        {
            var sbtw = new StringBuilderTextWriter();

            if (newLineStr != null)
                sbtw.NewLine = newLineStr;

            var settings = new SMLPDFormatterSettings()
            {
                Indent = indent,
                Rethrow = rethrow,
            };

            if (levelIndentStr != null)
                settings.LevelIndentStr = levelIndentStr;

            if (nominalElementLineSplitLength != null)
                settings.NominalElementLineSplitLength = nominalElementLineSplitLength ?? default;

            var smlPDFormatter = new SMLPDFormatter() { Settings = settings };

            smlPDFormatter.Format(message, sbtw);

            return sbtw.ToString();
        }
    }
}
