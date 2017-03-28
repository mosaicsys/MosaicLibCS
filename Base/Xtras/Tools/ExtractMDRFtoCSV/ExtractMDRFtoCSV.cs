using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Values.Attributes;
using MosaicLib.PartsLib.Tools.MDRF.Common;
using MosaicLib.PartsLib.Tools.MDRF.Reader;
using MosaicLib.Time;
using MosaicLib.Utils;

using MDRF = MosaicLib.PartsLib.Tools.MDRF;

namespace MosaicLib.Tools.ExtractMDRFtoCSV
{
    class ExtractMDRFtoCSV
    {
        static void Main(string[] args)
        {
            string appName = Fcns.CurrentMethod.DeclaringType.Assembly.GetName().Name;

            try
            {
                Modular.Config.IConfig config = Modular.Config.Config.Instance;

                config.AddStandardProviders(ref args);

                bool stopProcessingArgs = false;

                foreach (var arg in args)
                {
                    switch (arg)
                    {
                        case "-?":
                            Console.WriteLine("Usage: {0} {-IncludeOccurrances | -IncludeExtras | -r1 | -r5 | -r10} [fileName.mdrf] ...".CheckedFormat(appName));
                            stopProcessingArgs = true;
                            break;

                        case "-io":
                        case "-IO":
                        case "-IncludeOccurrances": 
                            includeOccurrances = true; 
                            break;

                        case "-ix":
                        case "-IX":
                        case "-IncludeExtras": 
                            includeExtras = true; 
                            break;

                        case "-r0": nominalUpdateInterval = 0.0; break;
                        case "-r1": nominalUpdateInterval = 1.0; break;
                        case "-r2": nominalUpdateInterval = 0.5; break;
                        case "-r5": nominalUpdateInterval = 0.2; break;
                        case "-r10": nominalUpdateInterval = 0.1; break;

                        default:
                            if (arg.StartsWith("-"))
                            {
                                Console.WriteLine("{0}: option '{1}' is not recognized.  Use -? for usage".CheckedFormat(appName, arg));
                                stopProcessingArgs = true;
                            }
                            else
                            {
                                ProcessFileNameArg(arg);
                            }
                            break;
                    }

                    if (stopProcessingArgs)
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("{0}: failed with exception: {1}".CheckedFormat(appName, ex.ToString(ExceptionFormat.AllButStackTrace)));
            }
        }

        private static void ProcessFileNameArg(string arg)
        {
            if (!System.IO.Path.HasExtension(arg))
                arg = arg + ".mdrf";

            if (arg.Contains("?") || arg.Contains("*"))
            {
                foreach (string fname in System.IO.Directory.EnumerateFiles(".", arg, System.IO.SearchOption.TopDirectoryOnly))
                {
                    ProcessMDRFFile(System.IO.Path.GetFullPath(fname));
                }
            }
            else
            {
                ProcessMDRFFile(System.IO.Path.GetFullPath(arg));
            }
        }

        private static void ProcessMDRFFile(string mdrfFilePath)
        {
            IVI = new ValuesInterconnection(Fcns.CurrentMethodName, registerSelfInDictionary: false, makeAPIThreadSafe: false);

            using (currentReader = new MDRFFileReader(mdrfFilePath, IVI))
            {
                string csvPath = System.IO.Path.ChangeExtension(mdrfFilePath, ".csv");

                Console.WriteLine("Processing '{0}' {1} bytes => '{2}'".CheckedFormat(System.IO.Path.GetFileName(mdrfFilePath), currentReader.FileLength, System.IO.Path.GetFileName(csvPath)));

                if (System.IO.File.Exists(csvPath))
                    System.IO.File.Delete(csvPath);
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(csvPath))
                {
                    sw.CheckedWriteLine("$File.Path,{0}{1}", mdrfFilePath, currentReader.FileIndexInfo.FileWasProperlyClosed ? "" : ",NotProperlyClosed");

                    sw.CheckedWriteLine("$Group.Names,{0}", String.Join(",", currentReader.GroupInfoArray.Select(gi => gi.Name).ToArray()));
                    sw.CheckedWriteLine("$File.Size,{0}", currentReader.FileLength);
                    sw.CheckedWriteLine("$File.Date.First,{0:o}", currentReader.DateTimeInfo.UTCDateTime.ToLocalTime());
                    sw.CheckedWriteLine("$File.Date.Last,{0:o}", currentReader.FileIndexInfo.FileIndexRowArray.Select(row => row.FirstBlockDateTime + (row.LastBlockDeltaTimeStamp - row.FirstBlockDeltaTimeStamp).FromSeconds()).Max().ToLocalTime());
                    sw.CheckedWriteLine("$File.Elapsed.Hours:{0:f6}", currentReader.FileIndexInfo.LastBlockInfo.BlockDeltaTimeStamp / 3600.0);
                    sw.CheckedWriteLine("");

                    ivaArray = currentReader.GroupPointIVAArray;
                    string [] columnNames = (new [] { "DeltaTime" }).Concat(ivaArray.Select(iva => iva.Name)).ToArray();

                    sw.CheckedWriteLine(String.Join(",", columnNames));

                    MDRF.Reader.ReadAndProcessFilterSpec filterSpec = new MDRF.Reader.ReadAndProcessFilterSpec()
                    {
                        EventHandlerDelegate = (sender, pceData) => ProcessContentEventHandlerDelegate(sw, IVI, ivaArray, sender, pceData),
                        PCEMask = ProcessContentEvent.All,
                        FileIndexUserRowFlagBitsMask = 0,
                        NominalMinimumGroupAndTimeStampUpdateInterval = nominalUpdateInterval,
                    };

                    currentReader.ReadAndProcessContents(filterSpec);

                    sw.CheckedWriteLine("");
                    sw.CheckedWriteLine("$File.End");
                }
            }
        }

