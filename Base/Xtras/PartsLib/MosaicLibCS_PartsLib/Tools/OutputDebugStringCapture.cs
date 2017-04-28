//-------------------------------------------------------------------
/*! @file OutputDebugStringCapture.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2017 Mosaic Systems Inc.
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
using System.Collections.Generic;
using System.Linq;

using MosaicLib;
using MosaicLib.Modular;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;

namespace MosaicLib.PartsLib.Tools
{
    public class OutputDebugStringCapturePart : SimpleActivePartBase
    {
        public static readonly string name_DBWinMutex = "DBWinMutex";       // not used by the capture part
        public static readonly string name_DBWIN_BUFFER = "DBWIN_BUFFER";
        public static readonly string name_DBWIN_BUFFER_READY = "DBWIN_BUFFER_READY";
        public static readonly string name_DBWIN_DATA_READY = "DBWIN_DATA_READY";
        public const int bufferSize = 4096;

        public OutputDebugStringCapturePart(string partID, Logging.MesgType ? generateMesgType = null)
            : base(partID, SimpleActivePartBaseSettings.DefaultVersion1)
        {
            this.generateMesgType = generateMesgType ?? Logging.MesgType.Debug;

            try
            {
                DBWIN_BUFFER = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateNew(name_DBWIN_BUFFER, zeroBuffer.Length);
                DBWIN_BUFFER_Accessor = DBWIN_BUFFER.CreateViewAccessor(0, zeroBuffer.Length);

                DBWIN_BUFFER_READY = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, name_DBWIN_BUFFER_READY);
                DBWIN_DATA_READY = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.ManualReset, name_DBWIN_DATA_READY);

                isUsable = true;
            }
            catch (System.Exception ex)
            {
                Release();
                Log.Error.Emit("Construction failed with exception: {0}", ex.ToString(ExceptionFormat.TypeAndMessage));
            }
        }

        Logging.MesgType generateMesgType;

        System.IO.MemoryMappedFiles.MemoryMappedFile DBWIN_BUFFER;
        System.IO.MemoryMappedFiles.MemoryMappedViewAccessor DBWIN_BUFFER_Accessor;
        System.Threading.EventWaitHandle DBWIN_BUFFER_READY, DBWIN_DATA_READY;
        bool isUsable = false;

        protected override void MainThreadFcn()
        {
            if (isUsable)
                DBWIN_BUFFER_READY.Set();

            base.MainThreadFcn();

            Release();
        }

        protected override bool WaitForSomethingToDo(IWaitable waitable, TimeSpan useWaitTimeLimit)
        {
            bool wasSignaled = isUsable ? DBWIN_DATA_READY.WaitOne(useWaitTimeLimit) : waitable.Wait(useWaitTimeLimit);

            return wasSignaled;
        }

        byte[] mesgByteBuffer = new byte[bufferSize];
        byte[] zeroBuffer = new byte[bufferSize + 4];

        Dictionary<int, Tuple<Logging.ILogger, Logging.IMesgEmitter>> tupleDictionary = new Dictionary<int, Tuple<Logging.ILogger, Logging.IMesgEmitter>>();

        protected override void PerformMainLoopService()
        {
            int pid = 0;
            string mesg = null;

            bool dataReady = isUsable && DBWIN_DATA_READY.WaitOne(TimeSpan.Zero);

            if (dataReady)
            {
                pid = DBWIN_BUFFER_Accessor.ReadInt32(0);
                int readMesgBytes = DBWIN_BUFFER_Accessor.ReadArray(4, mesgByteBuffer, 0, bufferSize);

                int len = bufferSize;

                for (int idx = 0; idx < len; idx++)
                {
                    if (mesgByteBuffer[idx] == 0)
                        len = idx;
                }

                mesg = ByteArrayTranscoders.ByteStringTranscoder.Encode(mesgByteBuffer, 0, len);
                mesgByteBuffer.Clear();

                DBWIN_BUFFER_Accessor.WriteArray(0, zeroBuffer, 0, zeroBuffer.Length);

                DBWIN_DATA_READY.Reset();
                DBWIN_BUFFER_READY.Set();
            }

            if (BaseState.IsOnline && pid != 0 && !mesg.IsNullOrEmpty())
            {
                Func<System.Diagnostics.Process> func = (() => System.Diagnostics.Process.GetProcessById(pid));
                System.Diagnostics.Process sendingProcess = func.TryGet();

                Tuple<Logging.ILogger, Logging.IMesgEmitter> pidTuple = null;

                if (tupleDictionary.TryGetValue(pid, out pidTuple) && pidTuple != null)
                {
                    if (sendingProcess != null && !pidTuple.Item1.Name.EndsWith(sendingProcess.ProcessName))
                        pidTuple = null;
                }

                if (pidTuple == null)
                {
                    string loggerName;
                    if (sendingProcess != null)
                        loggerName = "{0}.pid{1}.{2}".CheckedFormat(PartID, pid, sendingProcess.ProcessName);
                    else
                        loggerName = "{0}.pid{1}".CheckedFormat(PartID, pid);

                    Logging.ILogger logger = new Logging.Logger(loggerName);
                    Logging.IMesgEmitter emitter = logger.Emitter(generateMesgType);

                    pidTuple = Tuple.Create(logger, emitter);
                    tupleDictionary[pid] = pidTuple;
                }

                if (pidTuple != null)
                {
                    pidTuple.Item2.Emit("{0}", mesg.TrimEnd(' ', '\t', '\r', '\n'));
                }
            }
        }

        private void Release()
        {
            isUsable = false;
            Fcns.DisposeOfObject(ref DBWIN_BUFFER_READY);
            Fcns.DisposeOfObject(ref DBWIN_DATA_READY);
            Fcns.DisposeOfObject(ref DBWIN_BUFFER_Accessor);
            Fcns.DisposeOfObject(ref DBWIN_BUFFER);
        }
    }
}
