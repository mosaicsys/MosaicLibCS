//-------------------------------------------------------------------
/*! @file CompressorStreams.cs
 *  @brief Defines common definitions that are used locally to support compressor and decompressor streams
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MosaicLib.Modular.Common;
using MosaicLib.Utils;

namespace Mosaic.ToolsLib.Compression
{
    #region CompressorSelect, Constants and related ExtensionMethods

    /// <summary>
    /// This enumeration lists the supported types and configurations of compreessor engines and compressor implementations that are supported here.
    /// <para/>None (0), LZ4, GZip
    /// </summary>
    /// <remarks>
    /// None uses a passthrought engine that applies no compression, 
    /// LZ4 uses the K4os.Compression.LZ4 nuget package.
    /// GZip uses the ICSharpCode.SharpZipLib's GZip related compression.
    /// In general LZ4 is more performant but is not as widely supported on Windows machines (and with normal tooling).  
    /// GZip is widely supported and is reasonably performant here (speed and ratio) but is not generally as good as LZ4 compression.
    /// Both support read while writing.
    /// </remarks>
    public enum CompressorSelect : int
    {
        /// <summary>No compression - stream acts as a passthrough.  FileExtension is empty string</summary>
        None = 0,

        /// <summary>Compress using LZ4Compreessor (defaults to using Chaining).  FileExtension is Constants.LZ4FileExtension [.lz4]</summary>
        LZ4,

        /// <summary>Compress using GZipCompressor.  FileExtension is Constants.GZipFileExtension [.gz]</summary>
        GZip,
    }

    /// <summary>
    /// Locally used constants
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Define the standard file extension (or add-on extension) that is used for files compressed using this stream type.
        /// </summary>
        public const string LZ4FileExtension = ".lz4";

        /// <summary>
        /// Define the standard file extension (or add-on extension) that is used for files compressed using this stream type.
        /// </summary>
        public const string GZipFileExtension = ".gz";

        /// <summary>
        /// Default buffer size used for streams, etc.  [65536]
        /// </summary>
        public const int DefaultBufferSize = 65536;

        /// <summary>
        /// Default block size [65536]
        /// </summary>
        public const int DefaultBlockSize = 65536;
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Attempts to determine which compressor selection to use for the given <paramref name="filePath"/> by looking at the file's last few characters.
        /// <para/>.lz4 -> CompressorSelect.LZ4 and .gz -> CompressorSelect.GZip.
        /// </summary>
        public static CompressorSelect GetCompressorSelectFromFilePath(this string filePath, StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase, CompressorSelect fallbackCompressorSelect = CompressorSelect.None)
        {
            if (filePath.EndsWith(Constants.LZ4FileExtension, stringComparison))
                return CompressorSelect.LZ4;
            else if (filePath.EndsWith(Constants.GZipFileExtension, stringComparison))
                return CompressorSelect.GZip;
            else
                return fallbackCompressorSelect;
        }

        /// <summary>
        /// Removes any supported compressor selection file name extension from the given <paramref name="filePath"/> and returns the resulting stem path.
        /// </summary>
        public static string RemoveCompressorSelectExtensionFromPath(this string filePath, StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase)
        {
            switch (filePath.GetCompressorSelectFromFilePath(stringComparison: stringComparison))
            {
                case CompressorSelect.LZ4: return filePath.RemoveSuffixIfNeeded(Constants.LZ4FileExtension);
                case CompressorSelect.GZip: return filePath.RemoveSuffixIfNeeded(Constants.GZipFileExtension);
                case CompressorSelect.None: return filePath;
                default: return filePath;
            }
        }

        /// <summary>
        /// This extension method takes a given <paramref name="compressorSelect"/> and returns the corresponding file extension that is used with it.
        /// </summary>
        public static string GetFileExtension(this CompressorSelect compressorSelect)
        {
            switch (compressorSelect)
            {
                case CompressorSelect.None: return "";
                case CompressorSelect.LZ4: return Constants.LZ4FileExtension;
                case CompressorSelect.GZip: return Constants.GZipFileExtension;
                default: new System.ArgumentException($"{compressorSelect} is not a valid value", "compressorSelect").Throw(); return null;
            }
        }

        /// <summary>
        /// This extension method takes a given <paramref name="compressorSelect"/> and returns the corresponding compressor factory that can be used with it.
        /// <para/>Accepts an optional <paramref name="compressionLevel"/>, <paramref name="leaveStreamOpen"/> and <paramref name="nvs"/> which may be used to pass additional configuration options to the compressor
        /// such as "blockSize", "bufferSize", "streamBufferSize", "outputBufferSize" - as appropriate for the corresponding compression engine.
        /// </summary>
        public static Func<System.IO.Stream, CompressorStreamBase> CreateCompressorFactory(this CompressorSelect compressorSelect, int compressionLevel = 1, bool leaveStreamOpen = false, INamedValueSet nvs = null)
        {
            switch (compressorSelect)
            {
                case CompressorSelect.None: return (targetStream) => new PassThroughCompressor(targetStream, leaveStreamOpen: leaveStreamOpen, nvs: nvs);
                case CompressorSelect.LZ4: return (targetStream) => new LZ4.LZ4CompressorStream(targetStream, compressionLevel: compressionLevel, chaining: true, leaveStreamOpen: leaveStreamOpen, nvs: nvs);
                case CompressorSelect.GZip: return (targetStream) => new GZip.GZipCompressorStream(targetStream, compressionLevel: compressionLevel, leaveStreamOpen: leaveStreamOpen, nvs: nvs);
                default: new System.ArgumentException($"{compressorSelect} is not a valid value", "compressorSelect").Throw(); return null;
            }
        }

        /// <summary>
        /// This extension method makes a compressor stream for the given <paramref name="targetStream"/> and a <paramref name="compressorSelect"/>.
        /// <para/>Accepts an optional <paramref name="compressionLevel"/>, <paramref name="leaveStreamOpen"/> and <paramref name="nvs"/> which may be used to pass additional configuration options to the compressor
        /// such as "blockSize", "bufferSize", "streamBufferSize", "outputBufferSize" - as appropriate for the corresponding compression engine.
        /// </summary>
        public static CompressorStreamBase CreateCompressor(this System.IO.Stream targetStream, CompressorSelect compressorSelect, int compressionLevel = 1, bool leaveStreamOpen = false, INamedValueSet nvs = null)
        {
            return compressorSelect.CreateCompressorFactory(compressionLevel, leaveStreamOpen, nvs: nvs).Invoke(targetStream);
        }

        /// <summary>
        /// This extension method takes a given <paramref name="compressorSelect"/> and returns the corresponding decompressor factory that can be used with it.
        /// <para/>Accepts an optional <paramref name="leaveStreamOpen"/> and <paramref name="nvs"/> which may be used to pass additional configuration options to the decompressor
        /// such as "bufferSize", "streamBufferSize" - as appropriate for the corresponding (de)compression engine.
        /// </summary>
        public static Func<System.IO.Stream, DecompressorStreamBase> CreateDecompressorFactory(this CompressorSelect compressorSelect, bool leaveStreamOpen = false, INamedValueSet nvs = null)
        {
            switch (compressorSelect)
            {
                case CompressorSelect.None: return (sourceStream) => new PassThroughDecompressor(sourceStream, leaveStreamOpen: leaveStreamOpen, nvs: nvs);
                case CompressorSelect.LZ4: return (sourceStream) => new LZ4.LZ4DecompressorStream(sourceStream, leaveStreamOpen: leaveStreamOpen, nvs: nvs);
                case CompressorSelect.GZip: return (sourceStream) => new GZip.GZipDecompressorStream(sourceStream, leaveStreamOpen: leaveStreamOpen, nvs: nvs);
                default: new System.ArgumentException($"{compressorSelect} is not a valid value", "compressorSelect").Throw(); return null;
            }
        }

        /// <summary>
        /// This extension method makes a decompressor stream for the given <paramref name="sourceStream"/> and a <paramref name="compressorSelect"/>.
        /// <para/>Accepts an optional <paramref name="leaveStreamOpen"/> and <paramref name="nvs"/> which may be used to pass additional configuration options to the compressor
        /// such as "bufferSize", "streamBufferSize" - as appropriate for the corresponding (de)compression engine.
        /// </summary>
        public static DecompressorStreamBase CreateDecompressor(this System.IO.Stream sourceStream, CompressorSelect compressorSelect, bool leaveStreamOpen = false, INamedValueSet nvs = null)
        {
            return compressorSelect.CreateDecompressorFactory(leaveStreamOpen: leaveStreamOpen, nvs: nvs).Invoke(sourceStream);
        }

        /// <summary>
        /// Attempts to open the given <paramref name="filePath"/> using the given <paramref name="fileMode"/>, <paramref name="fileAccess"/>, <paramref name="fileShare"/>, <paramref name="bufferSize"/>, and <paramref name="fileOptions"/>, 
        /// and then creates a compressor based on the extension on the given <paramref name="fileName"/> which is returned.  The Compressor will close the underlying FileStream when it is closed or disposed.
        /// </summary>
        public static CompressorStreamBase CreateCompressor(this string filePath, FileMode fileMode = FileMode.CreateNew, FileAccess fileAccess = FileAccess.Write, FileShare fileShare = FileShare.ReadWrite, int bufferSize = Constants.DefaultBufferSize, FileOptions fileOptions = FileOptions.None, INamedValueSet nvs = null)
        {
            return (new FileStream(filePath, fileMode, fileAccess, fileShare, bufferSize, fileOptions)).CreateCompressor(filePath.GetCompressorSelectFromFilePath(), nvs: nvs);
        }

        /// <summary>
        /// Attempts to open the given <paramref name="filePath"/> using the given <paramref name="fileMode"/>, <paramref name="fileAccess"/>, <paramref name="fileShare"/>, <paramref name="bufferSize"/>, and <paramref name="fileOptions"/>, 
        /// and then creates a decompressor based on the extension on the given <paramref name="fileName"/> which is returned.  The Decompressor will close the underlying FileStream when it is closed or disposed.
        /// </summary>
        public static DecompressorStreamBase CreateDecompressor(this string filePath, FileMode fileMode = FileMode.Open, FileAccess fileAccess = FileAccess.Read, FileShare fileShare = FileShare.Read, int bufferSize = Constants.DefaultBufferSize, FileOptions fileOptions = FileOptions.None, INamedValueSet nvs = null)
        {
            return (new FileStream(filePath, fileMode, fileAccess, fileShare, bufferSize, fileOptions)).CreateDecompressor(filePath.GetCompressorSelectFromFilePath(), nvs: nvs);
        }
    }

    #endregion

    /// <summary>
    /// Noop compressor stream type used for CompressorSelect.None.  Writes the bytes it is given directly to the underlying stream.
    /// </summary>
    public class PassThroughCompressor : CompressorStreamBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PassThroughCompressor(System.IO.Stream targetStream, bool leaveStreamOpen = false, INamedValueSet nvs = null)
            : base(targetStream, fileExtension: "", leaveStreamOpen: leaveStreamOpen, nvs: nvs)
        { }

        /// <inheritdoc/>
        protected override void CloseAndDisposeEngine() { }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void InnerWrite(byte[] buffer, int offset, int count)
        {
            TargetStream.Write(buffer, offset, count);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void FlushEngine() { }
    }

    /// <summary>
    /// Noop compressor stream type used for CompressorSelect.None.  Writes the bytes it is given directly to the underlying stream.
    /// </summary>
    public class PassThroughDecompressor : DecompressorStreamBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PassThroughDecompressor(System.IO.Stream sourceStream, bool leaveStreamOpen = false, INamedValueSet nvs = null)
            : base(sourceStream, fileExtension: "", leaveStreamOpen: leaveStreamOpen, nvs: nvs)
        { }

        /// <inheritdoc/>
        protected override void CloseAndDisposeEngine() { }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override int InnerRead(byte[] buffer, int offset, int count)
        {
            return SourceStream.Read(buffer, offset, count);
        }
    }

    /// <summary>
    /// This is the base class for all Compressor objects that are supported by this source.
    /// </summary>
    public abstract class CompressorStreamBase : System.IO.Stream, IIsDisposed
    {
        /// <summary>
        /// Protected constructor
        /// </summary>
        protected CompressorStreamBase(System.IO.Stream targetStream, string fileExtension, bool leaveStreamOpen, INamedValueSet nvs)
        {
            TargetStream = targetStream;
            initialTargetStreamPosition = TargetStream.Position;

            FileExtension = fileExtension;
            LeaveStreamOpen = leaveStreamOpen;
            NVS = nvs.MapNullToEmpty();

            BufferSize = (NVS["bufferSize"].VC.GetValue<int?>(rethrow: false) ?? Constants.DefaultBufferSize).Clip(1024, 1024 * 1024);
            BlockSize = (NVS["blockSize"].VC.GetValue<int?>(rethrow: false) ?? Constants.DefaultBlockSize).Clip(1024, 4 * 1024 * 1024);
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~CompressorStreamBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gives the stream into which the compressed bytes are to be written.
        /// </summary>
        protected System.IO.Stream TargetStream { get; private set; }

        private long initialTargetStreamPosition;

        /// <summary>
        /// Gives the file extension string that is (generally) used with this compressor.
        /// </summary>
        public string FileExtension { get; private set; }

        /// <summary>
        /// When set to true, this instance's Dispose() method will close the TargetStream using TargetStream.Close();
        /// </summary>
        public bool LeaveStreamOpen { get; set; }

        /// <summary>
        /// Used to allow passing additional configuration options to a given compressor type.
        /// </summary>
        public INamedValueSet NVS { get; private set; }

        /// <summary>A nomninal buffer size parameter</summary>
        protected int BufferSize { get; private set; }

        /// <summary>A nomninal blockSize size parameter</summary>
        protected int BlockSize { get; private set; }

        #region IDisposable and IIsDisposed interface support

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            isDisposing = true;

            if (disposing && !isDisposed)
            {
                FlushEngine();

                CloseAndDisposeEngine();

                if (LeaveStreamOpen)
                    TargetStream.Flush();
                else
                    TargetStream.Close();

                // Note the Stream base class calls SuppressFinalize automatically on return from this method.  Pattern is Dispose -> Close -> Dispose(true),SuppressFinalize ...
                // System.GC.SuppressFinalize(this);
            }

            isDisposed = true;
        }

        private volatile bool isDisposing, isDisposed;

        public bool IsDisposed => isDisposed;
        public bool IsDisposing => isDisposing;

        /// <summary>
        /// Abstract method which is used to close and dispose of all compression engine specific elements.
        /// <para/>Note that this method is called by Dispose(true) right after it has called FlushEngine.  
        /// As such this method should not need to flush any additional bytes.
        /// </summary>
        protected abstract void CloseAndDisposeEngine();

        #endregion

        /// <summary>Returns false</summary>
        public override bool CanRead => false;

        /// <summary>Returns true if the IntoStream.CanWrite</summary>
        public override bool CanWrite => TargetStream.CanWrite;

        /// <summary>Returns false</summary>
        public override bool CanSeek => false;

        /// <summary>Returns the count that has been written to the TargetStream since this compressor was constructed</summary>
        public override long Length => (TargetStream.Position - initialTargetStreamPosition);

        /// <summary>Getter returns the number of bytes that have been written to and encoded by this compressor object.  Setter throws NotImplementedException.</summary>
        public override long Position { get => _writtenCount; set => new NotImplementedException().Throw(); }

        protected long _writtenCount;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override void Write(byte[] buffer, int offset, int count)
        {
            this.ThrowIfDisposed(mesg: "Write cannot be used after Close or Dispose");

            InnerWrite(buffer, offset, count);

            _writtenCount += count;
        }

        /// <summary>
        /// Abstract innner method used to compress and writes bytes to the IntoStream.
        /// </summary>
        protected abstract void InnerWrite(byte[] buffer, int offset, int count);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush()
        {
            this.ThrowIfDisposed(mesg: "Flush cannot be used after Close or Dispose");

            FlushEngine();

            TargetStream.Flush();
        }

        /// <summary>
        /// This method is used to ask the underlying compression engine to commit any buffered (pending write) data to its encoder to and to push the resulting bytes to the IntoStream
        /// In addition this method may inject additional compression boundary frames if these are needed for partial content validation.
        /// </summary>
        protected abstract void FlushEngine();

        /// <summary>This method is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>This method is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override void SetLength(long value)
        {
            new NotImplementedException().Throw();
        }

        /// <summary>This method is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Exception type thrown if encoder internals failure is detected.
        /// </summary>
        public class EncodeFailedException : System.Exception
        {
            /// <summary>Constructor</summary>
            public EncodeFailedException(string mesg, System.Exception innerException = null) : base(mesg, innerException) { }
        }
    }

    /// <summary>
    /// This is the base class for all Decompressor objects that are supported by this source.
    /// </summary>
    public abstract class DecompressorStreamBase : System.IO.Stream, IIsDisposed
    {
        /// <summary>Defines the default value of the TreatEndOfStreamExceptionAsEndOfFile property for newly constructed DecompressorStreamBase instances when the provided nvs does not explicitly define the falue to use.  Defaults to true.</summary>
        public static bool DefaultTreatEndOfStreamExceptionAsEndOfFile { get; set; } = true;

        /// <summary>Defines the default value of the TreatExpectedDecompressionErrorsAsEndOfFile property for newly constructed DecompressorStreamBase instances when the provided nvs does not explicitly define the falue to use.  Defaults to true.</summary>
        public static bool DefaultTreatExpectedDecompressionErrorsAsEndOfFile { get; set; } = true;

        /// <summary>Defines the default value of the TreatAllErrorsAsEndOfFile property for newly constructed DecompressorStreamBase instances when the provided nvs does not explicitly define the value to use.  Defaults to false.</summary>
        public static bool DefaultTreatAllErrorsAsEndOfFile { get; set; }

        /// <summary>
        /// Protected constructor
        /// </summary>
        protected DecompressorStreamBase(System.IO.Stream sourceStream, string fileExtension, bool leaveStreamOpen, INamedValueSet nvs)
        {
            SourceStream = sourceStream;
            FileExtension = fileExtension;
            LeaveStreamOpen = leaveStreamOpen;
            NVS = nvs.MapNullToEmpty();

            BufferSize = (NVS["bufferSize"].VC.GetValue<int?>(rethrow: false) ?? Constants.DefaultBufferSize).Clip(1024, 1024 * 1024);
            TreatEndOfStreamExceptionAsEndOfFile = NVS["treatEndOfStreamExceptionAsEndOfFile"].VC.GetValue<bool?>(rethrow: false) ?? DefaultTreatEndOfStreamExceptionAsEndOfFile;
            TreatExpectedDecompressionErrorsAsEndOfFile = NVS["treatExpectedDecompressionErrorsAsEndOfFile"].VC.GetValue<bool?>(rethrow: false) ?? DefaultTreatExpectedDecompressionErrorsAsEndOfFile;
            TreatAllErrorsAsEndOfFile = NVS["treatAllErrorsAsEndOfFile"].VC.GetValue<bool?>(rethrow: false) ?? DefaultTreatAllErrorsAsEndOfFile;
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~DecompressorStreamBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gives the stream from which to get the compressed bytes that are to be decompressed and read.
        /// </summary>
        protected System.IO.Stream SourceStream { get; private set; }

        /// <summary>
        /// Gives the file extension string that is (generally) used with this compressor.
        /// </summary>
        public string FileExtension { get; private set; }

        /// <summary>
        /// When set to true, this instance's Dispose() method will close the SourceStream using SourceStream.Close();
        /// </summary>
        public bool LeaveStreamOpen { get; set; }

        /// <summary>
        /// Used to allow passing additional configuration options to a given compressor type.
        /// </summary>
        public INamedValueSet NVS { get; private set; }

        /// <summary>When true, the Read method will catch and handle the System.IO.EndOfStreamException as indicating end of file (to support read while writing with partially written contents).</summary>
        public bool TreatEndOfStreamExceptionAsEndOfFile { get; set; }

        /// <summary>When true, the Read method will catch and handle specific errors as indicating end of file (to support read while writing with partially written contents).</summary>
        public bool TreatExpectedDecompressionErrorsAsEndOfFile { get; set; }

        /// <summary>When true, the Read method will catch and handle all errors (exceptions) as indicating end of file (to support read while writing with partially written contents).</summary>
        public bool TreatAllErrorsAsEndOfFile { get; set; }

        /// <summary>A nomninal buffer size parameter</summary>
        protected int BufferSize { get; private set; }

        #region IDisposable and IIsDisposed interface support

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            isDisposing = true;

            if (disposing && !isDisposed)
            {
                CloseAndDisposeEngine();

                if (!LeaveStreamOpen)
                    SourceStream.Close();

                // Note the Stream base class calls SuppressFinalize automatically on return from this method.  Pattern is Dispose -> Close -> Dispose(true),SuppressFinalize ...
                // System.GC.SuppressFinalize(this);
            }

            isDisposed = true;
        }

        private volatile bool isDisposing, isDisposed;

        public bool IsDisposed => isDisposed;
        public bool IsDisposing => isDisposing;

        /// <summary>Abstract method which is used to close and dispose of all compression engine specific elements.</summary>
        protected abstract void CloseAndDisposeEngine();

        #endregion

        /// <summary>Returns FromStream.CanRead</summary>
        public override bool CanRead => SourceStream.CanRead;

        /// <summary>Returns false</summary>
        public override bool CanWrite => false;

        /// <summary>Returns false</summary>
        public override bool CanSeek => false;

        /// <summary>This property is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override long Length => throw new NotImplementedException();

        /// <summary>Getter returns the number of bytes that have been decoded and read using this decompressor.  Setter throws NotImplementedException.</summary>
        public override long Position { get => _decodedCount; set => new NotImplementedException().Throw(); }

        protected long _decodedCount;

        /// <summary>This method is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override void Write(byte[] sourceDataBuffer, int sourceDataOffset, int sourceDataCount)
        {
            new System.NotImplementedException().Throw();
        }

        /// <summary>This method is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override void Flush()
        {
            new NotImplementedException().Throw();
        }

        /// <summary>This method is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>This method is not supported by this class</summary>
        /// <exception cref="NotImplementedException"/>
        public override void SetLength(long value)
        {
            new NotImplementedException().Throw();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            this.ThrowIfDisposed(mesg: "Read cannot be used after Close of Dispose");

            int readCount = 0;

            try
            {
                int incrOffset = offset, remainingCount = count;
                for (; ; )
                {
                    var incrReadCount = InnerRead(buffer, incrOffset, remainingCount);

                    if (incrReadCount <= 0)
                        break;

                    readCount += incrReadCount;
                    incrOffset += incrReadCount;
                    remainingCount -= incrReadCount;

                    if (remainingCount <= 0)
                        break;
                }
            }
            catch (System.Exception ex)
            {
                var exType = ex.GetType();
                var recognized = (TreatEndOfStreamExceptionAsEndOfFile && exType == typeof(System.IO.EndOfStreamException))
                               || (TreatExpectedDecompressionErrorsAsEndOfFile && exceptionFullNamePrefixStringsToTreatAsEndOfStream.Any(prefix => exType.FullName.StartsWith(prefix)))
                               || TreatAllErrorsAsEndOfFile
                               ;

                if (recognized)
                    CloseAndDisposeEngine();
                else
                    throw;
            }

            if (readCount > 0)
                _decodedCount += readCount;

            return readCount;
        }

        /// <summary>
        /// This is the internal virtual/abstract method that the Read 
        /// </summary>
        protected abstract int InnerRead(byte[] buffer, int offset, int count);

        protected List<string> exceptionFullNamePrefixStringsToTreatAsEndOfStream = new List<string>();
    }
}
