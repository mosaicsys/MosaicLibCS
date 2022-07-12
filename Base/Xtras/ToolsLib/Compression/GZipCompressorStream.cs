//-------------------------------------------------------------------
/*! @file GZipCompressorStream.cs
 *  @brief Defines classes that are used as a customized version of ICSharpCode.SharpZipLib.GZip.GZipOutputStream and ICSharpCode.SharpZipLib.GZip.GZipInputStream
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2021 Mosaic Systems Inc.
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
using System.Runtime.CompilerServices;

using MosaicLib.Modular.Common;
using MosaicLib.Utils;

namespace Mosaic.ToolsLib.Compression.GZip
{
    /// <summary>
    /// This class is a custom GZip compression stream, based on ICSharpCode.SharpZipLib.GZip.GZipOutputStream.
    /// It emphasizes support for read while writing use patterns (such as MDRF2 uses).
    /// Flush is used to commit all written content to the given intoStream and to help ensure that this content can be immediately read and processed by concurrent accessors.
    /// <para/>This GZipOutputStream variant uses Flush to Finish the current GZip segment so as to generate a concatinated GZip file
    /// that consists of multiple internal segments, one per explicitly flushed content.  
    /// This gzip format will be substantially more compatible with MDRF2 and other read while writing type patterns as it explicitly contemplates both the 
    /// Flush induced reader latency and related issues with CRC errors during reading when the reader attempts to read partial GZip file contents that have not been "finished".
    /// </summary>
    public class GZipCompressorStream : CompressorStreamBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public GZipCompressorStream(System.IO.Stream intoStream, int compressionLevel = 1, bool leaveStreamOpen = false, INamedValueSet nvs = null)
            : base(intoStream, Constants.GZipFileExtension, leaveStreamOpen, nvs)
        {
            ClippedCompressionLevel = compressionLevel.Clip(0, 9);
            CreateEncoder();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateEncoder()
        {
            encoderStream = new ICSharpCode.SharpZipLib.GZip.GZipOutputStream(TargetStream, BufferSize) { IsStreamOwner = false };
            encoderStream.SetLevel(ClippedCompressionLevel);
        }

        private int ClippedCompressionLevel { get; set; }

        private ICSharpCode.SharpZipLib.GZip.GZipOutputStream encoderStream;

        /// <inheritdoc/>
        protected override void CloseAndDisposeEngine()
        {
            encoderStream.Close();
            Fcns.DisposeOfObject(ref encoderStream);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void InnerWrite(byte[] buffer, int offset, int count)
        {
            encoderStream.Write(buffer, offset, count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void FlushEngine()
        {
            encoderStream.Flush();
            Fcns.DisposeOfObject(ref encoderStream);
            CreateEncoder();
        }
    }

    /// <summary>
    /// This class is a custom GZip decompression stream, based on ICSharpCode.SharpZipLib.GZip.GZipInputStream
    /// It supports use with read while writing use patterns (such as with MDRF2) including mapping of specific errors to end of file.
    /// </summary>
    public class GZipDecompressorStream : DecompressorStreamBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public GZipDecompressorStream(System.IO.Stream fromStream, bool leaveStreamOpen = false, INamedValueSet nvs = null)
            : base(fromStream, Constants.GZipFileExtension, leaveStreamOpen, nvs)
        {
            gzDecoderStream = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(SourceStream, BufferSize) { IsStreamOwner = !leaveStreamOpen };

            exceptionFullNamePrefixStringsToTreatAsEndOfStream.Add("ICSharpCode.SharpZipLib");
        }

        private ICSharpCode.SharpZipLib.GZip.GZipInputStream gzDecoderStream;

        /// <inheritdoc/>
        protected override void CloseAndDisposeEngine()
        {
            Fcns.DisposeOfObject(ref gzDecoderStream);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override int InnerRead(byte[] buffer, int offset, int count)
        {
            return gzDecoderStream?.Read(buffer, offset, count) ?? 0;
        }
    }
}
