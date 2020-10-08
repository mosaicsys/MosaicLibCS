//-------------------------------------------------------------------
/*! @file BufferWriter.cs
 *  @brief Defines a class used as a local implementation class for the IBufferWriter interface.
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
using System.Runtime.CompilerServices;

using MosaicLib.Utils;

namespace Mosaic.ToolsLib.BufferWriter
{
    /// <summary>
    /// This class provides an IDisposable inplementation of the IBufferWriter{byte} interface.
    /// It supports use with an optionally specified ArrayPool{byte} instance and dynamically expands the allocated buffer capacity as needed to accept the set of bytes that are written to it.
    /// It also supports a ResetCount method that can be used to rewind the write starting position and CurrentCount so that the buffer may be re-used without needing to be disposed.
    /// </summary>
    public class ByteArrayBufferWriter : IBufferWriter<byte>, IDisposable
    {
        public ByteArrayBufferWriter(int initialSize, ArrayPool<byte> arrayPool = null)
        {
            InitialSize = initialSize;

            ArrayPool = arrayPool;
            UseArrayPool = (ArrayPool != null);

            ByteArray = AllocateBuffer(InitialSize);
        }

        public void Dispose()
        {
            ReleaseBuffer(ByteArray);
            ByteArray = null;
            IsDisposed = true;
        }

        public int InitialSize { get; private set; }
        public double MinGrowthFactor { get; set; } = 1.5;
        public uint MinExtraSize { get; set; } = 512;

        public ArrayPool<byte> ArrayPool { get; private set; }
        private bool UseArrayPool { get; set; }

        public byte[] ByteArray { get; private set; }
        public int CurrentCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public byte[] TrimmedContentAsByteArray
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsDisposed)
                    new System.ObjectDisposedException(Fcns.CurrentClassLeafName).Throw();

                var currentCount = CurrentCount;
                if (currentCount == ByteArray.Length)
                {
                    return ByteArray;
                }
                else
                {
                    var trimmedBuffer = new byte[currentCount];
                    System.Buffer.BlockCopy(ByteArray, 0, trimmedBuffer, 0, currentCount);
                    return trimmedBuffer;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] AllocateBuffer(int size)
        {
            return UseArrayPool ? ArrayPool.Rent(size) : new byte[size];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReleaseBuffer(byte[] buffer, bool clearArray = false)
        {
            if (UseArrayPool)
                ArrayPool.Return(buffer, clearArray: clearArray);
        }

        public void ResetCount()
        {
            CurrentCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            CurrentCount += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (sizeHint > 0)
                EnsureAvailableSpace(sizeHint);
            else if (IsDisposed)
                new System.ObjectDisposedException(Fcns.CurrentClassLeafName).Throw();

            return new Memory<byte>(ByteArray, CurrentCount, ByteArray.Length - CurrentCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return GetMemory(sizeHint: sizeHint).Span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureAvailableSpace(int requestAvailableSpace)
        {
            if (IsDisposed)
                new System.ObjectDisposedException(Fcns.CurrentClassLeafName).Throw();

            var requestedTotalSize = requestAvailableSpace + CurrentCount;
            if (requestedTotalSize > ByteArray.Length)
            {
                var biggerSize = Math.Max((int)(requestedTotalSize + MinExtraSize), (int)Math.Ceiling(CurrentCount * MinGrowthFactor));
                var biggerArray = AllocateBuffer(biggerSize);

                ByteArray.CopyTo(biggerArray, 0);

                var releaseBuffer = ByteArray;
                ByteArray = biggerArray;

                ReleaseBuffer(releaseBuffer);
            }
        }
    }
}
