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
using Mosaic.ToolsLib.Compression;
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
        private readonly static System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();

        static void Main(string[] args)
        {
            var logger = new Logging.Logger(Fcns.CurrentClassLeafName);

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

                    ProcessFileNameArg(arg, logger);
                }
            }
            catch (System.Exception ex)
            {
                logger.Error.Emit("{0}: failed with exception: {1}".CheckedFormat(appName, ex.ToString(ExceptionFormat.AllButStackTrace)));
            }
        }

        public static readonly Logging.IBasicLogger nullBasicLogger = new Logging.NullBasicLogger();

        private static void WriteUsage()
        {
            Console.WriteLine("Usage: {0} [fileName.mdrf2|fileName.mdrf2.lz4] ...");
            Console.WriteLine();

            Console.WriteLine("Assembly: {0}", currentExecAssy.GetSummaryNameAndVersion());
        }

        /// <remarks>This method is public so that it may be used from other assemblies and in other contexts such as under dotnet interactive</remarks>
        public static void ProcessFileNameArg(string arg, Logging.IBasicLogger logger = null)
        {
            bool isWildCard = arg.Contains("?") || arg.Contains("*");

            if (!System.IO.Path.HasExtension(arg) && !isWildCard)
                arg += ".mdrf2.lz4";

            var filePart = System.IO.Path.GetFileName(arg);
            var pathPart = System.IO.Path.GetDirectoryName(arg).MapNullOrEmptyTo(".");

            if (filePart.Contains("?") || filePart.Contains("*"))
            {
                foreach (var fpath in System.IO.Directory.EnumerateFiles(pathPart, filePart, System.IO.SearchOption.AllDirectories))
                {
                    if (System.IO.Directory.Exists(fpath))
                        continue;

                    using (var rateTrace = new Logging.RateTrace(logger.Debug, $"file:{fpath}"))
                    {
                        rateTrace.Count = ProcessMDRFFile(fpath, logger);
                    }
                }
            }
            else
            {
                using (var rateTrace = new Logging.RateTrace(logger.Debug, $"file:{arg}"))
                {
                    rateTrace.Count = ProcessMDRFFile(arg, logger);
                }
            }
        }

        /// <remarks>This method is public so that it may be used from other assemblies and in other contexts such as under dotnet interactive</remarks>
        public static long ProcessMDRFFile(string mdrfFilePath, Logging.IBasicLogger logger = null)
        {
            logger = logger ?? nullBasicLogger;

            var settings = new MessagePackFileRecordReaderSettings()
            {
                BufferArrayPool = ArrayPool<byte>.Shared,
                FileOptions = FileOptions.SequentialScan,
                InitialBufferSize = 65536,
            };

            bool vcJsonConvert = true;

            string stemName = mdrfFilePath.RemoveCompressorSelectExtensionFromPath();

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
                    logger.Error.Emit(mesg = $"Processing for file '{mdrfFilePath}' failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
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