        private static bool includeOccurrances = false, includeExtras = false;
        private static double nominalUpdateInterval = 0.0;
        private static IValuesInterconnection IVI = null;
        private static MDRF.Reader.MDRFFileReader currentReader = null;
        private static IValueAccessor[] ivaArray;

        private static void ProcessContentEventHandlerDelegate(StreamWriter sw, IValuesInterconnection ivi, IValueAccessor [] ivaArray, object sender, ProcessContentEventData pceData)
        {
            switch (pceData.PCE)
            {
                case ProcessContentEvent.ReadingStart:
                case ProcessContentEvent.ReadingEnd:
                    if (includeExtras)
                        sw.CheckedWriteLine("${0} ts:{1:f6}", pceData.PCE, pceData.FileDeltaTimeStamp);
                    return;
                case ProcessContentEvent.RowStart:
                case ProcessContentEvent.RowEnd:
                    if (includeExtras)
                        sw.CheckedWriteLine("${0} row:{1} firstTS:{2:f6} userFlags:0x{3:x16}", pceData.PCE, pceData.Row.RowIndex, pceData.FileDeltaTimeStamp, pceData.Row.FileIndexUserRowFlagBits);
                    return;

                case ProcessContentEvent.StartOfFullGroup:
                    if (includeExtras)
                        sw.CheckedWriteLine("${0}", pceData.PCE);
                    return;

                case ProcessContentEvent.GroupSetEnd:
                    ivi.Update(ivaArray);
                    sw.CheckedWriteLine("{0:f6},{1}", pceData.FileDeltaTimeStamp, string.Join(",", ivaArray.Select(iva => iva.ValueAsObject.ToString()).ToArray()));
                    return;

                case ProcessContentEvent.Message:
                    if (includeExtras)
                        sw.CheckedWriteLine("${0} ts:{1:f6} {2}", pceData.PCE, pceData.Row.RowIndex, pceData.FileDeltaTimeStamp, pceData.MessageInfo.Message);
                    return;

                case ProcessContentEvent.Occurrence:
                    if (includeOccurrances)
                        sw.CheckedWriteLine("${0} ts:{1:f6} {2} {3}", pceData.PCE, pceData.FileDeltaTimeStamp, pceData.OccurrenceInfo.Name, pceData.VC);
                    return;

                default:
                    return;
            }
        }
    }
}
