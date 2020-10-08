//-------------------------------------------------------------------
/*! @file ConvertMDRF2toJSON.cs
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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MessagePack;
using Mosaic.ToolsLib.MessagePackUtils;
using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Reflection.Attributes;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.Tools.ConvertMDRF2toJSON
{
    public static class ConvertMDRF2toJSON
    {
        private static string appName = "AppNameNotFound";
        private static System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();

        static void Main(string[] args)
        {
            try
            {
                Config.AddStandardProviders(ref args);

                Logging.AddLogMessageHandlerToDefaultDistributionGroup(Logging.CreateConsoleLogMessageHandler(logGate: Logging.LogGate.Debug));
                if (System.Diagnostics.Debugger.IsAttached)
                    Logging.AddLogMessageHandlerToDefaultDistributionGroup(Logging.CreateDiagnosticTraceLogMessageHandler(logGate: Logging.LogGate.All));

                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

                appName = System.IO.Path.GetFileName(currentProcess.MainModule.ModuleName);

                bool stopProcessingArgs = false;

                if (args.SafeLength() <= 0)
                {
                    WriteUsage();
                    stopProcessingArgs = true;
                }

                foreach (var arg in args)
                {
                    if (stopProcessingArgs)
                        break;

                    ProcessFileNameArg(arg);
                }
            }
            catch (System.Exception ex)
            {
                appLogger.Error.Emit("{0}: failed with exception: {1}".CheckedFormat(appName, ex.ToString(ExceptionFormat.AllButStackTrace)));
            }
        }

        static Logging.ILogger appLogger = new Logging.Logger(Fcns.CurrentClassLeafName);

        private static void WriteUsage()
        {
            Console.WriteLine("Usage: {0} [fileName.mdrf2|fileName.mdrf2.lz4] ...");
            Console.WriteLine();

            Console.WriteLine("Assembly: {0}", currentExecAssy.GetSummaryNameAndVersion());
        }

        private static void ProcessFileNameArg(string arg)
        {
            bool isWildCard = arg.Contains("?") || arg.Contains("*");

            if (!System.IO.Path.HasExtension(arg) && !isWildCard)
                arg = arg + ".mdrf2.lz4";

            string filePart = System.IO.Path.GetFileName(arg);
            string pathPart = System.IO.Path.GetDirectoryName(arg).MapNullOrEmptyTo(".");
            string fullPathPart = System.IO.Path.GetFullPath(pathPart);
            var taskFactory = new TaskFactory();

            if (filePart.Contains("?") || filePart.Contains("*"))
            {
                foreach (string fpath in System.IO.Directory.EnumerateFiles(pathPart, filePart, System.IO.SearchOption.AllDirectories))
                {
                    if (System.IO.Directory.Exists(fpath))
                        continue;

                    using (var rateTrace = new Logging.RateTrace(appLogger.Debug, $"file:{fpath}"))
                    {
                        rateTrace.Count = ProcessMDRFFile(fpath);
                    }
                }
            }
            else
            {
                using (var rateTrace = new Logging.RateTrace(appLogger.Debug, $"file:{arg}"))
                {
                    rateTrace.Count = ProcessMDRFFile(arg);
                }
            }
        }

        private static long ProcessMDRFFile(string mdrfFilePath)
        {
            var settings = new MessagePackFileRecordReaderSettings()
            {
                BufferArrayPool = ArrayPool<byte>.Shared,
                FileOptions = FileOptions.SequentialScan,
                LZ4ExtraMemory = 64 * 1024,
                InitialBufferSize = 64 * 1024,
            };

            bool vcJsonConvert = true;

            var stemName = mdrfFilePath.RemoveSuffixIfNeeded(".lz4");

            using (var mpFileRecordReader = new MessagePackFileRecordReader().Open(mdrfFilePath, settings))
            using (var ofs = new FileStream(string.Concat(stemName, ".json"), FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(ofs))
            {
                try
                {
                    MessagePackReader mpReader = default;

                    for (; ; )
                    {
                        if (mpFileRecordReader.SetupMPReaderForNextRecord(ref mpReader))
                        {
                            var nextRecordLength = mpFileRecordReader.NextRecordLength;

                            if (vcJsonConvert)
                            {
                                var vc = mpReader.DeserializeVC();
                                sw.WriteLine(vc.ConvertToRawJSON());
                            }
                            else
                            {
                                MessagePackSerializer.ConvertToJson(ref mpReader, sw);
                            }

                            mpFileRecordReader.AdvancePastCurrentRecord();
                        }
                        else if (mpFileRecordReader.EndReached)
                        {
                            break;
                        }
                        else
                        {
                            throw new System.InvalidOperationException("SetupMPReaderForNextRecord returned false before reaching end of file");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    string mesg;
                    Console.WriteLine(mesg = $"Processing for file '{mdrfFilePath}' failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }

                var counters = mpFileRecordReader.Counters;
                var ofsLength = ofs.Length;

                sw.Flush();
                sw.Close();

                return Math.Max(Math.Max((long) counters.FileLength, ofsLength), (long) counters.TotalBytesProcessed);
            }
        }
    }
}
