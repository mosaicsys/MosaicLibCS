//-------------------------------------------------------------------
/*! @file MDRF2PortRecordingRecordFormatters.cs
 *  @brief This file provides a set of helper classes that can be used to format MDRF2PortRecording records in various forms (text file, mermaid sequence diagram)
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2024 Mosaic Systems Inc.
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mosaic.ToolsLib.MDRF2.Reader;
using Mosaic.ToolsLib.Mermaid.Helpers;
using Mosaic.ToolsLib.Semi.SMLPD;
using MosaicLib.Modular.Common;
using MosaicLib.Semi.E005;
using MosaicLib.Semi.E037;
using MosaicLib.Utils;

namespace Mosaic.ToolsLib.Semi.E005.PortRecording.Formatters
{
    public class MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings : SMLPDFormatterSettings
    {
        public MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings() 
        {
            LinePrefix = "  ";
        }

        public MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings(MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings other) 
            : base(other) 
        {
            LocalEndName = other.LocalEndName;
            RemoteEndName = other.RemoteEndName;
            UseLocalDateTime = other.UseLocalDateTime;
            DateTimeFormatStr = other.DateTimeFormatStr;
            IncludeMessageSentRecords = other.IncludeMessageSentRecords;
            IncludeStateChangeRecords = other.IncludeStateChangeRecords;
        }

        public string LocalEndName { get; set; } = "E";

        public string RemoteEndName { get; set; } = "H";

        public bool UseLocalDateTime { get; set; } = true;

        public string DateTimeFormatStr { get; set; } = "o";

        public bool IncludeMessageSentRecords { get; set; } = false;

        public bool IncludeStateChangeRecords { get; set; } = false;

        public bool IncludeUnrecognizedRecords { get; set; } = false;
    }

    public class MDRF2PortRecordingRecordSMLPDTextFileFormatter
    {
        /// <summary>
        /// Default constructor.  Initializes <see cref="Settings"> to default values. 
        /// </summary>
        public MDRF2PortRecordingRecordSMLPDTextFileFormatter() 
        {
            Settings = new MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings() { Indent = true };
        }

        /// <summary>
        /// Constructor for use with explicitly provided <paramref name="settings"/>.
        /// </summary>
        public MDRF2PortRecordingRecordSMLPDTextFileFormatter(MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings settings)
        {
            Settings = settings;
        }

        public MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings Settings 
        {
            get => _Settings;
            set { _Settings = value; Formatter.Settings = value; }
        }
        private MDRF2PortRecordingRecordSMLPDTextFileFormatterSettings _Settings;

        protected SMLPDFormatter Formatter { get; } = new SMLPDFormatter();

        public string FormatAsTextFile(IEnumerable<IMDRF2QueryRecord> records)
        {
            using (var tw = new StringBuilderTextWriter())
            {
                FormatAsTextFile(records, tw);

                return tw.ToString();
            }
        }

        public void FormatAsTextFile(IEnumerable<IMDRF2QueryRecord> records, TextWriter tw)
        {
            foreach (var r in records)
            {
                var dt = r.DTPair.DateTimeUTC;
                if (Settings.UseLocalDateTime)
                    dt = dt.ToLocalTime();

                var dtStr = dt.ToString(Settings.DateTimeFormatStr);

                bool IsRemoteToLocal = r.KeyName.EndsWith(".NoteE005HeaderReceived") 
                                       || r.KeyName.EndsWith(".NoteE005MessageReceived");
                bool isNoteIssueMesg = r.KeyName.EndsWith(".NoteIssueMesg");
                bool isNoteInfoMesg = r.KeyName.EndsWith(".NoteInfoMesg");
                bool isNoteIssueObject = r.KeyName.EndsWith(".NoteIssueMesg");
                bool isNoteInfoObject = r.KeyName.EndsWith(".NoteInfoMesg");
                bool isStateChangeRecord = r.KeyName.EndsWith(".NoteStateChange");

                var dirStr = IsRemoteToLocal ? $"{Settings.RemoteEndName}->{Settings.LocalEndName}"
                                             : $"{Settings.LocalEndName}->{Settings.RemoteEndName}";

                if (r is IMDRF2QueryRecord<IMessage> rMesg)
                {
                    var mesg = rMesg.Data;
                    tw.WriteLine($"{dtStr} {dirStr} seq:{mesg.TenByteHeader.SystemBytes:x8}");

                    Formatter.Format(rMesg.Data, tw, includeLinePrefixForFirstLine: true);

                    tw.WriteLine();
                    tw.WriteLine();
                }
                else if (r is IMDRF2QueryRecord<E005MesgSentRecord> rMesgSent)
                {
                    if (Settings.IncludeMessageSentRecords)
                    {
                        tw.Write(dtStr);

                        var mesg = rMesgSent.Data.Mesg;
                        var rc = rMesgSent.Data.ResultCode;

                        if (rc.IsNullOrEmpty())
                            tw.Write($"{dtStr} {dirStr} seq:{mesg.TenByteHeader.SystemBytes:x8} {mesg.SF} Was Successfully Delivered");
                        else
                            tw.Write($"{dtStr} {dirStr} seq:{mesg.TenByteHeader.SystemBytes:x8} {mesg.SF} Delivery Failed: '{rc}'");

                        tw.WriteLine();
                    }
                }
                else if (r is IMDRF2QueryRecord<ITenByteHeader> rTBH)
                {
                    tw.WriteLine($"{dtStr} {dirStr} {rTBH.Data}");
                    tw.WriteLine();
                }
                else if (isNoteInfoMesg || isNoteIssueMesg)
                {
                    tw.Write(dtStr);
                    tw.Write(isNoteIssueMesg ? " Error " : " Info ");
                    tw.WriteLine(r.DataAsObject);
                    tw.WriteLine();
                }
                else if (isNoteInfoObject || isNoteIssueObject)
                {
                    tw.Write(dtStr);
                    tw.Write(isNoteIssueObject ? " Error " : " Info ");
                    tw.WriteLine(ValueContainer.CreateFromObject(r.DataAsObject));
                    tw.WriteLine();
                }
                else if (isStateChangeRecord)
                {
                    if (Settings.IncludeStateChangeRecords)
                    {
                        tw.Write(dtStr);
                        tw.Write(" State Change ");
                        tw.WriteLine(ValueContainer.CreateFromObject(r.DataAsObject));
                        tw.WriteLine();
                    }
                }
                else if (Settings.IncludeUnrecognizedRecords)
                {
                    tw.Write(dtStr);
                    tw.WriteLine($" {r.KeyName} {r.DataAsObject}");
                    tw.WriteLine();
                }
            }
        }
    }

    public class MDRF2PortRecordingRecordMermaidFormatterSettings
    {
        public MDRF2PortRecordingRecordMermaidFormatterSettings()  
        { }

        public MDRF2PortRecordingRecordMermaidFormatterSettings(MDRF2PortRecordingRecordMermaidFormatterSettings other) 
        {
            LocalEndName = other.LocalEndName;
            RemoteEndName = other.RemoteEndName;
            IncludeTBHHeaderRecords = other.IncludeTBHHeaderRecords;
            IncludeNoteInfoAndNoteIssueRecords = other.IncludeNoteInfoAndNoteIssueRecords;
            IncludeStateChangeSet = other.IncludeStateChangeSet?.ConvertToHashSet();
            IncludeUnrecognizedRecords = other.IncludeUnrecognizedRecords;
        }

        public string LocalEndName { get; set; } = "Equipment";

        public string RemoteEndName { get; set; } = "Host";

        public bool IncludeTBHHeaderRecords { get; set; } = true;

        public bool IncludeNoteInfoAndNoteIssueRecords{ get; set; } = true;

        public HashSet<string> IncludeStateChangeSet { get; set; } = new HashSet<string>();

        public bool IncludeUnrecognizedRecords { get; set; } = true;
    }

    public class MDRF2PortRecordingRecordMermaidFormatter
    {
        public MDRF2PortRecordingRecordMermaidFormatterSettings Settings { get; set; } = new MDRF2PortRecordingRecordMermaidFormatterSettings();

        public string FormatAsMermaidSequenceDiagram(IEnumerable<IMDRF2QueryRecord> records)
        {
            using (var tw = new StringBuilderTextWriter())
            {
                FormatAsMermaidSequenceDiagram(records, tw);
                return tw.ToString();
            }
        }

        public void FormatAsMermaidSequenceDiagram(IEnumerable<IMDRF2QueryRecord> records, TextWriter tw)
        {
            var indentStr = "    ";

            tw.WriteLine("sequenceDiagram");
            tw.WriteLine($"{indentStr}participant {Settings.RemoteEndName}");
            tw.WriteLine($"{indentStr}participant {Settings.LocalEndName}");

            foreach (var r in records)
            {
                bool IsRemoteToLocal = r.KeyName.EndsWith(".NoteE005HeaderReceived") 
                                       || r.KeyName.EndsWith(".NoteE005MessageReceived");
                bool isNoteIssueMesg = r.KeyName.EndsWith(".NoteIssueMesg");
                bool isNoteInfoMesg = r.KeyName.EndsWith(".NoteInfoMesg");
                bool isNoteIssueObject = r.KeyName.EndsWith(".NoteIssueMesg");
                bool isNoteInfoObject = r.KeyName.EndsWith(".NoteInfoMesg");
                bool isStateChangeRecord = r.KeyName.EndsWith(".NoteStateChange");

                if (r is IMDRF2QueryRecord<IMessage> rMsg)
                {
                    tw.Write(indentStr);

                    var mesg = rMsg.Data;

                    tw.Write(IsRemoteToLocal ? Settings.RemoteEndName : Settings.LocalEndName);
                    tw.Write(mesg.SF.GetArrowText());
                    tw.Write(IsRemoteToLocal ? Settings.LocalEndName : Settings.RemoteEndName);

                    tw.Write($": {mesg.SF}");
                    tw.WriteLine();
                }
                else if (r is IMDRF2QueryRecord<ITenByteHeader> rTBH)
                {
                    if (Settings.IncludeTBHHeaderRecords)
                    {
                        tw.Write(indentStr);

                        tw.Write(IsRemoteToLocal ? Settings.RemoteEndName : Settings.LocalEndName);
                        tw.Write(rTBH.Data.GetArrowText());
                        tw.Write(IsRemoteToLocal ? Settings.LocalEndName : Settings.RemoteEndName);

                        bool isHSMS = rTBH.Data is IE037TenByteHeader;
                        tw.Write($": {(isHSMS ? "HSMS" : "E004")} Header: {rTBH.Data}");
                        tw.WriteLine();
                    }
                }
                else if (isStateChangeRecord)
                {
                    var vc = ValueContainer.CreateFromObject(r.DataAsObject);
                    var nvs = vc.GetValueNVS(rethrow: false);
                    var nameHashSet = Settings.IncludeStateChangeSet;

                    if (nvs.Any(nv => nameHashSet?.Contains(nv.Name) != false))
                    {
                        tw.Write(indentStr);
                        tw.WriteLine($"Note right of {Settings.LocalEndName}:State Change {vc}");
                    }
                }
                else
                {
                    string noteStr = null;

                    if (isNoteInfoMesg || isNoteIssueMesg)
                    {
                        if (Settings.IncludeNoteInfoAndNoteIssueRecords)
                            noteStr = $"{(isNoteIssueMesg ? "Error" : "Info")} {r.DataAsObject}";
                    }
                    else if (isNoteInfoObject || isNoteIssueObject)
                    {
                        if (Settings.IncludeNoteInfoAndNoteIssueRecords)
                            noteStr = $"{(isNoteIssueMesg ? "Error" : "Info")} {ValueContainer.CreateFromObject(r.DataAsObject)}";
                    }
                    else if (Settings.IncludeUnrecognizedRecords)
                    {
                        noteStr = $"Unrecognized Record: {r.KeyName} {r.DataAsObject}";
                    }

                    if (noteStr.IsNeitherNullNorEmpty())
                    {
                        tw.Write(indentStr);
                        tw.WriteLine($"Note right of {Settings.LocalEndName}:{noteStr}");
                    }
                }
            }
        }
    }

    public static partial class ExtensionMethods
    {
        public static string GetArrowText(this StreamFunction sf)
        {
            return sf.GetArrowType().GetArrowText();
        }

        public static string GetArrowText(this ITenByteHeader tbh)
        {
            if (tbh is IE037TenByteHeader e037TBH)
                return e037TBH.GetArrowText();
            else
                return tbh.SF.GetArrowText();
        }

        public static string GetArrowText(this IE037TenByteHeader e037TBH)
        {
            return e037TBH.GetArrowType().GetArrowText();
        }

        public static MermaidSequenceDiagramArrowType GetArrowType(this StreamFunction sf)
        {
            if (sf.IsPrimary)
                return (sf.ReplyExpected) ? MermaidSequenceDiagramArrowType.SolidArrow : MermaidSequenceDiagramArrowType.SolidArrowAsync;
            else
                return MermaidSequenceDiagramArrowType.DottedArrow;
        }

        public static MermaidSequenceDiagramArrowType GetArrowType(this IE037TenByteHeader e037TBH)
        {
            switch (e037TBH.SType)
            {
                case SType.SelectReq: return MermaidSequenceDiagramArrowType.SolidArrow;
                case SType.SelectRsp: return MermaidSequenceDiagramArrowType.DottedArrow;
                case SType.LinktestReq: return MermaidSequenceDiagramArrowType.SolidArrow;
                case SType.LinktestRsp: return MermaidSequenceDiagramArrowType.DottedArrow;
                case SType.DeselectReq: return MermaidSequenceDiagramArrowType.SolidArrow;
                case SType.DeselectRsp: return MermaidSequenceDiagramArrowType.DottedArrow;
                case SType.RejectReq: return MermaidSequenceDiagramArrowType.SolidArrowAsync;
                case SType.SeparateReq: return MermaidSequenceDiagramArrowType.SolidArrowAsync;
                case SType.DataMessage: return MermaidSequenceDiagramArrowType.Invalid;
                case SType.Invalid: return MermaidSequenceDiagramArrowType.Invalid;
                default: return MermaidSequenceDiagramArrowType.Invalid;
            }
        }
    }
}
