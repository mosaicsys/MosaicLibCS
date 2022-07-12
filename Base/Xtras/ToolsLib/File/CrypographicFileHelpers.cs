//-------------------------------------------------------------------
/*! @file CryptographicFileHelpera.cs
 *  @brief helper classs used to support ability to read from and write to files that use cryptography.
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
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using MosaicLib;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

namespace Mosaic.ToolsLib.File.Cryptography
{
    /// <summary>
    /// This iterface is supported by all of the FileHelper classes in this namespace.
    /// </summary>
    /// <remarks>
    /// The classes that are used to support this interface make use of System.Security.Cryptography and are thus generally considered to be computationally complex.
    /// In addition their related constructors and setup related properties setters may trigger significant cryptographic initialization related work.
    /// As such construction and disposal of these objects should be performed as rarely as practal.
    /// </remarks>
    public interface ICryptographicFileHelper : IDisposable
    {
        /// <summary>Gives the path that this helper will use when performing Read and/or Write operations.</summary>
        string FilePath { get; set; }

        /// <summary>When non-null this property gives the byte array value that Read will return when the indicated file does not exist.  Defaults to an empty array of bytes.</summary>
        byte[] FileNotFoundReadValue { get; set; }

        /// <summary>When true (the default) the Write operation will attempt to delete the file if any aspect of the normal Write pattern fails.</summary>
        bool AttemptToDeleteFileOnFailedWrite { get; set; }

        /// <summary>
        /// Reads, validates and/or decrypts the file contents and returns said contents as an array.  The data returned will be the decrypted data with any HMAC hash header removed.
        /// Handling for all file and cryptographic related exceptions that may be thrown are determined by the <paramref name="rethrow"/> and <paramref name="fallbackValue"/> (optional) parameters.
        /// On exception if <paramref name="rethrow"/> is false then this method will return the given <paramref name="fallbackValue"/> otherwise the exception will be passed to the caller.
        /// </summary>
        byte[] Read(bool rethrow = false, byte[] fallbackValue = null);

        /// <summary>
        /// Writes the given data to the file after applying any selected encryption and/or validation HMAC header generation and prefixing.
        /// Handling for all file and cryptographic related exceptions that may be thrown are determined by the <paramref name="rethrow"/> and <paramref name="fallbackValue"/> (optional) parameters.
        /// On exception if <paramref name="rethrow"/> is false then this method will return the given <paramref name="fallbackValue"/> otherwise the exception will be passed to the caller.
        /// </summary>
        bool Write(byte[] data, bool rethrow = false);

        /// <summary>Reads the file using Read(rethrow:true) and then constructs and returns a read-only MemoryStream on the resulting byte array.</summary>
        Stream ReaderStream { get; }

        /// <summary>Constructs and returns a variant of a writable MemoryStream that will call Write on the accumulated stream contents when the stream is Close()d or Dispose()ed</summary>
        Stream WriterStream { get; }
    }

    /// <summary>
    /// The exception that is throw when internal exception conditions are found in the related helper classes (such as file verification failed).
    /// </summary>
    public class CryptographicFileHelperException : System.Exception
    {
        public CryptographicFileHelperException(string mesg, System.Exception innerException = null) : base(mesg, innerException) { }
    }

    /// <summary>
    /// Base class for file helper classes in this namespace.  Provides common implementations related to reader and writer stream generation.
    /// </summary>
    public abstract class CryptgraphicFileHelperBase : DisposableBaseBaseWithExplicitDisposeList, ICryptographicFileHelper
    {
        /// <summary>Protected default constructor</summary>
        protected CryptgraphicFileHelperBase()
        {
            AddExplicitDisposeAction(() => HandleDispose());
        }

        /// <summary>Abstract method called to </summary>
        public virtual void HandleDispose()
        {
            HasBeenDisposed = true;
        }

        /// <summary>Returns true if this instance has been explicitly disposed</summary>
        protected bool HasBeenDisposed { get; private set; }

        /// <inheritdoc/>
        public string FilePath
        {
            get { return _FilePath; }
            set
            {
                _FilePath = value;
                FullFilePath = _FilePath.IsNeitherNullNorEmpty() ? System.IO.Path.GetFullPath(_FilePath) : _FilePath;
            }
        }
        protected string _FilePath;

        protected string FullFilePath { get; private set; }

        /// <inheritdoc/>
        public byte[] FileNotFoundReadValue { get; set; } = EmptyArrayFactory<byte>.Instance;

        /// <inheritdoc/>
        public bool AttemptToDeleteFileOnFailedWrite { get; set; } = true;

        /// <inheritdoc/>
        public abstract byte[] Read(bool rethrow, byte[] fallbackValue = null);
        /// <inheritdoc/>
        public abstract bool Write(byte[] data, bool rethrow);

        /// <inheritdoc/>
        public Stream ReaderStream { get { return new MemoryStream(Read(rethrow: true, fallbackValue: EmptyArrayFactory<byte>.Instance), writable: false); } }
        /// <inheritdoc/>
        public Stream WriterStream
        {
            get
            {
                if (HasBeenDisposed)
                    new CryptographicFileHelperException($"{Fcns.CurrentMethodName} failed: Helper has been Disposed").Throw();

                return new WriteDataToFileOnDisposeStream(this);
            }
        }

        /// <summary>
        /// Custom variant of writable MemoryStream which tells the parent ICryptographicFileHelper to write the stream accumulated bytes to the file helper instance when the stream is closed (which is also automatically triggered when it is disposed).
        /// </summary>
        private class WriteDataToFileOnDisposeStream : MemoryStream
        {
            /// <summary>
            /// Constructor.  Caller must provide the ICryptographicFileHelper <paramref name="icfh"/> instance to use.
            /// </summary>
            public WriteDataToFileOnDisposeStream(ICryptographicFileHelper icfh, int capacity = 4096)
                : base(capacity: capacity)
            {
                ICFH = icfh;
            }

            /// <summary>
            /// Gives the ICryptogarphicFileHelper instance that will be used to write the contents on Close
            /// </summary>
            public ICryptographicFileHelper ICFH { get; private set; }

            /// <inheritdoc/>
            public override void Close()
            {
                ICFH.Write(this.ToArray());

                base.Close();
            }
        }
    }

    /// <summary>
    /// This helper class is used to support writing and reading file contents with an automatic prefix that includes an HMAC to support detection of attempts to tamper with the file contents.
    /// <para/>This class supports use of a string prefix in front of the HMAC hash which is generally used warn users not to modify the files contents.
    /// <para/>This class requires that the client specify or explicitly provide the HMAC type or instance to use and as such it is agnostic as to the actual HMAC algorthm that is to be employed.
    /// </summary>
    public class TamperResistantFileHelper : CryptgraphicFileHelperBase
    {
        public const string DefaultFilePrefix = "HMAC based tamper resistant file.  DO NOT MODIFY.  ";
        private static readonly byte[] _DefaultFilePrefixByteArray = ByteArrayTranscoders.ByteStringTranscoder.Decode(DefaultFilePrefix);

        /// <summary>
        /// Constructor.  Caller must provide the <paramref name="hmacAlgorithmName"/> and the <paramref name="keyByteArray"/>.
        /// <para/>When the 
        /// </summary>
        public TamperResistantFileHelper(string hmacAlgorithmName, byte [] keyByteArray)
        {
            if (hmacAlgorithmName.IsNeitherNullNorEmpty())
            {
                HMAC = HMAC.Create(hmacAlgorithmName);
                HMAC.Key = keyByteArray.MapNullToEmpty();
            }
        }

        public override void HandleDispose()
        {
            Fcns.DisposeOfObject(ref _HMAC);
            base.HandleDispose();
        }

        public string FilePrefix { get { return _FilePrefix; } set { _FilePrefixByteArray = ByteArrayTranscoders.ByteStringTranscoder.Decode(_FilePrefix = value.MapNullToEmpty()); } }
        public byte[] FilePrefixByteArray { get { return _FilePrefixByteArray.SafeToArray(); } }

        protected string _FilePrefix = DefaultFilePrefix;
        protected byte[] _FilePrefixByteArray = _DefaultFilePrefixByteArray;

        public HMAC HMAC
        {
            get { return _HMAC; }
            set
            {
                Fcns.DisposeOfObject(ref _HMAC);
                _HMAC = value;
            }
        }
        private HMAC _HMAC = null;

        public override byte[] Read(bool rethrow, byte[] fallbackValue = null)
        {
            try
            {
                if (HasBeenDisposed)
                    new CryptographicFileHelperException($"{Fcns.CurrentMethodName} failed: Helper has been Disposed").Throw();

                if (FilePath.IsNullOrEmpty())
                    new CryptographicFileHelperException($"{Fcns.CurrentMethodName} failed: FilePath must be non-empty").Throw();

                if (!System.IO.File.Exists(FullFilePath) && FileNotFoundReadValue != null)
                    return FileNotFoundReadValue;

                byte[] fileBytes = System.IO.File.ReadAllBytes(FullFilePath);

                int filePrefixLength = _FilePrefixByteArray.Length;
                if (!fileBytes.Take(filePrefixLength).SequenceEqual(_FilePrefixByteArray))
                    new CryptographicFileHelperException($"{Fcns.CurrentMethodName} failed: file does not start with expected prefix [{FilePath}]").Throw();

                int hashByteCount = ((HMAC?.HashSize ?? 0) + 7) >> 3;
                int hexHashStringByteCount = hashByteCount * 2;

                var totalHeaderLength = filePrefixLength + hexHashStringByteCount;
                var computedHashBytes = HMAC?.ComputeHash(fileBytes, totalHeaderLength, fileBytes.Length - totalHeaderLength) ?? EmptyArrayFactory<byte>.Instance;

                var fileHexHashBytesString = ByteArrayTranscoders.ByteStringTranscoder.Encode(fileBytes, filePrefixLength, hexHashStringByteCount);
                var fileHashBytes = ByteArrayTranscoders.HexStringTranscoderNoPadding.Decode(fileHexHashBytesString);

                if (!computedHashBytes.SequenceEqual(fileHashBytes))
                    new CryptographicFileHelperException($"{Fcns.CurrentMethodName} failed: tamper detection has been triggered [{FilePath}]").Throw();

                return fileBytes.SafeSubArray(totalHeaderLength);
            }
            catch
            {
                if (rethrow)
                    throw;

                return fallbackValue;
            }
        }

        public override bool Write(byte[] data, bool rethrow)
        {
            try
            {
                if (HasBeenDisposed)
                    new CryptographicFileHelperException($"{Fcns.CurrentMethodName} failed: Helper has been Disposed").Throw();

                if (FilePath.IsNullOrEmpty())
                    new CryptographicFileHelperException($"{Fcns.CurrentMethodName} failed: FilePath must be non-empty").Throw();

                var hashBytes = HMAC?.ComputeHash(data) ?? EmptyArrayFactory<byte>.Instance;
                var hexHashBytesStr = ByteArrayTranscoders.HexStringTranscoderNoPadding.Encode(hashBytes);
                var hexHashBytes = ByteArrayTranscoders.ByteStringTranscoder.Decode(hexHashBytesStr);

                using (var fs = System.IO.File.Open(FullFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    fs.Write(FilePrefixByteArray, 0, FilePrefixByteArray.Length);
                    fs.Write(hexHashBytes, 0, hexHashBytes.Length);
                    fs.Write(data, 0, data.Length);
                }

                return true;
            }
            catch
            {
                try
                {
                    if (System.IO.File.Exists(FullFilePath) && AttemptToDeleteFileOnFailedWrite)
                        System.IO.File.Delete(FullFilePath);
                }
                catch { }

                if (rethrow)
                    throw;

                return false;
            }
        }
    }

    public static partial class ExtensionMethods
    {
        public static byte[] BuildKeyBytes(this object keySourceRoot)
        {
            List<byte> keyByteArrayBuilderList = new List<byte>();

            if (keySourceRoot is IEnumerable<object>)
            {
                foreach (var keySourceItem in (IEnumerable<object>)keySourceRoot)
                    keyByteArrayBuilderList.AddKeySourceItem(keySourceItem);
            }
            else if (keySourceRoot != null)
            {
                keyByteArrayBuilderList.AddKeySourceItem(keySourceRoot);
            }

            return keyByteArrayBuilderList.ToArray();
        }

        public static void AddKeySourceItem(this List<byte> keyByteArrayBuilderList, object keySourceItem)
        {
            var addData = keySourceItem as byte[];

            if (addData == null)
            {
                if (keySourceItem == null)
                    addData = nullProxyByteArray;
                else if (keySourceItem is string)
                    addData = System.Text.UTF8Encoding.Default.GetBytes((string)keySourceItem);
                else
                {
                    addData = new byte[4];
                    MosaicLib.Utils.Data.Unpack(unchecked((UInt32)keySourceItem.GetHashCode()), addData);
                }
            }

            keyByteArrayBuilderList.SafeAddRange(addData);
        }

        private static readonly byte[] nullProxyByteArray = ByteArrayTranscoders.ByteStringTranscoder.Decode("nullProxyBytes"); 
    }
}
