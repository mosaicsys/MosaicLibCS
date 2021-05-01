//-------------------------------------------------------------------
/*! @file LZ4CompressorStream.cs
 *  @brief Defines a class used as a customized version of K4os.Compression.LZ4.Streams
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

using K4os.Compression.LZ4.Encoders;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;

namespace Mosaic.ToolsLib.Compression.LZ4
{
    /// <summary>
    /// This class is a custom LZ4 compression stream, based on K4os.Compression.LZ4.
    /// It emphasizes support for read while writing use patterns (such as MDRF2 uses).
    /// Flush is used to commit all written content to the given intoStream and to help ensure that this content can be immediately read and processed by concurrent accessors.
    /// <para/>Also note that LZ4 native stream compression implementation for this engine does not correctly support Flush in that it does not flush the compression engine itself in the tested version.
    /// </summary>
    public class LZ4CompressorStream : CompressorStreamBase
    {
        /// <summary>Ideally we will change this to true after verifitying the checksum inclusion logic is working correctly</summary>
        private const bool DefaultIncludeBlockChecksums = true;

        /// <summary>
        /// Constructor
        /// </summary>
        public LZ4CompressorStream(System.IO.Stream intoStream, int compressionLevel = 1, bool chaining = true, bool leaveStreamOpen = false, INamedValueSet nvs = null)
            : base(intoStream, Constants.LZ4FileExtension, leaveStreamOpen, nvs)
        {
            Chaining = NVS["chaining"].VC.GetValue<bool?>(rethrow: false) ?? true;
            ExtraBlocks = NVS["extraBlocks"].VC.GetValueI4(rethrow: false);
            IncludeBlockCheckSums = NVS["includeBlockCheckSums"].VC.GetValue<bool?>(rethrow: false) ?? DefaultIncludeBlockChecksums;

            SelectedLZ4Level = compressionLevel.ConvertToLZ4CompressionLevel();

            var useBlockSize = BlockSize.Clip(1024, 4 * 1024 * 1024);

            lz4Encoder = LZ4Encoder.Create(chaining: Chaining, level: SelectedLZ4Level, blockSize: useBlockSize, extraBlocks: ExtraBlocks);

            var minOutputBufferSize = K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(useBlockSize);
            var outputBufferSize = Math.Max(minOutputBufferSize, NVS["outputBufferSize"].VC.GetValue<int>(rethrow: false));

            outputBuffer = new byte[outputBufferSize];
        }

        private bool Chaining { get; set; }
        private int ExtraBlocks { get; set; }
        private bool IncludeBlockCheckSums { get; set; }

        private K4os.Compression.LZ4.LZ4Level SelectedLZ4Level { get; set; }

        private K4os.Compression.LZ4.Encoders.ILZ4Encoder lz4Encoder;
        private byte[] outputBuffer;

        /// <inheritdoc/>
        protected override void CloseAndDisposeEngine()
        {
            // write EndMark if needed
            if (startupFrameGenerated)
            {
                TargetStream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
            }

            Fcns.DisposeOfObject(ref lz4Encoder);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void InnerWrite(byte[] buffer, int offset, int count)
        {
            WriteStartFrameIfNeeded();

            int remainingSourceDataCount = count;

            for (int sourceProcessedCount = 0; remainingSourceDataCount > 0; )
            {
                var result = lz4Encoder.TopupAndEncode(source: buffer, sourceOffset: offset, sourceLength: remainingSourceDataCount, target: outputBuffer, targetOffset: 0, targetLength: outputBuffer.Length, forceEncode: false, allowCopy: true, loaded: out int loadedCountOut, encoded: out int encodedCountOut);

                if (loadedCountOut > 0)
                {
                    _writtenCount += loadedCountOut;
                    sourceProcessedCount += loadedCountOut;

                    offset += loadedCountOut;
                    remainingSourceDataCount -= loadedCountOut;
                }

                ProcessEncoderActionAndWriteBlockIfNeeded(result, encodedCountOut, remainingSourceDataCount);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void FlushEngine()
        {
            if (lz4Encoder.BytesReady > 0)
            {
                for (; ; )
                {
                    var encoderGivenSourceDataCount = lz4Encoder.BytesReady;

                    if (encoderGivenSourceDataCount == 0)
                        break;

                    var result = lz4Encoder.FlushAndEncode(target: outputBuffer, targetOffset: 0, targetLength: outputBuffer.Length, allowCopy: true, encoded: out int encodedCountOut);

                    ProcessEncoderActionAndWriteBlockIfNeeded(result, encodedCountOut, encoderGivenSourceDataCount);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessEncoderActionAndWriteBlockIfNeeded(EncoderAction encoderAction, int encodedCount, int encoderGivenSourceDataCount)
        {
            switch (encoderAction)
            {
                case EncoderAction.Encoded:
                    {
                        // prefix the block contents with its length - top bit not set because this is encoded.
                        blockU32WriteBuffer[0] = (byte)encodedCount;
                        blockU32WriteBuffer[1] = (byte)(encodedCount >> 8);
                        blockU32WriteBuffer[2] = (byte)(encodedCount >> 16);
                        blockU32WriteBuffer[3] = (byte)(encodedCount >> 24);

                        TargetStream.Write(blockU32WriteBuffer, 0, 4);

                        TargetStream.Write(outputBuffer, 0, encodedCount);

                        if (IncludeBlockCheckSums)
                        {
                            var checksum = K4os.Hash.xxHash.XXH32.DigestOf(outputBuffer, 0, encodedCount);

                            blockU32WriteBuffer[0] = (byte)checksum;
                            blockU32WriteBuffer[1] = (byte)(checksum >> 8);
                            blockU32WriteBuffer[2] = (byte)(checksum >> 16);
                            blockU32WriteBuffer[3] = (byte)(checksum >> 24);

                            TargetStream.Write(blockU32WriteBuffer, 0, 4);
                        }
                    }
                    break;
                case EncoderAction.Copied:
                    {
                        // prefix the block contents with its length - top bit set because this is copied.
                        blockU32WriteBuffer[0] = (byte)encodedCount;
                        blockU32WriteBuffer[1] = (byte)(encodedCount >> 8);
                        blockU32WriteBuffer[2] = (byte)(encodedCount >> 16);
                        blockU32WriteBuffer[3] = (byte)((encodedCount >> 24) | 0x80);

                        TargetStream.Write(blockU32WriteBuffer, 0, 4);

                        TargetStream.Write(outputBuffer, 0, encodedCount);

                        if (IncludeBlockCheckSums)
                        {
                            var checksum = K4os.Hash.xxHash.XXH32.DigestOf(outputBuffer, 0, encodedCount);

                            blockU32WriteBuffer[0] = (byte)checksum;
                            blockU32WriteBuffer[1] = (byte)(checksum >> 8);
                            blockU32WriteBuffer[2] = (byte)(checksum >> 16);
                            blockU32WriteBuffer[3] = (byte)(checksum >> 24);

                            TargetStream.Write(blockU32WriteBuffer, 0, 4);
                        }
                    }
                    break;
                case EncoderAction.Loaded:
                    break;

                case EncoderAction.None:
                    if (encodedCount != 0)
                        new EncodeFailedException($"lz4Encoder gave ${encoderAction} with ${encoderGivenSourceDataCount} bytes remaining").Throw();
                    break;

                default:
                    new EncodeFailedException($"lz4Encoder gave unxpected EncoderAction.${encoderAction}").Throw();
                    break;
            }
        }

        byte[] blockU32WriteBuffer = new byte[4];

        bool startupFrameGenerated = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStartFrameIfNeeded()
        {
            if (!startupFrameGenerated)
            {
                WriteStartupFrame();

                startupFrameGenerated = true;
            }
        }

        protected void WriteStartupFrame()
        {
            byte maxBlockSizeSizingValue = 0;
            if (BlockSize <= 65536)
                maxBlockSizeSizingValue = 4;
            else if (BlockSize <= 256 * 1024)
                maxBlockSizeSizingValue = 5;
            else if (BlockSize <= 1024 * 1024)
                maxBlockSizeSizingValue = 6;
            else if (BlockSize <= 4 * 1024 * 1024)
                maxBlockSizeSizingValue = 7;
            else
                new EncodeFailedException($"BlockSize: {BlockSize} is not a supported value (too large)").Throw();

            var startupFrameBytes = new byte[]
            {
                    // magic number
                    // 0x184D2204 - little endian
                    0x04,
                    0x22,
                    0x4d,
                    0x18,
                    // descriptor
                    // Flag Byte
                    (byte) ((1 << 6) // version 1 
                     | ((Chaining ? 0 : 1) << 5) // block independence (aka not chaining)
                     | ((IncludeBlockCheckSums ? 1 : 0) << 4) // include per block checksums
                     | (0 << 3) // content size is not included in this frame
                     | (0 << 2) // content checksum will not be included after the EndMark
                     | (0 << 1) // Reserved
                     | (0 << 0) // no dictionary ID will be included in this frame
                     ),
                    // BD Byte
                    (byte) ((maxBlockSizeSizingValue) << 4),
                // HC Byte will be added explicitly later.
            };

            var xxh32DigestValue = K4os.Hash.xxHash.XXH32.DigestOf(startupFrameBytes, 4, startupFrameBytes.Length - 4);

            startupFrameBytes = startupFrameBytes.Concat((byte)(xxh32DigestValue >> 8)).ToArray();

            TargetStream.Write(startupFrameBytes, 0, startupFrameBytes.Length);
        }
    }

    /// <summary>
    /// This class is a custom LZ4 decompression stream, based on K4os.Compression.LZ4.Stream
    /// It supports use with read while writing use patterns (such as with MDRF2) including mapping of specific errors to end of file.
    /// </summary>
    public class LZ4DecompressorStream : DecompressorStreamBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public LZ4DecompressorStream(System.IO.Stream fromStream, bool leaveStreamOpen = false, INamedValueSet nvs = null)
            : base(fromStream, Constants.LZ4FileExtension, leaveStreamOpen: leaveStreamOpen, nvs: nvs)
        {
            lz4DecoderStream = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(SourceStream, extraMemory: 0, leaveOpen: leaveStreamOpen, interactive: true);

            exceptionFullNamePrefixStringsToTreatAsEndOfStream.Add("K4os.Compression.LZ4");
        }

        private K4os.Compression.LZ4.Streams.LZ4DecoderStream lz4DecoderStream;

        /// <inheritdoc/>
        protected override void CloseAndDisposeEngine()
        {
            Fcns.DisposeOfObject(ref lz4DecoderStream);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override int InnerRead(byte[] buffer, int offset, int count)
        {
            return lz4DecoderStream?.Read(buffer, offset, count) ?? 0;
        }
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Converts the given generic <paramref name="compressionLevel"/> to an LZ4Level.  
        /// Nominally this adds one and clips.  It also maps some values to their nearest corresponding LZ4Level value.
        /// </summary>
        public static K4os.Compression.LZ4.LZ4Level ConvertToLZ4CompressionLevel(this int compressionLevel)
        {
            switch (compressionLevel)
            {
                case 1: return K4os.Compression.LZ4.LZ4Level.L00_FAST;
                case 2: return K4os.Compression.LZ4.LZ4Level.L03_HC;
                case 3: return K4os.Compression.LZ4.LZ4Level.L03_HC;
                case 4: return K4os.Compression.LZ4.LZ4Level.L03_HC;
                case 5: return K4os.Compression.LZ4.LZ4Level.L04_HC;
                case 6: return K4os.Compression.LZ4.LZ4Level.L05_HC;
                case 7: return K4os.Compression.LZ4.LZ4Level.L06_HC;
                case 8: return K4os.Compression.LZ4.LZ4Level.L07_HC;
                case 9: return K4os.Compression.LZ4.LZ4Level.L08_HC;
                case 10: return K4os.Compression.LZ4.LZ4Level.L09_HC;
                case 11: return K4os.Compression.LZ4.LZ4Level.L10_OPT;
                case 12: return K4os.Compression.LZ4.LZ4Level.L11_OPT;
                case 13: return K4os.Compression.LZ4.LZ4Level.L12_MAX;
                default: return (compressionLevel <= 0) ? K4os.Compression.LZ4.LZ4Level.L00_FAST : K4os.Compression.LZ4.LZ4Level.L12_MAX;
            }
        }
    }
}
