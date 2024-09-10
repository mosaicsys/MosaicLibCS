//-------------------------------------------------------------------
/*! @file MDRF2PortRecording.cs
 *  @brief 
 *
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2024 Mosaic Systems Inc.
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
using Mosaic.ToolsLib.MDRF2.Common;
using Mosaic.ToolsLib.MDRF2.Writer;
using MosaicLib.Modular.Common;
using MosaicLib.Semi.E005;
using MosaicLib.Semi.E005.Manager;
using MosaicLib.Semi.E005.Port;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using System.Collections.Generic;
using static Mosaic.ToolsLib.MDRF2.Common.TypeNameHandlers;

namespace Mosaic.ToolsLib.Semi.E005.PortRecording
{
    /// <summary>
    /// Provides the configuration values that are used to setup a <see cref="MDRF2PortRecording"/> instance.
    /// </summary>
    public class MDRF2PortRecordingConfig : ICopyable<MDRF2PortRecordingConfig>
    {
        /// <summary>Provides the <see cref="IMDRF2Writer"/> instance that objects will be recorded to.</summary>
        public IMDRF2Writer MDRF2Writer { get; set; }

        /// <inheritdoc/>
        public MDRF2PortRecordingConfig MakeCopyOfThis(bool deepCopy = true)
        {
            return (MDRF2PortRecordingConfig) MemberwiseClone();
        }
    }

    /// <summary>
    /// This class implements the <see cref="IPortRecording"/> interface and records the given values to a provided <see cref="IMDRF2Writer"/> instances.
    /// </summary>
    public class MDRF2PortRecording : IPortRecording
    {
        /*

                    NoteE005HeaderReceivedKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteE005HeaderReceived"),
                    NoteE005MessageReceivedKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteE005MessageReceived"),
                    NoteE005MessageSentKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteE005MessageSent"),
                    NoteInfoMesgKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteInfoMesg"),
                    NoteIssueMesgKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteIssueMesg"),
                    NoteSendingE005HeaderKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteSendingE005Header"),
                    NoteSendingE005MessageKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteSendingE005Message"),
                    NoteStateChangeKeyID = MDRF2Writer.RegisterAndGetKeyID($"PortRecording.{name}.NoteStateChange"),
         
         */

        public MDRF2PortRecording(MDRF2PortRecordingConfig config) 
        {
            Config = config.MakeCopyOfThis();
            MDRF2Writer = Config.MDRF2Writer;
        }

        protected MDRF2PortRecordingConfig Config { get; }

        protected IMDRF2Writer MDRF2Writer { get; }

        /// <inheritdoc/>
        public void NoteInfoMesg(IPort port, string infoMesg)
        {
            MDRF2Writer?.RecordObject(infoMesg, keyID: GetKeyIDSetForPort(port).NoteInfoMesgKeyID);
        }

        /// <inheritdoc/>
        public void NoteIssueMesg(IPort port, string issueMesg)
        {
            MDRF2Writer?.RecordObject(issueMesg, keyID: GetKeyIDSetForPort(port).NoteIssueMesgKeyID);
        }

        /// <inheritdoc/>
        public void NoteInfoObject(IPort port, object o)
        {
            MDRF2Writer?.RecordObject(o, keyID: GetKeyIDSetForPort(port).NoteInfoObjectKeyID);
        }

        /// <inheritdoc/>
        public void NoteIssueObject(IPort port, object o)
        {
            MDRF2Writer?.RecordObject(o, keyID: GetKeyIDSetForPort(port).NoteInfoObjectKeyID);
        }

        /// <inheritdoc/>
        public void NoteE005HeaderReceived(IPort port, ITenByteHeader tbh)
        {
            MDRF2Writer?.RecordObject(tbh, keyID: GetKeyIDSetForPort(port).NoteE005HeaderReceivedKeyID);
        }

        /// <inheritdoc/>
        public void NoteE005MessageReceived(IPort port, IMessage mesg)
        {
            MDRF2Writer?.RecordObject(mesg, keyID: GetKeyIDSetForPort(port).NoteE005MessageReceivedKeyID);
        }

        /// <inheritdoc/>
        public void NoteE005MessageSent(IPort port, IMessage mesg, string resultCode)
        {
            MDRF2Writer?.RecordObject(new E005MesgSentRecord() { Mesg = mesg, ResultCode = resultCode }, keyID: GetKeyIDSetForPort(port).NoteE005MessageSentKeyID);
        }

        /// <inheritdoc/>
        public void NoteSendingE005Header(IPort port, ITenByteHeader tbh)
        {
            MDRF2Writer?.RecordObject(tbh, keyID: GetKeyIDSetForPort(port).NoteSendingE005HeaderKeyID);
        }

        /// <inheritdoc/>
        public void NoteSendingE005Message(IPort port, IMessage mesg)
        {
            MDRF2Writer?.RecordObject(mesg, keyID: GetKeyIDSetForPort(port).NoteSendingE005MessageKeyID);
        }

        /// <inheritdoc/>
        public void NoteStateChange(IPort port, INamedValueSet nvs)
        {
            MDRF2Writer?.RecordObject(nvs, keyID: GetKeyIDSetForPort(port).NoteStateChangeKeyID);
        }

        /// <inheritdoc/>
        public void NoteRecordAssociatedInformationObject(string informationIDTokenStr, object o)
        {
            MDRF2Writer?.RecordObject(o, keyID: GetKeyIDForTokenStr(informationIDTokenStr));
        }

        protected RecordingKeyIDSet GetKeyIDSetForPort(IPort port)
        {
            if (!roKeyIDsByPort.TryGetValue(port, out RecordingKeyIDSet set) || set == null)
                set = CreateKeyIDSet(port);

            return set;
        }

        protected int GetKeyIDForTokenStr(string tokenStr)
        {
            if (!roKeyIDByTokenStr.TryGetValue(tokenStr, out int keyID))
            {
                var keyName = $"PortRecording.{tokenStr}";

                lock (dictionaryMutex)
                {
                    keyID = MDRF2Writer?.RegisterAndGetKeyID(keyName) ?? default;

                    keyIDByTokenStr[tokenStr] = keyID;
                    roKeyIDByTokenStr = keyIDByTokenStr.ConvertToReadOnly();
                }
            }

            return keyID;
        }

        protected RecordingKeyIDSet CreateKeyIDSet(IPort port)
        {
            string portName = port.PartID;

            lock (dictionaryMutex)
            {
                var set = new RecordingKeyIDSet()
                {
                    NoteE005HeaderReceivedKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteE005HeaderReceived") ?? default,
                    NoteE005MessageReceivedKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteE005MessageReceived") ?? default,
                    NoteE005MessageSentKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteE005MessageSent") ?? default,
                    NoteInfoMesgKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteInfoMesg") ?? default,
                    NoteIssueMesgKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteIssueMesg") ?? default,
                    NoteInfoObjectKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteInfoObject") ?? default,
                    NoteIssueObjectKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteIssueObject") ?? default,
                    NoteSendingE005HeaderKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteSendingE005Header") ?? default,
                    NoteSendingE005MessageKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteSendingE005Message") ?? default,
                    NoteStateChangeKeyID = MDRF2Writer?.RegisterAndGetKeyID($"PortRecording.{portName}.NoteStateChange") ?? default,
                };

                keyIDsByPort[port] = set;
                roKeyIDsByPort = keyIDsByPort.ConvertToReadOnly();

                return set;
            }
        }

        private static object dictionaryMutex = new object();
        private Dictionary<IPort, RecordingKeyIDSet> keyIDsByPort = new Dictionary<IPort, RecordingKeyIDSet>();
        private ReadOnlyIDictionary<IPort, RecordingKeyIDSet> roKeyIDsByPort = new ReadOnlyIDictionary<IPort, RecordingKeyIDSet>();

        private Dictionary<string, int> keyIDByTokenStr = new Dictionary<string, int>();
        private ReadOnlyIDictionary<string, int> roKeyIDByTokenStr = new ReadOnlyIDictionary<string, int>();

        protected class RecordingKeyIDSet
        {
            public int NoteE005HeaderReceivedKeyID { get; set; }
            public int NoteE005MessageReceivedKeyID { get; set; }
            public int NoteE005MessageSentKeyID { get; set; }
            public int NoteInfoMesgKeyID { get; set; }
            public int NoteIssueMesgKeyID { get; set; }
            public int NoteInfoObjectKeyID { get; set; }
            public int NoteIssueObjectKeyID { get; set; }
            public int NoteSendingE005HeaderKeyID { get; set; }
            public int NoteSendingE005MessageKeyID { get; set; }
            public int NoteStateChangeKeyID { get; set; }
        }
    }

    public class E005MesgSentRecord : IMDRF2MessagePackSerializable
    {
        public IMessage Mesg { get; set; }

        public string ResultCode { get; set; }

        private static E005MessageTypeNameHandler E005MesgSerializer { get; } = new E005MessageTypeNameHandler();

        public void Serialize(ref MessagePackWriter mpWriter, MessagePackSerializerOptions mpOptions)
        {
            mpWriter.WriteArrayHeader(2);

            E005MesgSerializer.Serialize(ref mpWriter, Mesg, mpOptions);
            mpWriter.Write(ResultCode);
        }

        public void Deserialize(ref MessagePackReader mpReader, MessagePackSerializerOptions mpOptions)
        {
            var arrayLen = mpReader.ReadArrayHeader();
            if (arrayLen == 2)
            {
                Mesg = E005MesgSerializer.Deserialize(ref mpReader, mpOptions);
                ResultCode = mpReader.ReadString();
            }
            else
            {
                throw new System.ArgumentOutOfRangeException($"{Fcns.CurrentClassLeafName} Deserialize failed: contents are not valid [arrayLen was not 2, was {arrayLen}]");
            }
        }
    }
}
