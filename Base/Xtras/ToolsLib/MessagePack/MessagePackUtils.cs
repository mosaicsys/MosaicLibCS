//-------------------------------------------------------------------
/*! @file MessagePackEMs.cs
 *  @brief MessagePack specific extension methods and related type specific formatters that are used with ValueContainers and MessagePack based serialization.
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

using MessagePack;
using MessagePack.Formatters;

using Mosaic.ToolsLib.Compression;

using MosaicLib;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Common.CustomSerialization;
using MosaicLib.Semi.E039;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Mosaic.ToolsLib.MessagePackUtils
{
    public static partial class Instances
    {
        public static readonly VCMesgPackFormatterResolver VCFormatterResolver = new VCMesgPackFormatterResolver();

        /// <summary>
        /// Starts with the Standard options with the resolver set ot VCFormatterResolver and with OmitAssemblyVersion=true and AllowAssemblyVersionMismatch=true
        /// </summary>
        public static readonly MessagePack.MessagePackSerializerOptions VCDefaultMPOptions = MessagePack.MessagePackSerializerOptions.Standard.WithResolver(VCFormatterResolver).WithOmitAssemblyVersion(true).WithAllowAssemblyVersionMismatch(true);
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary>
        /// Serializes the given <paramref name="vc"/> to the given <paramref name="mpWriter"/> using the given <paramref name="options"/> ?? MessagePackSerializerOptions.Standard.
        /// Internally uses a <see cref="VCFormatter"/> for this purpose.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeVC(this ref MessagePack.MessagePackWriter mpWriter, ValueContainer vc, MessagePack.MessagePackSerializerOptions options = null)
        {
            VCFormatter.Instance.Serialize(ref mpWriter, vc, options ?? Instances.VCDefaultMPOptions);
        }

        /// <summary>
        /// Serializes the given <paramref name="nvs"/> to the given <paramref name="mpWriter"/> using the given <paramref name="options"/> ?? MessagePackSerializerOptions.Standard.
        /// Internally uses a <see cref="VCFormatter"/> for this purpose.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeAsVC(this ref MessagePack.MessagePackWriter mpWriter, INamedValueSet nvs, MessagePack.MessagePackSerializerOptions options = null)
        {
            VCFormatter.Instance.SerializeWithExtHeader(ref mpWriter, nvs, options ?? Instances.VCDefaultMPOptions);
        }

        /// <summary>
        /// Attempts to deserialize and return a ValueContainer from the given <paramref name="mpReader"/> using the given <paramref name="options"/> ?? MessagePackSerializerOptions.Standard.
        /// Internally uses a <see cref="VCFormatter"/> for this purpose.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueContainer DeserializeVC(this ref MessagePack.MessagePackReader mpReader, MessagePack.MessagePackSerializerOptions options = null)
        {
            return VCFormatter.Instance.Deserialize(ref mpReader, options ?? Instances.VCDefaultMPOptions);
        }

        /// <summary>
        /// Extracts the next MP record from the given <paramref name="mpReader"/> and returns a JSON string from it using MessagePackSerializer.ConverToJson.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ConvertNextRecordToJSON(this ref MessagePack.MessagePackReader mpReader)
        {
            using (var stringWriter = new StringWriter())
            {
                MessagePackSerializer.ConvertToJson(ref mpReader, stringWriter);
                return stringWriter.ToString();
            }
        }
    }

    /// <summary>
    /// This class is responsible for converting between VC notation and MesgPack notation.
    /// </summary>
    public class VCFormatter : IMessagePackFormatter<ValueContainer>, IMessagePackFormatter
    {
        public static readonly VCFormatter Instance = new VCFormatter();

        public void Serialize(ref MessagePackWriter mpWriter, ValueContainer value, MessagePackSerializerOptions options)
        {
            var cst = value.cvt;

            // first write out the simple types that use native MP typecodes unchanged.

            // Note that some of these used forced size writes in order to simplify the reconstruction.   
            // This is not the most efficient format but it does help make certain that the deserialized VC will have the same contents as the original one did without extra though.
            // hopefully the compression engine will undo most of the inefficiency added by forcing original data types to use their full width in the output format rather than letting MP choose the most efficient format.

            switch (cst)
            {
                case ContainerStorageType.None: mpWriter.WriteNil(); return;
                case ContainerStorageType.Bo: mpWriter.Write(value.u.b); return;
                case ContainerStorageType.I1: mpWriter.WriteInt8(value.u.i8); return;
                case ContainerStorageType.I2: mpWriter.WriteInt16(value.u.i16); return;
                case ContainerStorageType.I4: mpWriter.WriteInt32(value.u.i32); return;
                case ContainerStorageType.I8: mpWriter.WriteInt64(value.u.i64); return;
                case ContainerStorageType.U1: mpWriter.WriteUInt8(value.u.u8); return;
                case ContainerStorageType.U2: mpWriter.WriteUInt16(value.u.u16); return;
                case ContainerStorageType.U4: mpWriter.WriteUInt32(value.u.u32); return;
                case ContainerStorageType.U8: mpWriter.WriteUInt64(value.u.u64); return;
                case ContainerStorageType.F4: mpWriter.Write(value.u.f32); return;
                case ContainerStorageType.F8: mpWriter.Write(value.u.f64); return;
                case ContainerStorageType.A:
                    {
                        var a = (value.o as string);
                        if (a != null)
                        {
                            mpWriter.Write(a);
                            return;
                        }
                    }
                    break;

                case ContainerStorageType.L:
                    {
                        var l = (value.o as ReadOnlyIList<ValueContainer>);
                        if (l != null)
                        {
                            vcLFormatter.Serialize(ref mpWriter, l, options);
                            return;
                        }
                    }
                    break;

                default:
                    break;
            }

            // for all other cases we avoid ambiguity by using an extension header to encode the expected CST type, and other details that goes with the ValueContainer's contents.
            SerializeWithExtHeader(ref mpWriter, cst, value, options);
        }

        /// <summary>This method is used for all of the formats that will use an extension header.</summary>
        public void SerializeWithExtHeader(ref MessagePackWriter mpWriter, ContainerStorageType cst, ValueContainer value, MessagePackSerializerOptions options)
        {
            // Prefix all Extension Header hinted types with a FixMap1 item.  As such the Extension Header will be stored as the map's key and the hinted type will be stored as the corresponding value.
            mpWriter.WriteMapHeader(1);

            // custom cases (NVS, NV, Custom, Object)
            switch (cst)
            {
                case ContainerStorageType.Custom:
                    if (value.o is Logging.LogGate)
                    {
                        mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)MPExtensionType.LogGate, 0));
                        mpWriter.Write(value.GetValue<Logging.LogGate>(rethrow: false).ToString());
                    }
                    else
                    {
                        mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)cst), 0));
                        mpWriter.Write($"MPSerializeWithExtHeader: Inavlid usage of CST.Custom for {value}");
                    }
                    break;

                case ContainerStorageType.Object:
                    {
                        var o = value.o;
                        Type oType = o?.GetType();

                        if (o == null)
                        {
                            mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)ContainerStorageType.Object), 0));
                            mpWriter.WriteNil();    // in this particular case we just write Nil since it is unambiguous here.  Reading Nil will produce ValueContainer.Null.
                        }
                        else if (o is BiArray)
                        {
                            mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.ArrayOfCSTBase + (sbyte)ContainerStorageType.Bi), 0));
                            u1ArrayFormatter.Serialize(ref mpWriter, (byte[])((BiArray)o), options);
                        }
                        else if (oType.IsArray)
                        {
                            var oItemCST = ValueContainer.GetDecodedTypeInfo(oType.GetElementType()).cst;
                            var oa = (Array)value.o;
                            var oaLength = oa.Length;

                            mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.ArrayOfCSTBase + (sbyte)oItemCST), 0));

                            switch (oItemCST)
                            {
                                case ContainerStorageType.Bo: boArrayFormatter.Serialize(ref mpWriter, (bool[])o, options); break;
                                case ContainerStorageType.Bi: u1ArrayFormatter.Serialize(ref mpWriter, (byte[])o, options); break;
                                case ContainerStorageType.I1: i1ArrayFormatter.Serialize(ref mpWriter, (sbyte[])o, options); break;
                                case ContainerStorageType.I2: i2ArrayFormatter.Serialize(ref mpWriter, (short[])o, options); break;
                                case ContainerStorageType.I4: i4ArrayFormatter.Serialize(ref mpWriter, (int[])o, options); break;
                                case ContainerStorageType.I8: i8ArrayFormatter.Serialize(ref mpWriter, (long[])o, options); break;
                                case ContainerStorageType.U1: u1ArrayFormatter.Serialize(ref mpWriter, (byte[])o, options); break;
                                case ContainerStorageType.U2: u2ArrayFormatter.Serialize(ref mpWriter, (ushort[])o, options); break;
                                case ContainerStorageType.U4: u4ArrayFormatter.Serialize(ref mpWriter, (uint[])o, options); break;
                                case ContainerStorageType.U8: u8ArrayFormatter.Serialize(ref mpWriter, (ulong[])o, options); break;
                                case ContainerStorageType.F4: f4ArrayFormatter.Serialize(ref mpWriter, (float[])o, options); break;
                                case ContainerStorageType.F8: f8ArrayFormatter.Serialize(ref mpWriter, (double[])o, options); break;
                                default: mpWriter.Write($"MPSerializeWithExtHeader: Unsupported Array item type {oItemCST} in {value}"); break;
                            }
                        }
                        else
                        {
                            var customVCSerializer = MosaicLib.Modular.Common.CustomSerialization.CustomSerialization.Instance.GetCustomTypeSerializerItemFor(oType, "MessagePack");
                            var tavc = customVCSerializer.Serialize(o);

                            mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)MPExtensionType.VCE, 0));

                            TypeAndValueCarrierFormatter.Instance.Serialize(ref mpWriter, tavc, options);
                        }
                    }
                    break;

                case ContainerStorageType.LS:
                    mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)cst), 0));
                    lsFormatter.Serialize(ref mpWriter, value.GetValueLS(rethrow: false), options);
                    break;

                case ContainerStorageType.Bi:
                    mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)cst), 0));
                    mpWriter.Write(value.u.bi);
                    break;

                case ContainerStorageType.TS:
                    mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)cst), 0));
                    mpWriter.WriteInt64(value.u.TimeSpan.Ticks);
                    break;

                case ContainerStorageType.DT:
                    mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)cst), 0));
                    mpWriter.WriteInt64(value.u.DateTime.ToBinary());
                    break;

                case ContainerStorageType.NVS:
                    SerializeWithExtHeader(ref mpWriter, value.GetValueNVS(rethrow: false), options, addMapHeader: false);
                    break;

                case ContainerStorageType.NV:
                    SerializeWithExtHeader(ref mpWriter, value.GetValueNV(rethrow: false), options, addMapHeader: false);
                    break;

                default:
                    mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)cst), 0));
                    mpWriter.Write($"MPSerializeWithExtHeader: Unsupported cst {cst} in {value}");
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SerializeWithExtHeader(ref MessagePackWriter mpWriter, INamedValueSet nvs, MessagePackSerializerOptions options, bool addMapHeader = true)
        {
            if (addMapHeader)
                mpWriter.WriteMapHeader(1);
            mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)ContainerStorageType.NVS), 0));
            NVSFormatter.Instance.Serialize(ref mpWriter, nvs, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SerializeWithExtHeader(ref MessagePackWriter mpWriter, INamedValue nv, MessagePackSerializerOptions options, bool addMapHeader = true)
        {
            if (addMapHeader)
                mpWriter.WriteMapHeader(1);
            mpWriter.WriteExtensionFormatHeader(new ExtensionHeader((sbyte)(MPExtensionType.CSTBase + (sbyte)ContainerStorageType.NV), 0));
            NVFormatter.Instance.Serialize(ref mpWriter, nv, options);
        }

        /// <summary>Gives the ValueContainer value that is produced when encountering Nil on its own - produces ValueContainer.Empty.  Nil in other contexts will produce other results, typically type specific references to null.</summary>
        private static readonly ValueContainer vcForNil = ValueContainer.Empty;
        private static readonly ValueContainer vcTrue = ValueContainer.CreateBo(true);
        private static readonly ValueContainer vcFalse = ValueContainer.CreateBo(false);

        public ValueContainer Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            // First handle the values that use a simple type code that we can easily process and populate a ValueContainer from.

            var mpHeaderFormat = unchecked((MPHeaderByteCode)mpReader.NextCode);

            switch (mpHeaderFormat)
            {
                case MPHeaderByteCode.Nil: mpReader.ReadNil(); return vcForNil;
                case MPHeaderByteCode.True: mpReader.Skip(); return vcTrue;
                case MPHeaderByteCode.False: mpReader.Skip(); return vcFalse;
                case MPHeaderByteCode.Int8: return ValueContainer.CreateI1(mpReader.ReadSByte());
                case MPHeaderByteCode.Int16: return ValueContainer.CreateI2(mpReader.ReadInt16());
                case MPHeaderByteCode.Int32: return ValueContainer.CreateI4(mpReader.ReadInt32());
                case MPHeaderByteCode.Int64: return ValueContainer.CreateI8(mpReader.ReadInt64());
                case MPHeaderByteCode.UInt8: return ValueContainer.CreateU1(mpReader.ReadByte());
                case MPHeaderByteCode.UInt16: return ValueContainer.CreateU2(mpReader.ReadUInt16());
                case MPHeaderByteCode.UInt32: return ValueContainer.CreateU4(mpReader.ReadUInt32());
                case MPHeaderByteCode.UInt64: return ValueContainer.CreateU8(mpReader.ReadUInt64());
                case MPHeaderByteCode.Float32: return ValueContainer.CreateF4(mpReader.ReadSingle());
                case MPHeaderByteCode.Float64: return ValueContainer.CreateF8(mpReader.ReadDouble());
                case MPHeaderByteCode.Str8:
                case MPHeaderByteCode.Str16:
                case MPHeaderByteCode.Str32: return ValueContainer.CreateA(mpReader.ReadString());
                case MPHeaderByteCode.FixMap1:
                    {
                        int mapItemCount = mpReader.ReadMapHeader();
                        var mapKeyMPType = mpReader.NextMessagePackType;

                        if (mapItemCount == 1 && mapKeyMPType == MessagePackType.Extension)
                            return HandleDeserializeVCWithExtPrefix(ref mpReader, options);
                        else
                            return HandleDeserialzeUnexpectedMapBodyToNVS(ref mpReader, mapItemCount, options);
                    }
                default:
                    if (mpHeaderFormat >= MPHeaderByteCode.PositiveFixInt_First && mpHeaderFormat <= MPHeaderByteCode.PositiveFixInt_Last)
                    {
                        return ValueContainer.CreateU1(mpReader.ReadByte());
                    }
                    else if (mpHeaderFormat >= MPHeaderByteCode.NegFixInt_First && mpHeaderFormat <= MPHeaderByteCode.NegFixInt_Last)
                    {
                        return ValueContainer.CreateI1(mpReader.ReadSByte());
                    }
                    break;
            }

            // the rest of the cases use the MP libraries MessagePackType concept to generate a summary type.
            var mesgPackType = mpReader.NextMessagePackType;

            switch (mesgPackType)
            {
                case MessagePackType.String:
                    return ValueContainer.CreateA(mpReader.ReadString());

                case MessagePackType.Array:
                    return ValueContainer.Create(vcLFormatter.Deserialize(ref mpReader, options));

                case MessagePackType.Binary:
                    return ValueContainer.Create(mpReader.ReadBytes()?.First.ToArray());

                case MessagePackType.Map:   // this is not the normal way to read an NVS as it is usually prefixed with an extension record.
                    return HandleDeserialzeUnexpectedMapBodyToNVS(ref mpReader, mpReader.ReadMapHeader(), options);

                case MessagePackType.Extension:
                    // NOTE: this is not the normal way to encounter an ext block for VC decoding.  Normally the extension block is the key item in a FixMap1.
                    return HandleDeserializeVCWithExtPrefix(ref mpReader, options);

                case MessagePackType.Integer:
                case MessagePackType.Unknown:
                default:
                    {
                        var entryConsumed = mpReader.Consumed;
                        mpReader.Skip();
                        return ValueContainer.CreateA($"Skipped invalid/unexpected MesgPack block [mpt:{mesgPackType}, mphf:{mpHeaderFormat}, size:{mpReader.Consumed - entryConsumed}]");
                    }
            }
        }

        private ValueContainer HandleDeserializeVCWithExtPrefix(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            var mpHeaderFormat = unchecked((MPHeaderByteCode)mpReader.NextCode);
            var mesgPackType = mpReader.NextMessagePackType;

            var extHdr = mpReader.ReadExtensionFormatHeader();
            var mpExtType = unchecked((MPExtensionType)extHdr.TypeCode);

            if (extHdr.Length == 0)
            {
                switch (mpExtType)
                {
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.None: return ValueContainer.Empty;
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.Bi: return ValueContainer.CreateBi(mpReader.ReadByte());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.I1: return ValueContainer.CreateI1(mpReader.ReadSByte());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.I2: return ValueContainer.CreateI2(mpReader.ReadInt16());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.I4: return ValueContainer.CreateI4(mpReader.ReadInt32());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.U1: return ValueContainer.CreateU1(mpReader.ReadByte());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.U2: return ValueContainer.CreateU2(mpReader.ReadUInt16());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.U4: return ValueContainer.CreateU4(mpReader.ReadUInt32());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.DT: return ValueContainer.CreateDT(DateTime.FromBinary(mpReader.ReadInt64()));
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.TS: return ValueContainer.CreateTS(TimeSpan.FromTicks(mpReader.ReadInt64()));
                    case MPExtensionType.LogGate: return ValueContainer.Create((Logging.LogGate)mpReader.ReadString());
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.LS: return ValueContainer.Create(lsFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.INamedValue: return ValueContainer.CreateNV(NVFormatter.Instance.Deserialize(ref mpReader, options));
                    case MPExtensionType.CSTBase + (int)ContainerStorageType.INamedValueSet: return ValueContainer.CreateNVS(NVSFormatter.Instance.Deserialize(ref mpReader, options));

                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.Bo: return ValueContainer.Create(boArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.Bi: return ValueContainer.Create(new BiArray(u1ArrayFormatter.Deserialize(ref mpReader, options)));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.I1: return ValueContainer.Create(i1ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.I2: return ValueContainer.Create(i2ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.I4: return ValueContainer.Create(i4ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.I8: return ValueContainer.Create(i8ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.U1: return ValueContainer.Create(u1ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.U2: return ValueContainer.Create(u2ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.U4: return ValueContainer.Create(u4ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.U8: return ValueContainer.Create(u8ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.F4: return ValueContainer.Create(f4ArrayFormatter.Deserialize(ref mpReader, options));
                    case MPExtensionType.ArrayOfCSTBase + (int)ContainerStorageType.F8: return ValueContainer.Create(f8ArrayFormatter.Deserialize(ref mpReader, options));

                    case MPExtensionType.CSTBase + (int)ContainerStorageType.Object: mpReader.ReadNil(); return ValueContainer.Null;

                    case MPExtensionType.VCE:
                        {
                            var tavc = TypeAndValueCarrierFormatter.Instance.Deserialize(ref mpReader, options);

                            if (tavc != null)
                            {
                                if (tavc.ErrorCode.IsNullOrEmpty())
                                {
                                    var cts = MosaicLib.Modular.Common.CustomSerialization.CustomSerialization.Instance.GetCustomTypeSerializerItemFor(tavc);

                                    return ValueContainer.CreateFromObject(cts.Deserialize(tavc));
                                }
                                else
                                {
                                    return ValueContainer.CreateA($"MPDeserializeWithExtPrefix: encountered invalid TypeAndValueCarrier: {tavc}");
                                }
                            }
                            else
                            {
                                return ValueContainer.Null;
                            }
                        }

                    default:
                        break;
                }
            }

            if (extHdr.Length > 0)
                mpReader.ReadRaw(extHdr.Length);

            return ValueContainer.CreateA($"MPDeserializeWithExtPrefix: Skipped unknown Extension MesgPack block [mpt:{mesgPackType}, mphf:{mpHeaderFormat}, et:{mpExtType}, len:{extHdr.Length}]");
        }

        private ValueContainer HandleDeserialzeUnexpectedMapBodyToNVS(ref MessagePackReader mpReader, int mapItemCount, MessagePackSerializerOptions options)
        {
            var nvs = new NamedValueSet();

            for (int idx = 0; idx < mapItemCount; idx++)
            {
                var nVC = Deserialize(ref mpReader, options);
                var v = Deserialize(ref mpReader, options);

                nvs.SetValue(nVC.GetValueA(rethrow: false).Sanitize(), v);
            }

            return ValueContainer.CreateNVS(nvs.MakeReadOnly());
        }

        public void SkipCurrentVCRecord(ref MessagePackReader mpReader)
        {
            var mpt = mpReader.NextMessagePackType;

            switch (mpt)
            {
                case MessagePackType.Unknown:
                case MessagePackType.Integer:
                case MessagePackType.Nil:
                case MessagePackType.Boolean:
                case MessagePackType.Float:
                case MessagePackType.String:
                case MessagePackType.Binary:
                case MessagePackType.Extension:     // we do not expect to see extension blocks at this level - they are only expected as the first key item in a FixMap1 block.
                default:
                    mpReader.Skip();
                    break;

                case MessagePackType.Array:
                    {
                        int arrayCount = mpReader.ReadArrayHeader();

                        for (int i = 0; i < arrayCount; i++)
                            SkipCurrentVCRecord(ref mpReader);
                    }
                    break;

                case MessagePackType.Map:
                    {
                        int mapCount = mpReader.ReadMapHeader();
                        var keyMPT = mpReader.NextMessagePackType;

                        if (mapCount == 1 && keyMPT == MessagePackType.Extension)
                        {
                            mpReader.Skip();    //skip the extension block
                            SkipCurrentVCRecord(ref mpReader);
                        }
                        else
                        {
                            for (int i = 0; i < mapCount; i++)
                            {
                                mpReader.Skip();        // map keys are never VC serialized - under normal VC serialization this will always be a string.
                                SkipCurrentVCRecord(ref mpReader);
                            }
                        }
                    }
                    break;
            }
        }

        private static readonly ReadOnlyIListFormatter<ValueContainer> vcLFormatter = new ReadOnlyIListFormatter<ValueContainer>();
        private static readonly ReadOnlyIListFormatter<string> lsFormatter = new ReadOnlyIListFormatter<string>();
        private static readonly ArrayFormatter<bool> boArrayFormatter = new ArrayFormatter<bool>();
        private static readonly ArrayFormatter<sbyte> i1ArrayFormatter = new ArrayFormatter<sbyte>();
        private static readonly ArrayFormatter<short> i2ArrayFormatter = new ArrayFormatter<short>();
        private static readonly ArrayFormatter<int> i4ArrayFormatter = new ArrayFormatter<int>();
        private static readonly ArrayFormatter<long> i8ArrayFormatter = new ArrayFormatter<long>();
        private static readonly ArrayFormatter<byte> u1ArrayFormatter = new ArrayFormatter<byte>();
        private static readonly ArrayFormatter<ushort> u2ArrayFormatter = new ArrayFormatter<ushort>();
        private static readonly ArrayFormatter<uint> u4ArrayFormatter = new ArrayFormatter<uint>();
        private static readonly ArrayFormatter<ulong> u8ArrayFormatter = new ArrayFormatter<ulong>();
        private static readonly ArrayFormatter<float> f4ArrayFormatter = new ArrayFormatter<float>();
        private static readonly ArrayFormatter<double> f8ArrayFormatter = new ArrayFormatter<double>();
    }

    /// <summary>
    /// This class is responsible for converting between NVS notation and MesgPack notation.
    /// <para/>contents are serialized using Map notation where the keys are the string nv.Names for each nv in the corresponding nvs and the map values use VCFormatter serialization.
    /// </summary>
    public class NVSFormatter : IMessagePackFormatter<INamedValueSet>, IMessagePackFormatter<NamedValueSet>, IMessagePackFormatter
    {
        public static readonly NVSFormatter Instance = new NVSFormatter();

        void IMessagePackFormatter<NamedValueSet>.Serialize(ref MessagePackWriter mpWriter, NamedValueSet nvs, MessagePackSerializerOptions options)
        {
            Serialize(ref mpWriter, nvs, options);
        }

        public void Serialize(ref MessagePackWriter mpWriter, INamedValueSet nvs, MessagePackSerializerOptions options)
        {
            if (nvs != null)
            {
                var nvsCount = nvs.Count;
                mpWriter.WriteMapHeader(nvsCount);

                for (int idx = 0; idx < nvsCount; idx++)
                {
                    var nv = nvs[idx];
                    mpWriter.Write(nv.Name);
                    VCFormatter.Instance.Serialize(ref mpWriter, nv.VC, options);
                }
            }
            else
            {
                mpWriter.WriteNil();
            }
        }

        INamedValueSet IMessagePackFormatter<INamedValueSet>.Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            return Deserialize(ref mpReader, options);
        }

        public NamedValueSet Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            if (mpReader.TryReadNil())
                return null;

            int mapItemCount = mpReader.ReadMapHeader();

            var nvs = new NamedValueSet();

            for (int idx = 0; idx < mapItemCount; idx++)
            {
                var name = mpReader.ReadString();
                var vc = VCFormatter.Instance.Deserialize(ref mpReader, options);
                nvs.SetValue(name.Sanitize(), vc);
            }

            return nvs.MakeReadOnly();
        }
    }

    /// <summary>
    /// This class is responsible for converting between NV notation and MesgPack notation.
    /// <para/>contents are serialized using L2 notation where the first list item is the nv.name and the second list item is the nv.Value using VCFormatter serialization
    /// </summary>
    public class NVFormatter : IMessagePackFormatter<INamedValue>, IMessagePackFormatter<NamedValue>, IMessagePackFormatter
    {
        public static readonly NVFormatter Instance = new NVFormatter();

        void IMessagePackFormatter<NamedValue>.Serialize(ref MessagePackWriter mpWriter, NamedValue nv, MessagePackSerializerOptions options)
        {
            Serialize(ref mpWriter, nv, options);
        }

        public void Serialize(ref MessagePackWriter mpWriter, INamedValue nv, MessagePackSerializerOptions options)
        {
            if (nv != null)
            {
                mpWriter.WriteArrayHeader(2);

                mpWriter.Write(nv.Name);
                VCFormatter.Instance.Serialize(ref mpWriter, nv.VC, options);
            }
            else
            {
                mpWriter.WriteNil();
            }
        }

        INamedValue IMessagePackFormatter<INamedValue>.Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            return Deserialize(ref mpReader, options);
        }

        public NamedValue Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            if (mpReader.TryReadNil())
                return null;

            var arrayLen = mpReader.ReadArrayHeader();

            if (arrayLen == 2)
            {
                return new NamedValue(mpReader.ReadString(), VCFormatter.Instance.Deserialize(ref mpReader, options)).MakeReadOnly();
            }
            else
            {
                foreach (var idx in Enumerable.Range(0, arrayLen))
                    VCFormatter.Instance.SkipCurrentVCRecord(ref mpReader);

                return new NamedValue("", $"NVFormater.Deserialize: Invalid NamedValue array length [{arrayLen} != 2]");
            }
        }
    }

    /// <summary>
    /// Variant of CollectionFormatterBase that can be used with ReadOnlyIList instances
    /// </summary>
    public class ReadOnlyIListFormatter<TItemType> : CollectionFormatterBase<TItemType, TItemType[], ReadOnlyIList<TItemType>>
    {
        public static readonly ReadOnlyIListFormatter<TItemType> Instance = new ReadOnlyIListFormatter<TItemType>();

        protected override void Add(TItemType[] collection, int index, TItemType value, MessagePackSerializerOptions options)
        {
            collection[index] = value;
        }

        protected override ReadOnlyIList<TItemType> Complete(TItemType[] intermediateCollection)
        {
            return new ReadOnlyIList<TItemType>(intermediateCollection);
        }

        protected override TItemType[] Create(int count, MessagePackSerializerOptions options)
        {
            return new TItemType[count];
        }
    }

    /// <summary>
    /// This class is used as a serialiaztion/deserialization formatter for TandAndValueCarrier objects.
    /// </summary>
    public class TypeAndValueCarrierFormatter : IMessagePackFormatter<TypeAndValueCarrier>, IMessagePackFormatter
    {
        public static readonly TypeAndValueCarrierFormatter Instance = new TypeAndValueCarrierFormatter();

        public void Serialize(ref MessagePackWriter mpWriter, TypeAndValueCarrier tavc, MessagePackSerializerOptions options)
        {
            if (tavc != null)
            {
                bool hasEC = tavc.ErrorCode.IsNeitherNullNorEmpty();

                mpWriter.WriteArrayHeader(hasEC ? 6 : 5);

                mpWriter.Write(tavc.TypeStr);
                mpWriter.Write(tavc.AssemblyFileName);
                mpWriter.Write(tavc.FactoryName.MapEmptyToNull());
                mpWriter.Write(tavc.ValueStr.MapEmptyToNull());
                mpWriter.Write(tavc.ValueByteArray.MapEmptyToNull());

                if (hasEC)
                    mpWriter.Write(tavc.ErrorCode);
            }
            else
            {
                mpWriter.WriteNil();
            }
        }

        public TypeAndValueCarrier Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            if (mpReader.TryReadNil())
                return null;

            var arrayLen = mpReader.ReadArrayHeader();

            bool hasEC = (arrayLen == 6);

            if (arrayLen == 5 || hasEC)
            {
                var typeStr = mpReader.ReadString();
                var assyFileName = mpReader.ReadString();
                var factoryName = mpReader.ReadString();
                var valueStr = mpReader.ReadString();
                var valueByteArray = mpReader.ReadBytes()?.First.ToArray();
                var ec = (hasEC) ? mpReader.ReadString() : null;

                var tavc = new TypeAndValueCarrier(typeStr, assyFileName, factoryName, valueStr, valueByteArray, errorCode: ec);

                return tavc;
            }
            else
            {
                foreach (var idx in Enumerable.Range(0, arrayLen))
                    mpReader.Skip();

                return new TypeAndValueCarrier(errorCode: $"TAVCFormatter.Deserialize: Invalid TypeAndValueCarrier array length [{arrayLen} != 5 && != 6]");
            }
        }
    }

    /// <summary>
    /// This class is used as a serialization/deserialization formatter for Logging.ILogMessage and Logging.LogMessage objects.
    /// </summary>
    public class LogMessageFormatter : IMessagePackFormatter<Logging.ILogMessage>, IMessagePackFormatter<Logging.LogMessage>, IMessagePackFormatter
    {
        public static readonly LogMessageFormatter Instance = new LogMessageFormatter();

        private static readonly IMessagePackFormatter<Logging.MesgType> mesgTypeFormatter = new MessagePack.Formatters.EnumAsStringFormatter<Logging.MesgType>();

        void IMessagePackFormatter<Logging.LogMessage>.Serialize(ref MessagePackWriter mpWriter, Logging.LogMessage lm, MessagePackSerializerOptions options)
        {
            Serialize(ref mpWriter, lm, options);
        }

        public void Serialize(ref MessagePackWriter mpWriter, Logging.ILogMessage lm, MessagePackSerializerOptions options)
        {
            if (lm != null)
            {
                mpWriter.WriteArrayHeader(12);

                mpWriter.Write(lm.LoggerName);
                mesgTypeFormatter.Serialize(ref mpWriter, lm.MesgType, options);
                mpWriter.Write(lm.Mesg);
                NVSFormatter.Instance.Serialize(ref mpWriter, lm.NamedValueSet.MapEmptyToNull(), options);
                mpWriter.Write(lm.Data);
                mpWriter.Write(lm.Emitted);
                mpWriter.Write(lm.EmittedQpcTime.Time);
                mpWriter.Write(lm.SeqNum);
                mpWriter.Write(lm.ThreadID);
                mpWriter.Write(lm.Win32ThreadID);
                mpWriter.Write(lm.ThreadName);
                mpWriter.Write(lm.EmittedDateTime.ToUniversalTime().Ticks);
            }
            else
            {
                mpWriter.WriteNil();
            }
        }

        Logging.ILogMessage IMessagePackFormatter<Logging.ILogMessage>.Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            return Deserialize(ref mpReader, options);
        }

        public Logging.LogMessage Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            if (mpReader.TryReadNil())
                return null;

            var arrayLen = mpReader.ReadArrayHeader();

            if (arrayLen == 12)
            {
                var loggerName = mpReader.ReadString();
                var mesgType = mesgTypeFormatter.Deserialize(ref mpReader, options);
                var mesg = mpReader.ReadString();
                var nvs = NVSFormatter.Instance.Deserialize(ref mpReader, options);
                var data = mpReader.ReadBytes()?.ToArray();
                var emitted = mpReader.ReadBoolean();
                var emittedQpcTime = new QpcTimeStamp(mpReader.ReadDouble());
                var seqNum = mpReader.ReadInt32();
                var threadID = mpReader.ReadInt32();
                var win32ThreadID = mpReader.ReadInt32();
                var threadName = mpReader.ReadString();
                var emittedDateTime = new DateTime(mpReader.ReadInt64(), DateTimeKind.Utc);

                var lm = Logging.LogMessage.Generate(loggerName, 0, mesgType, mesg, nvs, data, emitted, emittedQpcTime, seqNum, threadID, win32ThreadID, threadName, emittedDateTime);
                return lm;
            }
            else
            {
                foreach (var idx in Enumerable.Range(0, arrayLen))
                    mpReader.Skip();

                return Logging.LogMessage.Generate("ILogMessageFormatter.Deserialize", 0, Logging.MesgType.Error, $"Invalid serialized ILogMessage array length [{arrayLen} != 12]", null, null, true, QpcTimeStamp.Now, 0, 0, 0, null, DateTime.Now);
            }
        }
    }

    /// <summary>
    /// This class is used as a serialization/deserialization formatter for IE039Object and E039Objcts objects.
    /// </summary>
    public class E039ObjectFormatter : IMessagePackFormatter<IE039Object>, IMessagePackFormatter<E039Object>, IMessagePackFormatter
    {
        public static readonly E039ObjectFormatter Instance = new E039ObjectFormatter();

        private static readonly IMessagePackFormatter<E039ObjectFlags> e039ObjectFlagsFormatter = new MessagePack.Formatters.EnumAsStringFormatter<E039ObjectFlags>();

        void IMessagePackFormatter<E039Object>.Serialize(ref MessagePackWriter mpWriter, E039Object e039Object, MessagePackSerializerOptions options)
        {
            Serialize(ref mpWriter, e039Object, options);
        }

        public void Serialize(ref MessagePackWriter mpWriter, IE039Object e039Object, MessagePackSerializerOptions options)
        {
            if (e039Object != null)
            {
                mpWriter.WriteArrayHeader(4);

                E039ObjectIDFormatter.Instance.Serialize(ref mpWriter, e039Object.ID, options);
                e039ObjectFlagsFormatter.Serialize(ref mpWriter, e039Object.Flags, options);
                NVSFormatter.Instance.Serialize(ref mpWriter, e039Object.Attributes, options);

                var linksToOtherObjectsList = e039Object.LinksToOtherObjectsList.MapNullToEmpty();
                mpWriter.WriteMapHeader(linksToOtherObjectsList.Count);
                foreach (var link in linksToOtherObjectsList)
                {
                    mpWriter.Write(link.Key);
                    E039ObjectIDFormatter.Instance.Serialize(ref mpWriter, link.ToID, options);
                }
            }
            else
            {
                mpWriter.WriteNil();
            }
        }

        IE039Object IMessagePackFormatter<IE039Object>.Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            return Deserialize(ref mpReader, options);
        }

        public E039Object Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            if (mpReader.TryReadNil())
                return null;

            var arrayLen = mpReader.ReadArrayHeader();

            if (arrayLen == 4)
            {
                var e039ObjectID = E039ObjectIDFormatter.Instance.Deserialize(ref mpReader, options);
                var flags = e039ObjectFlagsFormatter.Deserialize(ref mpReader, options);
                var attributes = NVSFormatter.Instance.Deserialize(ref mpReader, options);

                var linkToOtherMapCount = mpReader.ReadMapHeader();
                var linksToOtherObjectsList = new List<E039Link>(linkToOtherMapCount);
                for (int idx = 0; idx < linkToOtherMapCount; idx++)
                {
                    var key = mpReader.ReadString();
                    var toID = E039ObjectIDFormatter.Instance.Deserialize(ref mpReader, options);

                    linksToOtherObjectsList.Add(new E039Link(e039ObjectID, toID, key));
                }

                var e039Object = new E039Object(e039ObjectID, flags, attributes, linksToOtherObjectsList);
                return e039Object;
            }
            else
            {
                foreach (var idx in Enumerable.Range(0, arrayLen))
                    mpReader.Skip();

                return new E039Object(new E039ObjectID($"Invalid serialized IE039Object array length [{arrayLen} != 4]", "IE039Object.Deserialize"), E039ObjectFlags.IsFinal, null);
            }
        }
    }

    /// <summary>
    /// This class is used as a serialization/deserialization formatter for E039Link objects.
    /// </summary>
    public class E039LinkFormatter : IMessagePackFormatter<E039Link>, IMessagePackFormatter
    {
        public static readonly E039LinkFormatter Instance = new E039LinkFormatter();

        public void Serialize(ref MessagePackWriter mpWriter, E039Link e039Link, MessagePackSerializerOptions options)
        {
            mpWriter.WriteArrayHeader(3);

            E039ObjectIDFormatter.Instance.Serialize(ref mpWriter, e039Link.FromID, options);
            mpWriter.Write(e039Link.Key);
            E039ObjectIDFormatter.Instance.Serialize(ref mpWriter, e039Link.ToID, options);
        }

        public E039Link Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            var arrayLen = mpReader.ReadArrayHeader();

            if (arrayLen == 3)
            {
                var fromID = E039ObjectIDFormatter.Instance.Deserialize(ref mpReader, options);
                var key = mpReader.ReadString();
                var toID = E039ObjectIDFormatter.Instance.Deserialize(ref mpReader, options);

                var e039Link = new E039Link(fromID, toID, key);
                return e039Link;
            }
            else
            {
                foreach (var idx in Enumerable.Range(0, arrayLen))
                    mpReader.Skip();

                return new E039Link(new E039ObjectID($"Invalid serialized E039ObjectID array length [{arrayLen} != 3]", "E039Link.Deserialize"), E039ObjectID.Empty, "Error");
            }
        }
    }

    /// <summary>
    /// This class is used as a serialization/deserialization formatter for E039Link objects.
    /// </summary>
    public class E039ObjectIDFormatter : IMessagePackFormatter<E039ObjectID>, IMessagePackFormatter
    {
        public static readonly E039ObjectIDFormatter Instance = new E039ObjectIDFormatter();

        public void Serialize(ref MessagePackWriter mpWriter, E039ObjectID e039ObjectID, MessagePackSerializerOptions options)
        {
            if (e039ObjectID != null)
            {
                bool hasUUID = (e039ObjectID.UUID.IsNeitherNullNorEmpty());

                mpWriter.WriteArrayHeader(hasUUID ? 3 : 2);

                mpWriter.Write(e039ObjectID.Name);
                mpWriter.Write(e039ObjectID.Type);
                if (hasUUID)
                    mpWriter.Write(e039ObjectID.UUID);
            }
            else
            {
                mpWriter.WriteNil();
            }
        }

        public E039ObjectID Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            if (mpReader.TryReadNil())
                return null;

            var arrayLen = mpReader.ReadArrayHeader();

            bool hasUUID = (arrayLen == 3);
            if (arrayLen == 2 || hasUUID)
            {
                var name = mpReader.ReadString();
                var type = mpReader.ReadString();
                var uuid = (hasUUID) ? mpReader.ReadString() : null;

                var e039ObjectID = new E039ObjectID(name, type, uuid);
                return e039ObjectID;
            }
            else
            {
                foreach (var idx in Enumerable.Range(0, arrayLen))
                    mpReader.Skip();

                return new E039ObjectID($"Invalid serialized E039ObjectID array length [{arrayLen} != 3]", "E039ObjectID.Deserialize");
            }
        }
    }

    /// <summary>
    /// This class is used as a serialization/deserialization formatter for object types that are supported by MosaicLib.Modular.Common.CustomSerialization.
    /// </summary>
    public class CustomTypeSerializerFormatter<TItemType> : IMessagePackFormatter<TItemType>, IMessagePackFormatter
    {
        public CustomTypeSerializerFormatter(MosaicLib.Modular.Common.CustomSerialization.ITypeSerializerItem typeSerializerItem)
        {
            TypeSerializerItem = typeSerializerItem;
        }

        private ITypeSerializerItem TypeSerializerItem { get; set; }

        public void Serialize(ref MessagePackWriter mpWriter, TItemType item, MessagePackSerializerOptions options)
        {
            var tavc = TypeSerializerItem.Serialize(item);

            TypeAndValueCarrierFormatter.Instance.Serialize(ref mpWriter, tavc, options);
        }

        public TItemType Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions options)
        {
            var tavc = TypeAndValueCarrierFormatter.Instance.Deserialize(ref mpReader, options);
            return (TItemType)TypeSerializerItem.Deserialize(tavc);
        }
    }

    /// <summary>
    /// Formatter Resolver used with ValueContainer serialization.
    /// <para/>ValueContainer => VCFormatter, 
    /// <para/>INVS => INVSFormatter, 
    /// <para/>INV => INVFormatter, 
    /// <para/>TypeAndValueCarrier => TAVCFormatter, 
    /// <para/>Logging.ILogMessage => ILogMessageFormatter
    /// <para/>other => FallbackResolver based resolution, or MessagePack.MessagePackSerializer.DefaultOptions.Resolver if no FallbackResolver was explicitly specified.
    /// </summary>
    public class VCMesgPackFormatterResolver : IFormatterResolver
    {
        public VCMesgPackFormatterResolver(IFormatterResolver fallbackResolver = null, ICustomSerialization customSerialization = null, string defaultFactoryName = null)
        {
            FallbackResolver = fallbackResolver ?? MessagePack.MessagePackSerializer.DefaultOptions.Resolver;
            CustomSerialization = customSerialization ?? MosaicLib.Modular.Common.CustomSerialization.CustomSerialization.Instance;
            DefaultFactoryName = defaultFactoryName;
        }

        /// <summary>
        /// Gives the IFormatterResolver to be used when there is no KnownTypeFormatter to be used for a given type.  
        /// Defaults to MessagePack.MessagePackSerializer.DefaultOptions.Resolver if no fallbackResolver has been explicitly given to the constructor.
        /// </summary>
        public IFormatterResolver FallbackResolver { get; private set; }

        public ICustomSerialization CustomSerialization { get; private set; }

        public string DefaultFactoryName { get; private set; }

        /// <summary>
        /// Returns the KnownTypeFormatter to use of the given <typeparamref name="T"/>, or uses the FallbackResolver to obtain the formatter for this type otherwise.
        /// </summary>
        public virtual IMessagePackFormatter<T> GetFormatter<T>()
        {
            var formatter = KnownTypeFormatter<T>.Formatter;

            if (formatter == null)
                formatter = TryGetTAVCCustomFormatter<T>();

            if (formatter == null)
                formatter = FallbackResolver.GetFormatter<T>();

            return formatter;
        }

        /// <summary>
        /// This static class is used to pre-determine a set of Formatters to be used for different types using the combination of DotNet static initializers with templatized classes.
        /// </summary>
        private static class KnownTypeFormatter<T>
        {
            /// <summary>
            /// Cached, known formatter to use for type {T}, or null if no such formatter is known for this type.
            /// </summary>
            public static readonly IMessagePackFormatter<T> Formatter;

            /// <summary>
            /// class constructor for given type <typeparamref name="T"/>.  Directly recognizes specific types and assigns the static Formatter to the correct formatter instance to use for these cases.   Otherwise sets the Formatter to be null.
            /// </summary>
            static KnownTypeFormatter()
            {
                var typeOfT = typeof(T);
                IMessagePackFormatter<T> formatter = null;

                if (typeOfT == typeof(ValueContainer))
                    formatter = (IMessagePackFormatter<T>)VCFormatter.Instance;
                else if (typeof(INamedValueSet).IsAssignableFrom(typeOfT))
                    formatter = (IMessagePackFormatter<T>)NVFormatter.Instance;
                else if (typeof(INamedValueSet).IsAssignableFrom(typeOfT))
                    formatter = (IMessagePackFormatter<T>)NVFormatter.Instance;
                else if (typeOfT == typeof(TypeAndValueCarrier))
                    formatter = (IMessagePackFormatter<T>)TypeAndValueCarrierFormatter.Instance;
                else if (typeof(Logging.ILogMessage).IsAssignableFrom(typeOfT))
                    formatter = (IMessagePackFormatter<T>)LogMessageFormatter.Instance;
                else if (typeof(IE039Object).IsAssignableFrom(typeOfT))
                    formatter = (IMessagePackFormatter<T>)E039ObjectFormatter.Instance;
                else if (typeOfT == typeof(E039Link))
                    formatter = (IMessagePackFormatter<T>)E039LinkFormatter.Instance;
                else if (typeOfT == typeof(E039ObjectID))
                    formatter = (IMessagePackFormatter<T>)E039ObjectIDFormatter.Instance;

                if (formatter == null && LocalConstants.primitiveTypeSet.Contains(typeOfT))
                    formatter = MessagePack.Resolvers.PrimitiveObjectResolver.Instance.GetFormatter<T>();

                Formatter = formatter;
            }
        }

        private IMessagePackFormatter<T> TryGetTAVCCustomFormatter<T>()
        {
            var typeOfT = typeof(T);

            if (roFormatterDictionary.TryGetValue(typeOfT, out IMessagePackFormatter tryGetFormatter))
                return tryGetFormatter as IMessagePackFormatter<T>;     // use this path even if the formatter is null

            bool hasDataContractAttribute = (!typeOfT.GetCustomAttributes(typeof(DataContractAttribute), false).IsNullOrEmpty());
            bool hasCollectionDataContractAttribute = (!typeOfT.GetCustomAttributes(typeof(CollectionDataContractAttribute), false).IsNullOrEmpty());
            bool hasSerializableAttribute = (!typeOfT.GetCustomAttributes(typeof(SerializableAttribute), false).IsNullOrEmpty());
            bool isPrimitiveType = LocalConstants.primitiveTypeSet.Contains(typeOfT);

            if (hasDataContractAttribute || hasCollectionDataContractAttribute || hasSerializableAttribute || isPrimitiveType)
            {
                var typeSerializerItem = (!isPrimitiveType) ? CustomSerialization.GetCustomTypeSerializerItemFor(typeOfT, factoryName: DefaultFactoryName) : null;

                if (typeSerializerItem != null || isPrimitiveType)
                {
                    var formatter = (typeSerializerItem != null) ? new CustomTypeSerializerFormatter<T>(typeSerializerItem) : null;

                    lock (formatterDictionaryMutex)
                    {
                        formatterDictionary[typeOfT] = formatter;
                        roFormatterDictionary = formatterDictionary.ConvertToReadOnly();

                        return formatter;
                    }
                }
            }

            return null;
        }

        private IDictionary<Type, IMessagePackFormatter> roFormatterDictionary = ReadOnlyIDictionary<Type, IMessagePackFormatter>.Empty;
        private object formatterDictionaryMutex = new object();
        private Dictionary<Type, IMessagePackFormatter> formatterDictionary = new Dictionary<Type, IMessagePackFormatter>();

        protected static class LocalConstants
        {
            public static readonly ReadOnlyHashSet<Type> primitiveTypeSet = new ReadOnlyHashSet<Type>(new HashSet<Type>()
                {
                    typeof(bool),
                    typeof(char),
                    typeof(sbyte),
                    typeof(byte),
                    typeof(short),
                    typeof(ushort),
                    typeof(int),
                    typeof(uint),
                    typeof(long),
                    typeof(ulong),
                    typeof(float),
                    typeof(double),
                    typeof(DateTime),
                    typeof(string),
                    typeof(byte[]),
                });
        }
    }

    /// <summary>
    /// MessagePack Extension Types used here.  [sbyte]
    /// <para/>-128..-1 are reserved for current and future protocol use.
    /// <para/>0..127 are available for application specific use (see restrictions below in remarks section)
    /// <para/>CSTBase (0), ArrayOfCSTBase (40), LogGate (120), VCE (121)
    /// <para/>Values CSTBase to CSTBase + 29 are reserved for mapped versions of the ContainerStorageType (current max value is 21).  Only the CST values that are not known by context are used here.
    /// <para/>Values ArrayOfCSTBase to ArrayOfCSTBase + 29 are reserved for arrays of mapped versions of the ContainerStorageType (current max value is 21).  Only the CST values that are not known by context are used here.
    /// </summary>
    /// <remarks>
    ///-1	DateTime MessagePack-spec reserved for timestamp
    ///30	Vector2[]	for Unity, UnsafeBlitFormatter
    ///31	Vector3[]	for Unity, UnsafeBlitFormatter
    ///32	Vector4[]	for Unity, UnsafeBlitFormatter
    ///33	Quaternion[]	for Unity, UnsafeBlitFormatter
    ///34	Color[]	for Unity, UnsafeBlitFormatter
    ///35	Bounds[]	for Unity, UnsafeBlitFormatter
    ///36	Rect[]	for Unity, UnsafeBlitFormatter
    ///37	Int[]	for Unity, UnsafeBlitFormatter
    ///38	Float[]	for Unity, UnsafeBlitFormatter
    ///39	Double[]	for Unity, UnsafeBlitFormatter
    ///98	All MessagePackCompression.Lz4BlockArray
    ///99	All MessagePackCompression.Lz4Block
    ///100	object TypelessFormatter
    /// </remarks>
    public enum MPExtensionType : sbyte
    {
        // known existing 
        DateTime = MessagePack.ReservedMessagePackExtensionTypeCode.DateTime,       // -1
        CSTBase = 0,
        CSTLast = 29,
        // 30..39 are reserved - see remarks section above
        ArrayOfCSTBase = 50,
        ArrayOfCSTLast = 79,
        // 98, 99, 100 are reserved - see remarks section above
        LogGate = 120,
        VCE = 121,
    }

    /// <summary>
    /// The following enum is a set of the well known bytes codes (and some ranges)
    /// </summary>
    public enum MPHeaderByteCode : byte
    {
        PositiveFixInt_First = MessagePackCode.MinFixInt,
        PositiveFixInt_Last = MessagePackCode.MaxFixInt,
        FixMap_First = MessagePackCode.MinFixMap,
        FixMap0 = FixMap_First,
        FixMap1,
        FixMap2,
        FixMap3,
        FixMap4,
        FixMap5,
        FixMap6,
        FixMap7,
        FixMap8,
        FixMap9,
        FixMap10,
        FixMap11,
        FixMap12,
        FixMap13,
        FixMap14,
        FixMap15,
        FixMap_Last = MessagePackCode.MaxFixMap,
        FixArray_First = MessagePackCode.MinFixArray,
        FixArray0 = FixArray_First,
        FixArray1,
        FixArray2,
        FixArray3,
        FixArray4,
        FixArray5,
        FixArray6,
        FixArray7,
        FixArray8,
        FixArray9,
        FixArray10,
        FixArray11,
        FixArray12,
        FixArray13,
        FixArray14,
        FixArray15,
        FixArray_Last = MessagePackCode.MaxFixArray,
        FixStr_First = MessagePackCode.MinFixStr,
        FixStr0 = MessagePackCode.MinFixStr,
        FixStr1,
        FixStr2,
        FixStr3,
        FixStr4,
        FixStr5,
        FixStr6,
        FixStr7,
        FixStr8,
        FixStr9,
        FixStr10,
        FixStr11,
        FixStr12,
        FixStr13,
        FixStr14,
        FixStr15,
        FixStr16,
        FixStr17,
        FixStr18,
        FixStr19,
        FixStr20,
        FixStr21,
        FixStr22,
        FixStr23,
        FixStr24,
        FixStr25,
        FixStr26,
        FixStr27,
        FixStr28,
        FixStr29,
        FixStr30,
        FixStr31,
        FixStr_Last = MessagePackCode.MaxFixStr,
        Nil = MessagePackCode.Nil,
        NeverUsed = MessagePackCode.NeverUsed,
        False = MessagePackCode.False,
        True = MessagePackCode.True,
        Bin8 = MessagePackCode.Bin8,
        Bin16 = MessagePackCode.Bin16,
        Bin32 = MessagePackCode.Bin32,
        Ext8 = MessagePackCode.Ext8,
        Ext16 = MessagePackCode.Ext16,
        Ext32 = MessagePackCode.Ext32,
        Float32 = MessagePackCode.Float32,
        Float64 = MessagePackCode.Float64,
        UInt8 = MessagePackCode.UInt8,
        UInt16 = MessagePackCode.UInt16,
        UInt32 = MessagePackCode.UInt32,
        UInt64 = MessagePackCode.UInt64,
        Int8 = MessagePackCode.Int8,
        Int16 = MessagePackCode.Int16,
        Int32 = MessagePackCode.Int32,
        Int64 = MessagePackCode.Int64,
        FixExt1 = MessagePackCode.FixExt1,
        FixExt2 = MessagePackCode.FixExt2,
        FixExt4 = MessagePackCode.FixExt4,
        FixExt8 = MessagePackCode.FixExt8,
        FixExt16 = MessagePackCode.FixExt16,
        Str8 = MessagePackCode.Str8,
        Str16 = MessagePackCode.Str16,
        Str32 = MessagePackCode.Str32,
        Array16 = MessagePackCode.Array16,
        Array32 = MessagePackCode.Array32,
        Map16 = MessagePackCode.Map16,
        Map32 = MessagePackCode.Map32,
        NegFixInt_First = MessagePackCode.MinNegativeFixInt,
        NegFixInt_Last = MessagePackCode.MaxNegativeFixInt,
    }

    #region MessagePackFileRecordReaderSettings, MessagePackFileRecordReader

    /// <summary>
    /// Settings used when opening a file using a MessagePackFileRecordReader.
    /// </summary>
    public class MessagePackFileRecordReaderSettings : ICopyable<MessagePackFileRecordReaderSettings>
    {
        public ArrayPool<byte> BufferArrayPool { get; set; } = ArrayPool<byte>.Shared;
        public FileOptions FileOptions { get; set; } = (FileOptions.SequentialScan);        // nominally optimal selection for this reader
        public int InitialBufferSize { get; set; } = 65536;
        public int? InitialReadSize { get; set; } = null;   // use InitialBufferSize when null.
        public bool ReadInitialRecord { get; set; } = true;

        /// <summary>When true, the Read method will catch and handle the System.IO.EndOfStreamException as indicating end of file (to support read while writing with partially written contents).</summary>
        public bool TreatEndOfStreamExceptionAsEndOfFile { get; set; } = true;

        /// <summary>When true, the Read method will catch and handle specific errors as indicating end of file (to support read while writing with partially written contents).</summary>
        public bool TreatExpectedDecompressionErrorsAsEndOfFile { get; set; } = true;

        public MessagePackFileRecordReaderSettings MakeCopyOfThis(bool deepCopy = true)
        {
            return (MessagePackFileRecordReaderSettings) MemberwiseClone();
        }
    }

    /// <summary>
    /// Helper class used to open a file that contains a sequence of message pack records, either raw or lz4 compressed, 
    /// and to give the client access to read and traverse/decode each records contents using a relatively efficient reading engine.
    /// </summary>
    public class MessagePackFileRecordReader : IDisposable
    {
        private static readonly MessagePackFileRecordReaderSettings DefaultSettings = new MessagePackFileRecordReaderSettings()
        {
            BufferArrayPool = ArrayPool<byte>.Shared,
        };

        /// <summary>
        /// Opend and optionally read an initial record.
        /// </summary>
        public MessagePackFileRecordReader Open(string filePath, MessagePackFileRecordReaderSettings settingsIn = default)
        {
            ClearCounters();

            ReleaseFileStreams();

            Settings = settingsIn?.MakeCopyOfThis() ?? DefaultSettings;

            var compressorSelect = filePath.GetCompressorSelectFromFilePath();
            Stream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, Settings.FileOptions);
            _Counters.FileLength = (ulong) fileStream.Length;

            // Note: we use the compressor even when reading non-compressed files so we can make use of TreatYYYAsEndOfFile flags.
            var compressorStream = fileStream.CreateDecompressor(compressorSelect);

            compressorStream.TreatEndOfStreamExceptionAsEndOfFile = Settings.TreatEndOfStreamExceptionAsEndOfFile;
            compressorStream.TreatExpectedDecompressionErrorsAsEndOfFile = Settings.TreatExpectedDecompressionErrorsAsEndOfFile;

            useStream = compressorStream;

            EndReached = (_Counters.FileLength <= 0);

            ResetBufferGetAndPutIndexes();
            ResizeBuffer(Math.Max(Settings.InitialBufferSize, 512));

            int initialReadSize = Settings.InitialReadSize ?? Settings.InitialBufferSize;
            if (!EndReached && initialReadSize > 0)
            {
                ReadMore(initialReadSize);
                UpdateReadOnlySequenceIfNeeded();
            }

            if (Settings.ReadInitialRecord)
                AttemptToPopulateNextRecord();

            return this;
        }

        private MessagePackFileRecordReaderSettings Settings { get; set; } = DefaultSettings;

        /// <summary>A set of values that this class maintains during reading.</summary>
        public struct CounterValues
        {
            /// <summary>Gives the length of the file that was opened: either the uncompressed length of the input file, or the size of the compressed file.</summary>
            public ulong FileLength { get; set; }
            /// <summary>Gives the total number of bytes read (and decompressed as relevant)</summary>
            public ulong TotalBytesRead { get; set; }
            /// <summary>Gives the total number of bytes that the client has advanced over</summary>
            public ulong TotalBytesProcessed { get; set; }
            /// <summary>Gives the total number of file reads that have been done.</summary>
            public uint NumberOfReads { get; set; }
            /// <summary>Gives the total number of times that the buffer has been realligned by shifting.</summary>
            public uint NumberOfBufferShifts { get; set; }
            /// <summary>Gives the total number of bytes that have been copied during each buffer reallignment iteration.</summary>
            public uint NumberOfBytesShifted { get; set; }
        }

        /// <summary>Gives the client access to the current set of counter values.</summary>
        public CounterValues Counters => _Counters;
        private CounterValues _Counters;

        /// <summary>Returns true once the entire input file has been read.</summary>
        public bool EndReached { get; private set; }

        private void ClearCounters()
        {
            _Counters = default;
            EndReached = false;
        }

        public void Dispose()
        {
            ReleaseFileStreams();

            if (buffer != null)
            {
                Settings.BufferArrayPool?.Return(buffer);
                buffer = null;
            }

            ResetBufferGetAndPutIndexes();
            ClearCounters();
        }

        private Stream useStream;

        private void ReleaseFileStreams()
        {
            useStream?.Close();

            Fcns.DisposeOfObject(ref useStream);
        }

        /// <summary>
        /// Attempts to read enough bytes so that the buffer contains at least the given number of bytes.
        /// Returns true if this could be performed, or false if not, usually because the end of the file was reached.
        /// </summary>
        public bool AttemptToReadMoreBytes(uint requestedMinimumCount, bool allowUpdateReadOnlySequenceIfNeeded = true)
        {
            for (; ; )
            {
                if (CurrentBufferCount >= requestedMinimumCount)
                {
                    if (allowUpdateReadOnlySequenceIfNeeded)
                        UpdateReadOnlySequenceIfNeeded();
                    return true;
                }
                else if (EndReached)
                {
                    if (allowUpdateReadOnlySequenceIfNeeded)
                        UpdateReadOnlySequenceIfNeeded();
                    return false;
                }

                if (CurrentAvailableBufferSpace <= 0)
                {
                    if (getIndex > 0)
                        ReallignBuffer();
                    else
                        ResizeBuffer(bufferLength << 1);    // make buffer twice as big is it currently is
                }

                ReadMore(nominalReadPostLength);
            }
        }

        /// <summary>
        /// Repeatedly attempts to read more data (aka at least 1 more byte than the buffer already contains) 
        /// until no more bytes can be read or the next full record is found (aka NextRecordLength is non-zero).
        /// </summary>
        public bool AttemptToPopulateNextRecord()
        {
            for (; ; )
            {
                if (NextRecordLength > 0)
                    return true;
                else if (!AttemptToReadMoreBytes((uint) CurrentBufferCount + 1))
                    return false;
            }
        }

        /// <summary>
        /// If <paramref name="autoReadIfNeeded"/> is true and NextRecordLength is zero then this method calls AttemptToPopulateNextRecord.
        /// Then if there is a complete record available, this method sets up the given <paramref name="mpReader"/> instance to read from the buffer starting at that location and returns true.
        /// Otherwise this method set the given <paramref name="mpReader"/> to the default value and returns false.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetupMPReaderForNextRecord(ref MessagePackReader mpReader)
        {
            bool haveRecord = (NextRecordLength > 0 || AttemptToPopulateNextRecord());

            mpReader = haveRecord ? new MessagePackReader(in readOnlySequence) : default;

            return haveRecord;
        }

        /// <summary>Returns the length (in bytes) of the next full message pack record that is currently in the buffer or 0 if the buffer is empty or only contains a partial record.</summary>
        public uint NextRecordLength { get; private set; }

        /// <summary>When the buffer contains a complete record, this property gives the MPHeaderByteCode of the first (aka top level) item in the record.</summary>
        public MPHeaderByteCode NextRecordHeaderByteCode { get; private set; }

        /// <summary>
        /// Requests that the buffer advance past the current record by advancing over the number of bytes given by the current NextRecordLength property.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">This exception is thrown if the NextRecordLength is zero when this method is called.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvancePastCurrentRecord()
        {
            var nextRecordLength = NextRecordLength;
            if (nextRecordLength > 0)
                Advance((uint) nextRecordLength);
            else
                new System.InvalidOperationException($"{Fcns.CurrentMethodName} failed: there is no current record").Throw();
        }

        /// <summary>
        /// Requests that the reader advance over the given number of bytes, including advancing over bytes that have not been read yet.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(uint numBytesToAdvance)
        {
            for (; ; )
            {
                var currentBufferCount = CurrentBufferCount;

                if (numBytesToAdvance < currentBufferCount)
                {
                    getIndex += (int) numBytesToAdvance;
                    _Counters.TotalBytesProcessed += numBytesToAdvance;
                    break;
                }
                else if (numBytesToAdvance >= currentBufferCount && currentBufferCount > 0)
                {
                    getIndex = 0;
                    putIndex = 0;
                    numBytesToAdvance -= (uint) currentBufferCount;
                    _Counters.TotalBytesProcessed += (uint) currentBufferCount;

                    if (numBytesToAdvance == 0)
                        break;
                }
                else if (EndReached)
                {
                    break;
                }

                AttemptToReadMoreBytes(numBytesToAdvance, allowUpdateReadOnlySequenceIfNeeded: false);
            }

            UpdateReadOnlySequenceIfNeeded(forceUpdate: true);
        }

        bool updateReadOnlySequenceNeeded;
        ReadOnlySequence<byte> readOnlySequence;
        ReadOnlyMemory<byte> readOnlyMemory;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void UpdateReadOnlySequenceIfNeeded(bool forceUpdate = false)
        {
            if (forceUpdate || updateReadOnlySequenceNeeded)
            {
                updateReadOnlySequenceNeeded = false;

                int currentBufferCount = CurrentBufferCount;

                readOnlySequence = new ReadOnlySequence<byte>(buffer, getIndex, currentBufferCount);
                readOnlyMemory = readOnlySequence.First;

                if (currentBufferCount > 0)
                {
                    NextRecordHeaderByteCode = unchecked((MPHeaderByteCode) buffer[getIndex]);

                    int recordScanIndex = 1 + getIndex;

                    if (buffer.FindEndIndexForMPRecordBody(NextRecordHeaderByteCode, ref recordScanIndex, putIndex))
                        NextRecordLength = (uint) (recordScanIndex - getIndex);
                    else
                        NextRecordLength = 0;
                }
                else
                {
                    NextRecordHeaderByteCode = MPHeaderByteCode.NeverUsed;
                    NextRecordLength = 0;
                }
            }
        }

        private byte[] buffer;
        private int bufferLength;
        private int getIndex, putIndex;
        private int nominalReadPostLength;

        /// <summary>Returns the currently allocated buffer size.</summary>
        public int BufferSize => bufferLength;

        /// <summary>Returns the number of bytes that are currently in the buffer.</summary>
        public int CurrentBufferCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (putIndex - getIndex); }
        }

        /// <summary>Returns the currently available space from the end of the current buffer contents to the end of the buffer.</summary>
        public int CurrentAvailableBufferSpace
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (bufferLength - putIndex); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetBufferGetAndPutIndexes()
        {
            getIndex = 0;
            putIndex = 0;

            updateReadOnlySequenceNeeded = true;
        }

        private void ResizeBuffer(int requestedMinSize)
        {
            if (requestedMinSize <= bufferLength)
                return;

            var arrayPool = Settings.BufferArrayPool;
            var entryBuffer = buffer;
            var entryCount = CurrentBufferCount;
            var entryGetIndex = getIndex;

            buffer = arrayPool?.Rent(requestedMinSize) ?? new byte[requestedMinSize];
            getIndex = 0;
            putIndex = entryCount;
            bufferLength = buffer.Length;
            nominalReadPostLength = Math.Max(4096, (bufferLength >> 1) & ~4095);       // half of bufferLength rounded down to next multiple of 4096 but not below 4096

            if (entryBuffer != null)
                Buffer.BlockCopy(entryBuffer, entryGetIndex, buffer, 0, entryCount);

            if (entryBuffer != null)
                arrayPool?.Return(entryBuffer);

            updateReadOnlySequenceNeeded = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReallignBuffer()
        {
            var currentBufferCount = CurrentBufferCount;
            if (currentBufferCount > 0)
            {
                Buffer.BlockCopy(buffer, getIndex, buffer, 0, currentBufferCount);

                putIndex -= getIndex;
                getIndex = 0;

                updateReadOnlySequenceNeeded = true;

                _Counters.NumberOfBytesShifted += (uint) currentBufferCount;
            }
            else
            {
                ResetBufferGetAndPutIndexes();
            }

            _Counters.NumberOfBufferShifts += 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NoteBytesRead(int readCount)
        {
            if (readCount > 0)
            {
                putIndex += readCount;
                _Counters.TotalBytesRead += (ulong) readCount;
                updateReadOnlySequenceNeeded = true;
            }
            else
            {
                EndReached = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadMore(int requestedBytesToRead)
        {
            if (EndReached)
                new System.InvalidOperationException("Attempt to read past end of file").Throw();

            int readRequestCount = Math.Min(CurrentAvailableBufferSpace, requestedBytesToRead);

            if (!EndReached && readRequestCount > 0)
            {
                NoteBytesRead(useStream.Read(buffer, putIndex, readRequestCount));
                _Counters.NumberOfReads += 1;
            }

            updateReadOnlySequenceNeeded = true;
        }
    }

    /// <summary>
    /// Extension Methods
    /// </summary>
    public static partial class ExtensionMethods
    {
        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MPHeaderByteCode GetHeaderByteCode(ref this ReadOnlyMemory<byte> rom)
        {
            var rawHeaderByteCode = rom.Span[0];
            return unchecked((MPHeaderByteCode)(rom.Span[0]));
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MessagePackType Convert(this MPHeaderByteCode code)
        {
            return MPDecodeHelper.DecodedHeaderInfoMap[(int)code].MessagePackType;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int GetMPRecordLength(this byte[] byteArray, int scanIndex, int contentEndIndex)
        {
            fixed (byte* byteArrayP = byteArray)
            {
                return GetMPRecordLength(byteArrayP, scanIndex, contentEndIndex);
            }
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int GetMPRecordLength(byte* byteArrayP, int scanIndex, int contentEndIndex)
        {
            if (scanIndex >= contentEndIndex)
                return 0;

            var entryScanIndex = scanIndex;

            var rawHeaderByteCode = byteArrayP[scanIndex++];
            var mpHeaderByteCode = unchecked((MPHeaderByteCode)rawHeaderByteCode);

            if (!FindEndIndexForMPRecordBody(byteArrayP, mpHeaderByteCode, ref scanIndex, contentEndIndex))
                return 0;

            return scanIndex - entryScanIndex;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool FindEndIndexForMPRecordBody(this byte[] byteArray, MPHeaderByteCode mpHeaderByteCode, ref int scanIndex, int contentEndIndex)
        {
            fixed (byte* byteArrayP = byteArray)
            {
                return FindEndIndexForMPRecordBody(byteArrayP, mpHeaderByteCode, ref scanIndex, contentEndIndex);
            }
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe bool FindEndIndexForMPRecordBody(byte * byteArrayP, MPHeaderByteCode mpHeaderByteCode, ref int scanIndex, int contentEndIndex)
        {
            var fixedItemBodyLength = MPDecodeHelper.FixedItemBodyLengthMap[(int)mpHeaderByteCode];

            if (fixedItemBodyLength >= 0)
            {
                scanIndex += fixedItemBodyLength;

                return (scanIndex <= contentEndIndex);
            }

            return FindEndIndexForMPRecordNonFixedBody(byteArrayP, mpHeaderByteCode, ref scanIndex, contentEndIndex);
        }

        /// <summary></summary>
        internal static unsafe bool FindEndIndexForMPRecordNonFixedBody(byte * byteArrayP, MPHeaderByteCode mpHeaderByteCode, ref int scanIndex, int contentEndIndex)
        {
            var headerInfo = MPDecodeHelper.DecodedHeaderInfoMap[(int)mpHeaderByteCode];

            int itemCount = headerInfo.FixedItemCount;
            var itemCountLen = headerInfo.ItemCountLength;

            int nextScanIndex = scanIndex;

            if (itemCount < 0)
            {
                if (nextScanIndex + itemCountLen > contentEndIndex)
                    return false;

                switch (itemCountLen)
                {
                    case 1:
                        itemCount = byteArrayP[nextScanIndex++];
                        break;

                    case 2:
                        {
                            var b1 = byteArrayP[nextScanIndex++];
                            var b0 = byteArrayP[nextScanIndex++];
                            itemCount = (int)(((uint)b1 << 8) + b0);
                        }
                        break;

                    case 4:
                        {
                            var b3 = byteArrayP[nextScanIndex++];
                            var b2 = byteArrayP[nextScanIndex++];
                            var b1 = byteArrayP[nextScanIndex++];
                            var b0 = byteArrayP[nextScanIndex++];
                            itemCount = (int)(((uint)b3 << 24) + ((uint)b2 << 16) + ((uint)b1 << 8) + b0);
                        }
                        break;

                    default:
                        mpHeaderByteCode.ThrowExceptionForInvalidCode("GetMPRecordLength (InvalidItemCountLen)");
                        break;
                }
            }

            switch (headerInfo.MessagePackType)
            {
                case MessagePackType.String:
                    nextScanIndex += itemCount; // skip over contents bytes
                    break;

                case MessagePackType.Binary:
                    nextScanIndex += itemCount; // skip over contents bytes
                    break;

                case MessagePackType.Extension:
                    nextScanIndex += 1 + itemCount; // skip over type byte and content bytes
                    break;

                case MessagePackType.Array:
                    for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
                    {
                        int itemLen = GetMPRecordLength(byteArrayP, nextScanIndex, contentEndIndex);
                        if (itemLen <= 0)
                            return false;

                        nextScanIndex += itemLen;
                    }
                    break;

                case MessagePackType.Map:
                    for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
                    {
                        int keyLen = GetMPRecordLength(byteArrayP, nextScanIndex, contentEndIndex);
                        if (keyLen <= 0)
                            return false;
                        else
                        {
                            nextScanIndex += keyLen;
                            int itemLen = GetMPRecordLength(byteArrayP, nextScanIndex, contentEndIndex);
                            if (itemLen <= 0)
                                return false;

                            nextScanIndex += itemLen;
                        }
                    }
                    break;

                default:
                    mpHeaderByteCode.ThrowExceptionForInvalidCode($"GetMPRecordLength (Invalid MPTypeCode {headerInfo.MessagePackType})");
                    return false;
            }

            scanIndex = nextScanIndex;

            return (scanIndex <= contentEndIndex);
        }

        public static void Traverse(this ref MessagePackReader mpReader)
        {
            switch (mpReader.NextMessagePackType)
            {
                case MessagePackType.Array:
                    {
                        int len = mpReader.ReadArrayHeader();
                        for (int index = 0; index < len; index++)
                            mpReader.Traverse();
                    }
                    break;
                case MessagePackType.Binary:
                    mpReader.ReadBytes();
                    break;
                case MessagePackType.Boolean:
                    mpReader.ReadBoolean();
                    break;
                case MessagePackType.Extension:
                    mpReader.ReadExtensionFormat();
                    break;
                case MessagePackType.Float:
                    mpReader.ReadDouble();
                    break;
                case MessagePackType.Integer:
                    mpReader.ReadInt64();
                    break;
                case MessagePackType.Map:
                    {
                        int len = mpReader.ReadMapHeader();
                        for (int index = 0; index < len; index++)
                        {
                            mpReader.Traverse();
                            mpReader.Traverse();
                        }
                    }
                    break;
                case MessagePackType.Nil:
                    mpReader.ReadNil();
                    break;
                case MessagePackType.String:
                    mpReader.ReadString();
                    break;
                case MessagePackType.Unknown:
                default:
                    throw new System.InvalidOperationException($"Encountered unexpected MP type {mpReader.NextMessagePackType}");
            }
        }

        /// <summary>Throws an exception to indicate that the given byte <paramref name="code"/> is not valid or usable MPHeaderByteCode</summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        public static System.Exception ThrowExceptionForInvalidCode(this MPHeaderByteCode code, string mesg = default)
        {
            mesg = (mesg.IsNeitherNullNorEmpty() ? $", {mesg}" : "");
            throw new System.InvalidOperationException($"Encountered unrecognized MP Header Byte Code [${(byte)code:x2}, {code}, {code.Convert()}{mesg}]");
        }

        /// <summary>Throws an exception to indicate that the given byte <paramref name="code"/> is not valid or usable MPHeaderByteCode</summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        public static System.Exception ThrowExceptionForInvalidCode(byte code, string mesg = default)
        {
            return unchecked((MPHeaderByteCode)code).ThrowExceptionForInvalidCode(mesg);
        }
    }

    /// <summary>
    /// Common information for a given MPHeaderCode byte value, including its fixed item count, item count length, corresponding MessagePackType and its fixed item body length
    /// </summary>
    public struct MPDecodedHeaderInfo
    {
        public sbyte FixedItemCount;
        public sbyte ItemCountLength;
        public MessagePackType MessagePackType;
        public sbyte FixedItemBodyLength;

        public override string ToString()
        {
            var bodyLenStr = (FixedItemBodyLength >= 0) ? $" bodyLen:{FixedItemBodyLength}" : "";
            var itemCountStr = (FixedItemCount >= 0) ? $" itemCount:{FixedItemCount}" : "";
            var itemCountLenStr = (ItemCountLength > 0) ? $" itemCountLen:{ItemCountLength}" : "";

            return $"{MessagePackType}{bodyLenStr}{itemCountStr}{itemCountLenStr}";
        }
    };

    /// <summary>
    /// Static helper class used to construct Mapping arrays used to produce useful information about specific MPHeaderByteCode values.
    /// <para/>This information includes the Item's fixed body length for well know low level types, or its fixed item count for fixed length collection types, the corresponding MessagePackType, and the length of the item count field in bytes for variable length collection types.
    /// </summary>
    internal static class MPDecodeHelper
    {
        /// <summary>Map of MPHeaderByteCode byte value to the fixed item body length for that code, or -1 if the code gives a variable length body or is a fixed length collection type.</summary>
        public static readonly sbyte[] FixedItemBodyLengthMap;
        /// <summary>Map of MPHeaderByteCode byte value to the set of decoded values that are used while traversing an MP record's node tree.</summary>
        public static readonly MPDecodedHeaderInfo[] DecodedHeaderInfoMap;

        /// <summary>static class constructor - used to populate the FixedItemBodyLengthMap and the DecodedHeaderInfoMap arrays.</summary>
        static MPDecodeHelper()
        {
            FixedItemBodyLengthMap = new sbyte[256];
            DecodedHeaderInfoMap = new MPDecodedHeaderInfo[256];

            foreach (var index in Enumerable.Range(0, 256))
            {
                var bc = unchecked((MPHeaderByteCode)index);
                var mpt = MessagePackType.Unknown;
                sbyte fixedItemBodyLength = -1;
                sbyte fixedItemCount = -1;
                sbyte itemCountLength = 0;

                switch (bc)
                {
                    case MPHeaderByteCode.Array16: mpt = MessagePackType.Array; itemCountLength = 2; break;
                    case MPHeaderByteCode.Array32: mpt = MessagePackType.Array; itemCountLength = 4; break;
                    case MPHeaderByteCode.Bin8: mpt = MessagePackType.Binary; itemCountLength = 1; break;
                    case MPHeaderByteCode.Bin16: mpt = MessagePackType.Binary; itemCountLength = 2; break;
                    case MPHeaderByteCode.Bin32: mpt = MessagePackType.Binary; itemCountLength = 4; break;
                    case MPHeaderByteCode.True: mpt = MessagePackType.Boolean; fixedItemBodyLength = 0; break;
                    case MPHeaderByteCode.False: mpt = MessagePackType.Boolean; fixedItemBodyLength = 0; break;
                    case MPHeaderByteCode.FixExt1: mpt = MessagePackType.Extension; fixedItemBodyLength = 1; break;
                    case MPHeaderByteCode.FixExt2: mpt = MessagePackType.Extension; fixedItemBodyLength = 2; break;
                    case MPHeaderByteCode.FixExt4: mpt = MessagePackType.Extension; fixedItemBodyLength = 4; break;
                    case MPHeaderByteCode.FixExt8: mpt = MessagePackType.Extension; fixedItemBodyLength = 8; break;
                    case MPHeaderByteCode.FixExt16: mpt = MessagePackType.Extension; fixedItemBodyLength = 16; break;
                    case MPHeaderByteCode.Ext8: mpt = MessagePackType.Extension; itemCountLength = 1; break;
                    case MPHeaderByteCode.Ext16: mpt = MessagePackType.Extension; itemCountLength = 2; break;
                    case MPHeaderByteCode.Ext32: mpt = MessagePackType.Extension; itemCountLength = 4; break;
                    case MPHeaderByteCode.Map16: mpt = MessagePackType.Map; itemCountLength = 2; break;
                    case MPHeaderByteCode.Map32: mpt = MessagePackType.Map; itemCountLength = 4; break;
                    case MPHeaderByteCode.Str8: mpt = MessagePackType.String; itemCountLength = 1; break;
                    case MPHeaderByteCode.Str16: mpt = MessagePackType.String; itemCountLength = 2; break;
                    case MPHeaderByteCode.Str32: mpt = MessagePackType.String; itemCountLength = 4; break;
                    case MPHeaderByteCode.Int8: mpt = MessagePackType.Integer; fixedItemBodyLength = 1; break;
                    case MPHeaderByteCode.Int16: mpt = MessagePackType.Integer; fixedItemBodyLength = 2; break;
                    case MPHeaderByteCode.Int32: mpt = MessagePackType.Integer; fixedItemBodyLength = 4; break;
                    case MPHeaderByteCode.Int64: mpt = MessagePackType.Integer; fixedItemBodyLength = 8; break;
                    case MPHeaderByteCode.UInt8: mpt = MessagePackType.Integer; fixedItemBodyLength = 1; break;
                    case MPHeaderByteCode.UInt16: mpt = MessagePackType.Integer; fixedItemBodyLength = 2; break;
                    case MPHeaderByteCode.UInt32: mpt = MessagePackType.Integer; fixedItemBodyLength = 4; break;
                    case MPHeaderByteCode.UInt64: mpt = MessagePackType.Integer; fixedItemBodyLength = 8; break;
                    case MPHeaderByteCode.Float32: mpt = MessagePackType.Float; fixedItemBodyLength = 4; break;
                    case MPHeaderByteCode.Float64: mpt = MessagePackType.Float; fixedItemBodyLength = 8; break;
                    case MPHeaderByteCode.Nil: mpt = MessagePackType.Nil; fixedItemBodyLength = 0; break;

                    default:
                        if (index.IsInRange((int)MPHeaderByteCode.FixArray0, (int)MPHeaderByteCode.FixArray15))
                        {
                            mpt = MessagePackType.Array;
                            fixedItemCount = unchecked((sbyte)(index - (int)MPHeaderByteCode.FixArray0));
                        }
                        else if (index.IsInRange((int)MPHeaderByteCode.FixMap0, (int)MPHeaderByteCode.FixMap15))
                        {
                            mpt = MessagePackType.Map;
                            fixedItemCount = unchecked((sbyte)(index - (int)MPHeaderByteCode.FixMap0));
                        }
                        else if (index.IsInRange((int)MPHeaderByteCode.PositiveFixInt_First, (int)MPHeaderByteCode.PositiveFixInt_Last))
                        {
                            mpt = MessagePackType.Integer;
                            fixedItemBodyLength = 0;
                        }
                        else if (index.IsInRange((int)MPHeaderByteCode.NegFixInt_First, (int)MPHeaderByteCode.NegFixInt_Last))
                        {
                            mpt = MessagePackType.Integer;
                            fixedItemBodyLength = 0;
                        }
                        else if (index.IsInRange((int)MPHeaderByteCode.FixStr0, (int)MPHeaderByteCode.FixStr31))
                        {
                            mpt = MessagePackType.String;
                            fixedItemBodyLength = unchecked((sbyte)(index - (int)MPHeaderByteCode.FixStr0));
                        }
                        break;
                }

                FixedItemBodyLengthMap[index] = fixedItemBodyLength;
                DecodedHeaderInfoMap[index] = new MPDecodedHeaderInfo() { MessagePackType = mpt, FixedItemBodyLength = fixedItemBodyLength, FixedItemCount = fixedItemCount, ItemCountLength = itemCountLength };
            }
        }
    }

    #endregion
}