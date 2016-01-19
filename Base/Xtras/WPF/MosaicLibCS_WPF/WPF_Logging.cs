//-------------------------------------------------------------------
/*! @file WPF_Logging.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2011 Mosaic Systems Inc., All rights reserved
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;

using System.Xml.Serialization;
using System.Configuration;
using System.ComponentModel;
using System.Reflection;

namespace MosaicLib.WPF.Logging
{
    using MosaicLib;        // apparently this makes MosaicLib get searched before MosaicLib.WPF.Logging for resolving symbols here.
    using MosaicLib.Modular.Common;

    public class LogFilterConfig
    {
        public LogFilterConfig() : this(Logging.LogGate.Debug) { }
        public LogFilterConfig(Logging.LogGate displayLogGate)
        {
            DisplayLogGate = displayLogGate;
        }

        public LogFilterConfig(LogFilterConfig rhs)
        {
            DisplayLogGate = rhs.DisplayLogGate;
        }

        public Logging.LogGate DisplayLogGate { get; protected set; }
    }

    public interface ILogMessageCollectionItem : Logging.ILogMessage
    {
    }

    public class LogMessageCollectionItem : ILogMessageCollectionItem
    {
        public LogMessageCollectionItem(Logging.ILogMessage lm) { this.lm = lm; }

        protected Logging.ILogMessage lm;

        #region ILogMessage Members

        public string LoggerName { get { return lm.LoggerName; } }
        public Logging.MesgType MesgType { get { return lm.MesgType; } }
        public string Mesg { get { return lm.Mesg; } }
        public byte[] Data { get { return lm.Data; } }
        public INamedValueSet NamedValueSet { get { return lm.NamedValueSet; } }
        public MosaicLib.Time.QpcTimeStamp EmittedQpcTime { get { return lm.EmittedQpcTime; } }
        public int SeqNum { get { return lm.SeqNum; } }
        public int ThreadID { get { return lm.ThreadID; } }
        public int Win32ThreadID { get { return lm.Win32ThreadID; } }
        public DateTime EmittedDateTime { get { return lm.EmittedDateTime; } }
        public string GetFormattedDateTime() { return lm.GetFormattedDateTime(); }
        public string GetFormattedDateTime(MosaicLib.Utils.Dates.DateTimeFormat dtFormat) { return lm.GetFormattedDateTime(dtFormat); }
        public string DisplayTime { get { return EmittedDateTime.ToString("HH:mm:ss.ffffff"); } }

        #endregion
    }

    public class LogMessageObservableCollection
        : System.Collections.ObjectModel.ObservableCollection<LogMessageCollectionItem>
        , IDisposable
    {
        public LogMessageObservableCollection()
            : base()
        {
            SynchronizationContext = System.Threading.SynchronizationContext.Current;
            AddInstance(this);
        }

        #region Local members, properties and fields

        private LogFilterConfig logFilterConfig = new LogFilterConfig();
        public LogFilterConfig LogFilterConfig
        {
            get { return logFilterConfig; }
            set
            {
                logFilterConfig = (value != null ? value : new LogFilterConfig(Logging.LogGate.None));
                lastMesgSeqNumPair = new WpfLogMessageHandlerToolBase.SeqNumPair();
                Update();
            }
        }

        public System.Threading.SynchronizationContext SynchronizationContext { get; private set; }

        public void Update() { Update(null); }

        public void Update(object state)
        {
            WpfLogMessageHandlerToolBase.SeqNumPair newSeqNumPair = WpfLogMessageHandlerToolBase.Instance.GetNewMessages(lastMesgSeqNumPair, ref updateMesgQueue);
            int maxMesgsToKeep = WpfLogMessageHandlerToolBase.Instance.MaxMessageToKeep;

            if (!newSeqNumPair.AreThereAnyChanges(lastMesgSeqNumPair))
                return;

            bool resetContents = newSeqNumPair.IsContentReplaced(lastMesgSeqNumPair);
            if (resetContents)
            {
                ClearItems();
            }

            Logging.ILogMessage lm = null;
            int numMesgAdded = 0, numMesgDropped = 0;
            int idx = 0;

            if (Count > maxMesgsToKeep)
            {
                numMesgDropped = (Count - maxMesgsToKeep);
                for (idx = 0; idx < numMesgDropped; idx++)
                    RemoveAt(0);
            }

            while (updateMesgQueue.Count > 0)
            {
                lm = updateMesgQueue.Dequeue();
                if (LogFilterConfig.DisplayLogGate.IsTypeEnabled(lm.MesgType))
                {
                    if (Count >= maxMesgsToKeep)
                    {
                        numMesgDropped++;
                        RemoveAt(0);
                    }

                    Add(new LogMessageCollectionItem(lm));
                    numMesgAdded++;
                }
            }

            lastMesgSeqNumPair = newSeqNumPair;
        }

        WpfLogMessageHandlerToolBase.SeqNumPair lastMesgSeqNumPair = new WpfLogMessageHandlerToolBase.SeqNumPair();
        Queue<Logging.ILogMessage> updateMesgQueue = new Queue<Logging.ILogMessage>();

        #endregion

        #region ObservableCollection members
        #endregion

        #region IDisposable Members and support code

        protected virtual void Dispose(Utils.DisposableBase.DisposeType disposeType)
        {
            if (disposeType == MosaicLib.Utils.DisposableBase.DisposeType.CalledExplicitly)
                RemoveInstance(this);
        }

        public void Dispose()
        {
            this.Dispose(Utils.DisposableBase.DisposeType.CalledExplicitly);

            System.GC.SuppressFinalize(this);
        }

        ~LogMessageObservableCollection()
        {
            this.Dispose(Utils.DisposableBase.DisposeType.CalledByFinalizer);
        }


        #endregion

        #region static LogMessageObservableCollection instance list and syncrhonization context

        private static object lmocInstanceListMutex = new object();
        private static List<LogMessageObservableCollection> lmocInstanceList = new List<LogMessageObservableCollection>();
        private static volatile LogMessageObservableCollection[] lmocInstanceArray = null;

        private static void AddInstance(LogMessageObservableCollection lmoc)
        {
            lock (lmocInstanceListMutex)
            {
                lmocInstanceList.Add(lmoc);
                lmocInstanceArray = null;
            }
        }

        private static void RemoveInstance(LogMessageObservableCollection lmoc)
        {
            lock (lmocInstanceListMutex)
            {
                lmocInstanceList.Remove(lmoc);
                lmocInstanceArray = null;
            }
        }

        public static LogMessageObservableCollection[] InstanceArray
        {
            get
            {
                LogMessageObservableCollection[] capturedArray = lmocInstanceArray;
                if (capturedArray != null)
                    return capturedArray;

                lock (lmocInstanceListMutex)
                {
                    if (lmocInstanceArray == null)
                        lmocInstanceArray = lmocInstanceList.ToArray();

                    // latch the first non-null SychronizationContext as the default one that is used for all such objects.
                    if (DefaultSynchronizationContext == null)
                    {
                        foreach (LogMessageObservableCollection lmoc in lmocInstanceArray)
                        {
                            System.Threading.SynchronizationContext sc = lmoc.SynchronizationContext;
                            if (sc != null)
                            {
                                DefaultSynchronizationContext = sc;
                                break;
                            }
                        }
                    }

                    return lmocInstanceArray;
                }
            }
        }

        public static System.Threading.SynchronizationContext DefaultSynchronizationContext { get; private set; }

        #endregion
    }

    public class WpfLogMessageHandlerToolBase : MosaicLib.Logging.Handlers.CommonLogMessageHandlerBase
    {
        #region Construction

        public WpfLogMessageHandlerToolBase() : this("WpfLMH", Logging.LogGate.All, 10000) { }
        public WpfLogMessageHandlerToolBase(string name, Logging.LogGate defaultCollectionGate, int maxMessagesToKeep)
            : base(name, defaultCollectionGate, false, false)
        {
            rawLogMesgArray = new RawLogMesgArray(maxMessagesToKeep);
        }

        #endregion

        #region Singleton

        static readonly Utils.SingletonHelper<WpfLogMessageHandlerToolBase> singletonHelper = new Utils.SingletonHelper<WpfLogMessageHandlerToolBase>();

        public static WpfLogMessageHandlerToolBase Instance { get { return singletonHelper.Instance; } }

        #endregion

        #region Client object interface

        public struct SeqNumPair
        {
            public SeqNumPair(Int64 lastMesgAddedSeqNum, UInt32 contentResetChangeSeqNum)
                : this()
            {
                LastMesgAddedSeqNum = lastMesgAddedSeqNum;
                ContentResetChangeSeqNum = contentResetChangeSeqNum;
            }

            public Int64 LastMesgAddedSeqNum { get; set; }
            public UInt32 ContentResetChangeSeqNum { get; set; }

            public bool AreThereAnyChanges(SeqNumPair rhs) { return (AreThereAddedMesgs(rhs) || IsContentReplaced(rhs)); }
            public bool AreThereAddedMesgs(SeqNumPair rhs) { return (LastMesgAddedSeqNum != rhs.LastMesgAddedSeqNum); }
            public bool IsContentReplaced(SeqNumPair rhs) { return (ContentResetChangeSeqNum != rhs.ContentResetChangeSeqNum); }
        }

        public SeqNumPair GetNewMessages(SeqNumPair priorSeqNumPair, ref Queue<Logging.ILogMessage> updateMesgQueue)
        {
            SeqNumPair currentSeqNumPair = new SeqNumPair(LastMesgAddedSeqNum, contentResetChangeSeqNum.Value);
            Int64 finalLastMesgAddedSeqNum = currentSeqNumPair.LastMesgAddedSeqNum;

            if (priorSeqNumPair.IsContentReplaced(currentSeqNumPair))
            {
                lock (rawLogMesgArrayMutex)
                {
                    rawLogMesgArray.AppendMesgsSinceSeqNum(0, ref updateMesgQueue, out finalLastMesgAddedSeqNum);
                }
            }
            else if (priorSeqNumPair.AreThereAddedMesgs(currentSeqNumPair))
            {
                lock (rawLogMesgArrayMutex)
                {
                    rawLogMesgArray.AppendMesgsSinceSeqNum(priorSeqNumPair.LastMesgAddedSeqNum, ref updateMesgQueue, out finalLastMesgAddedSeqNum);
                }
            }

            currentSeqNumPair.LastMesgAddedSeqNum = finalLastMesgAddedSeqNum;

            return currentSeqNumPair;
        }

        public Int64 LastMesgAddedSeqNum { get { return rawLogMesgArray.LastMesgAddedSeqNum; } }
        public int MaxMessageToKeep { get { return rawLogMesgArray.MaxMessagesToKeep; } }
        public int CurrentMessageCount { get { return rawLogMesgArray.CurrentMessageCount; } }

        #endregion

        #region CommonLogMessageHandlerBase methods

        public override void HandleLogMessage(Logging.LogMessage lm)
        {
            lock (rawLogMesgArrayMutex)
            {
                if (lm != null && lm.BelongsToPool)
                    Utils.Asserts.TakeBreakpointAfterFault("WpfLogMessageHandler must be given non-pooled messages.  It does not support ReferenceCounted message handling");
                rawLogMesgArray.PutMessage(lm);
            }

            NoteMessagesAdded();
        }

        public override void HandleLogMessages(Logging.LogMessage[] lmArray)
        {
            lock (rawLogMesgArrayMutex)
            {
                foreach (Logging.LogMessage lm in lmArray)
                {
                    if (lm != null && lm.BelongsToPool)
                        Utils.Asserts.TakeBreakpointAfterFault("WpfLogMessageHandler must be given non-pooled messages.  It does not support ReferenceCounted message handling");

                    rawLogMesgArray.PutMessage(lm);
                }
            }

            NoteMessagesAdded();
        }

        public override bool IsMessageDeliveryInProgress(int testMesgSeqNum)
        {
            // this handler does not support any form of synchronized flush.  We return that message delivery is not in progress regardless of the given testMesgSeqNum value.
            return false;
        }

        public override void Flush()
        {
            // Flush has no effect in this message handler
        }

        public override void Shutdown()
        {
            // Shutdown has no effect in this message handler (except that we will probably not get any more messages...)
        }

        #endregion

        #region Methods and properties used to inform LogMessageObservableCollection list about possible change

        /// <summary>
        /// This variable is set from the when the asynch action is created to when the invoked delegate method begins executing.  
        /// It serves to block creating new, redundant update calls until the handler has had a chance to start distributing update calls
        /// </summary>
        volatile bool invokeDistributeUpdateCallsCreatedAndNotActive = false;

        protected void NoteMessagesAdded()
        {
            if (!invokeDistributeUpdateCallsCreatedAndNotActive)
            {
                invokeDistributeUpdateCallsCreatedAndNotActive = true;

                System.Threading.SynchronizationContext defaultSC = LogMessageObservableCollection.DefaultSynchronizationContext;
                if (defaultSC != null)
                    defaultSC.Post(DistributeUpdateCalls, distributeUpdateCallsMutex);       // invoke my DistributeUpdateCalls in the context of the default LogMessageObservableCollection's SynchronizationContext
                else
                    System.Threading.ThreadPool.QueueUserWorkItem(DistributeUpdateCalls, distributeUpdateCallsMutex);       // if there is no default sc then use a ThreadPool thread
            }
        }

        object distributeUpdateCallsMutex = new object();

        protected void DistributeUpdateCalls(object mutex)
        {
            System.Threading.SynchronizationContext defaultSC = LogMessageObservableCollection.DefaultSynchronizationContext;
            System.Threading.SynchronizationContext currentSC = System.Threading.SynchronizationContext.Current;

            // block the outer update loop rate to not exceed maximum rate when being run from an thread other than the default one.
            if (!object.ReferenceEquals(defaultSC, currentSC))
                System.Threading.Thread.Sleep(DispatchUpdateStartDelay);

            lock (mutex)    // only run one instance at a time per mutex (blocks later queued calls)
            {
                LogMessageObservableCollection[] activeCollectionSnapshot = LogMessageObservableCollection.InstanceArray;

                invokeDistributeUpdateCallsCreatedAndNotActive = false;

                foreach (LogMessageObservableCollection lmoc in activeCollectionSnapshot)
                {
                    System.Threading.SynchronizationContext lmocSC = lmoc.SynchronizationContext;
                    if (object.ReferenceEquals(currentSC, lmocSC))
                        lmoc.Update();
                    else
                        lmocSC.Send(lmoc.Update, null); // syncronous cross thread invoke: only returs after the update is complete
                }
            }
        }

        #endregion

        #region internal message ring and related fields and properties

        object rawLogMesgArrayMutex = new object();
        RawLogMesgArray rawLogMesgArray;

        Utils.AtomicUInt32 contentResetChangeSeqNum = new MosaicLib.Utils.AtomicUInt32(1);

        readonly TimeSpan DispatchUpdateStartDelay = TimeSpan.FromSeconds(0.030);       // limit update rate to 30 Hz

        #endregion

        #region RawLogMesgArray definition

        private struct RawLogMesgArray
        {
            public RawLogMesgArray(int maxMessagesToKeep)
                : this()
            {
                rawLogMesgArray = new Logging.LogMessage[maxMessagesToKeep];
                rawLogMesgArrayLength = rawLogMesgArray.Length;
                putIdx = getIdx = count = 0;
                lastMesgAddedSeqNum = new MosaicLib.Utils.AtomicInt64();
            }

            int putIdx, getIdx;
            volatile int count;

            /// <summary>Int64 makes certain that seq num will never wrap.  Use of signed integer allows signed comparisons and signed results of difference tests.</summary>
            /// <remarks>use of AtomicInt64 so that increments are atomic and so that Value property is MT safe (uses atomic exchange operation internally)</remarks>
            Utils.AtomicInt64 lastMesgAddedSeqNum;

            Logging.LogMessage[] rawLogMesgArray;
            int rawLogMesgArrayLength;

            public int MaxMessagesToKeep { get { return rawLogMesgArrayLength; } }
            public int CurrentMessageCount { get { return count; } }

            public void PutMessage(Logging.LogMessage lm)
            {
                if (count >= rawLogMesgArrayLength)
                    DropLastMessage();

                rawLogMesgArray[putIdx++] = lm;
                if (putIdx >= rawLogMesgArrayLength)
                    putIdx = 0;

                lastMesgAddedSeqNum.Increment();

                count++;
            }

            public void DropLastMessage()
            {
                if (count > 0)
                {
                    getIdx++;

                    if (getIdx >= rawLogMesgArrayLength)
                        getIdx = 0;

                    --count;
                }
            }

            /// <summary>MT safe property used to obtain a snap shot of the last message added seq num (counts total messages added, starting at 0).  This has no relation to the mesgSeqNum in the mesages themselves.</summary>
            public Int64 LastMesgAddedSeqNum { get { return lastMesgAddedSeqNum.Value; } }

            public int AppendMesgsSinceSeqNum(Int64 startAfterSeqNum, ref Queue<Logging.ILogMessage> updateSet, out Int64 updateListLastItemSeqNum)
            {
                Int64 lastMesgSeqNumCopy = lastMesgAddedSeqNum.VolatileValue;
                int numItemsAddedToSet = 0;
                Int64 numRequestedMesgs = lastMesgSeqNumCopy - startAfterSeqNum;

                updateListLastItemSeqNum = lastMesgSeqNumCopy;

                if (numRequestedMesgs <= 0)
                    return numItemsAddedToSet;

                int getCount = ((numRequestedMesgs < count) ? (int)numRequestedMesgs : count);      // either give back entire list or recent subset
                int scanIdx = putIdx - getCount;
                if (scanIdx < 0)
                    scanIdx += rawLogMesgArrayLength;

                numItemsAddedToSet = getCount;

                for (; getCount > 0; getCount--)
                {
                    updateSet.Enqueue(rawLogMesgArray[scanIdx++]);
                    if (scanIdx >= rawLogMesgArrayLength)
                        scanIdx = 0;
                }

                return numItemsAddedToSet;
            }
        }

        #endregion
    }
}
