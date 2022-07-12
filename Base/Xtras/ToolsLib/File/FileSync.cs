//-------------------------------------------------------------------
/*! @file FileSync.cs
 *  @brief classes/tools used for network based File Synchronization from a Server to a Client.
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
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;

using Mosaic.ToolsLib.BufferWriter;
using Mosaic.ToolsLib.File.FileSync.Common;

using MessagePack;
using System.Runtime.Serialization;

namespace Mosaic.ToolsLib.File.FileSync
{
    namespace Client
    {
        public class TCPFileSyncClientConfig : ICopyable<TCPFileSyncClientConfig>
        {
            public TCPFileSyncClientConfig(string partID)
            {
                PartID = partID;
            }

            public string PartID { get; private set; }
            public FileSyncUpdateSelection FileSyncUpdateSelection { get; set; } = FileSyncUpdateSelection.AppendAsNeeded;
            public DestinationFileTimeHandling DestinationFileTimeHandling { get; set; } = DestinationFileTimeHandling.UpdateCreationAndLastWriteTimesFromSource;
            public string RootDirPath { get; set; } = ".";
            public bool CreateRootDirPathIfNeeded { get; set; } = true;
            public TimeSpan IncrementalRecheckIfTrackedFileStillExistsInterval { get; set; } = (10.0).FromSeconds();
            public TimeSpan DefaultBackgroundScanSyncInterval { get; set; } = (1.0).FromSeconds();
            public TimeSpan? AutoReconnectHoldoff { get; set; } = (10.0).FromSeconds();
            public string HostName { get; set; }
            public int Port { get; set; } = Common.Constants.DefaultTCPPort;
            public bool UseIPV6 { get; set; } = false;
            public TimeSpan TcpClientReceiveAndSendTimeout { get; set; } = (5.0).FromMinutes();

            /// <summary>Defines the ActionLoggingConfig that is used for part's external actions</summary>
            public ActionLoggingConfig ActionLoggingConfig { get; set; } = ActionLoggingConfig.Debug_Debug_Debug_Debug.Update(actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);
            public Logging.LogGate InitialInstanceLogGate { get; set; } = Logging.LogGate.Debug;

            public MosaicLib.File.DirectoryTreePruningManager.Config DirectoryTreePruningManagerConfig { get; set; } = null;

            public bool RecopyFilesOnAnyChange { get { return FileSyncUpdateSelection == FileSyncUpdateSelection.RecopyOnAnyChange; } }
            public bool UpdateCreateTimeFromSource { get { return DestinationFileTimeHandling != DestinationFileTimeHandling.None; } }
            public bool UpdateLastWriteTimeFromSource { get { return DestinationFileTimeHandling == DestinationFileTimeHandling.UpdateCreationAndLastWriteTimesFromSource; } }

            public int NominalMaximumTransactionsPerSet { get; set; } = 50; // up to 3 mbytes per transaction set.
            public int MaximumPendingSendFiles { get; set; } = 5;
            public int MaximumPendingCreateAndSendFiles { get; set; } = 3;

            public TimeSpan? PerTranscationSetRateLimitSleepPeriod { get; set; } = null;

            public TCPFileSyncClientConfig MakeCopyOfThis(bool deepCopy = true)
            {
                var clone = (TCPFileSyncClientConfig)MemberwiseClone();
                clone.DirectoryTreePruningManagerConfig = (DirectoryTreePruningManagerConfig != null) ? new MosaicLib.File.DirectoryTreePruningManager.Config(DirectoryTreePruningManagerConfig) : null;

                return clone;
            }
        }

        public class TCPFileSyncClient : SimpleActivePartBase, IFileSyncClient
        {
            public TCPFileSyncClient(TCPFileSyncClientConfig config)
                : base(config.PartID, SimpleActivePartBaseSettings.DefaultVersion2.Build(disableBusyBehavior: true, waitTimeLimit: (0.2).FromSeconds(), initialInstanceLogGate: config.InitialInstanceLogGate))
            {
                Config = config.MakeCopyOfThis();
                RootDirFullPath = System.IO.Path.GetFullPath(Config.RootDirPath);

                if (Config.CreateRootDirPathIfNeeded && !System.IO.Directory.Exists(RootDirFullPath))
                    System.IO.Directory.CreateDirectory(RootDirFullPath);

                ActionLoggingReference.Config = Config.ActionLoggingConfig;

                AddMainThreadStoppingAction(() => Release("Part stopping"));
            }

            private TCPFileSyncClientConfig Config { get; set; }
            private readonly string RootDirFullPath;

            private void Release(string reason)
            {
                StopAndReleaseNetworking();
                ResetLocalFileTracking(reason);

                try
                {
                    openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit($"{CurrentMethodName} failed at openFileTracker.ReleaseAndApplyAnyPendingUpdates(): {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                }
            }

            protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
            {
                if (andInitialize)
                    Release(ipf.ToString(ToStringSelect.MesgAndDetail));

                if (pruningManager == null && Config.DirectoryTreePruningManagerConfig != null)
                {
                    pruningManager = new MosaicLib.File.DirectoryTreePruningManager($"{PartID}.PruneMgr", Config.DirectoryTreePruningManagerConfig);
                    recentlyTouchedFileFullPathSetForPruningManager = new HashSet<string>();
                }

                StartLocalTrackingTimers();

                string ec = StartNetworkingIfNeeded(ipf, runPing: true);

                if (ec.IsNullOrEmpty())
                    InnerServiceBackground(QpcTimeStamp.Now);

                return ec;
            }

            protected override string PerformGoOfflineAction(IProviderActionBase ipf)
            {
                if (IsConnected)
                {
                    // tell the server that we are closing.
                    var t = new Transaction() { clientRequest = new ClientRequest() { ClientSeqNum = -1 } };
                    string ec = RunTransactions(ipf, t);
                }

                Release(ipf.ToString(ToStringSelect.MesgAndDetail));

                return string.Empty;
            }

            protected override void PerformMainLoopService()
            {
                QpcTimeStamp qpcTimeStamp = QpcTimeStamp.Now;

                if (!IsConnected && (BaseState.IsOnline || BaseState.UseState == UseState.AttemptOnlineFailed) && Config.AutoReconnectHoldoff != null)
                {
                    if (AreAnySyncOperationsInProgress || autoReconnectHoldoffTimer.StartIfNeeded(Config.AutoReconnectHoldoff ?? TimeSpan.Zero).GetIsTriggered(qpcTimeStamp))
                    {
                        SetBaseState(UseState.AttemptOnline, ConnState.Connecting, reason: "Attempting auto reconnect");

                        string ec = StartNetworkingIfNeeded(null, runPing: true);
                        if (ec.IsNullOrEmpty())
                        {
                            SetBaseState(UseState.Online, ConnState.Connected, reason: "Auto reconnect succeeded");
                        }
                        else
                        {
                            string reason = $"Auto reconnect failed: {ec}";
                            Release(reason);
                            SetBaseState(UseState.OnlineFailure, ConnState.ConnectFailed, reason: reason);
                        }
                    }
                }
                else
                {
                    autoReconnectHoldoffTimer.StopIfNeeded();
                }

                InnerServiceBackground(qpcTimeStamp, allowStartNewTransactions: IsConnected && BaseState.IsOnline);

                if (pruningManager != null)
                {
                    if (BaseState.IsOnline)
                    {
                        pruningManager.Service();
                        if (recentlyTouchedFileFullPathSetForPruningManager.Count > 0)
                        {
                            recentlyTouchedFileFullPathSetForPruningManager.DoForEach(fileName => pruningManager.NotePathAdded(fileName));
                            recentlyTouchedFileFullPathSetForPruningManager.Clear();
                        }
                    }
                    else if (recentlyTouchedFileFullPathSetForPruningManager.Count > 0)
                    {
                        recentlyTouchedFileFullPathSetForPruningManager.Clear();
                    }
                }

                base.PerformMainLoopService();
            }

            private QpcTimer autoReconnectHoldoffTimer = default;

            private void InnerServiceBackground(QpcTimeStamp qpcTimeStamp, bool allowStartNewTransactions = true)
            {
                try
                {
                    ServicePendingSyncs(qpcTimeStamp);

                    ServicePostedTransactions(qpcTimeStamp);
                    ServiceFileTracking(qpcTimeStamp);

                    ServiceTransferBusyTracking(qpcTimeStamp);

                    if (allowStartNewTransactions)
                        ServiceStartNewTransactions(qpcTimeStamp, allowStartNewTransactions: allowStartNewTransactions);
                }
                catch (System.Exception ex)
                {
                    string mesg = $"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}";

                    SetBaseState(UseState.OnlineFailure, ConnState.ConnectionFailed, mesg);

                    Release(mesg);
                }
            }

            QpcTimer transferBusyUpdateHoldoffTimer = new QpcTimer() { TriggerInterval = (1.0).FromSeconds(), AutoReset = true }.Start();
            bool IsTransferBusy => (pendingSendFileTrackerList.Count > 0);
            bool IsSyncBusy => (pendingSyncTrackerList.Count > 0);
            string transferBusyReason, syncBusyReason;
            string lastBusyReason = "";
            FileTracker[] lastPendingSendFileTrackerListArray = null;

            private void ServiceTransferBusyTracking(QpcTimeStamp qpcTimeStamp)
            {
                if (!IsTransferBusy)
                {
                    if (transferBusyReason != null)
                    {
                        transferBusyReason = null;
                        transferBusyUpdateHoldoffTimer.StopIfNeeded();
                    }
                }
                else
                {
                    bool holdoffReached = transferBusyUpdateHoldoffTimer.StartIfNeeded().GetIsTriggered(qpcTimeStamp);
                    bool setBusyNow = (transferBusyReason == null) && IsTransferBusy && (pendingSendFileTrackerList.Count > 1 || holdoffReached);
                    bool updateReason = (transferBusyReason != null) && (!object.ReferenceEquals(lastPendingSendFileTrackerListArray, pendingSendFileTrackerList.Array)) && holdoffReached;

                    if (setBusyNow || updateReason)
                    {
                        lastPendingSendFileTrackerListArray = pendingSendFileTrackerList.Array;
                        transferBusyReason = "Sending {0} files [{1}]".CheckedFormat(pendingSendFileTrackerList.Count, string.Join(",", lastPendingSendFileTrackerListArray.Select(ft => ft.fileNameAndRelativePath)));
                    }
                }

                syncBusyReason = IsSyncBusy ? "Sync request is active" : null;

                string busyReason = transferBusyReason ?? syncBusyReason;
                var currentUseState = BaseState.UseState;

                if (lastBusyReason != busyReason && (currentUseState == UseState.Online || currentUseState == UseState.OnlineBusy))
                {
                    if (busyReason.IsNeitherNullNorEmpty())
                        SetBaseState(UseState.OnlineBusy, busyReason);
                    else
                        SetBaseState(UseState.Online, "Part is no longer busy");
                }

                lastBusyReason = busyReason;
            }

            private MosaicLib.File.DirectoryTreePruningManager pruningManager;
            private HashSet<string> recentlyTouchedFileFullPathSetForPruningManager;

            #region Sync support (Sync, PerformSync, SyncTracker, ServicePendingSyncs, CancelPendingSyncs)

            public IClientFacet Sync(TimeSpan nominalWaitTimeLimit, SyncFlags syncFlags = default)
            {
                return new BasicActionImpl(ActionQueue, (ipf) => PerformSync(ipf, nominalWaitTimeLimit, syncFlags), "Sync", ActionLoggingReference, mesgDetails: (nominalWaitTimeLimit.IsZero() ? $"{syncFlags}" : $"timeLimit:{nominalWaitTimeLimit.TotalSeconds:f3}s, {syncFlags}"));
            }

            private string PerformSync(IProviderFacet ipf, TimeSpan nominalWaitTimeLimit, SyncFlags syncFlags)
            {
                haveNewSyncRequest = true;

                if (!IsConnected)
                    PerformMainLoopService();

                if (syncFlags.IsSet(SyncFlags.Full))
                {
                    string ec;

                    if (pendingTransactionList.Count == 0)
                        StartPendingTrasactionListSet();

                    var t = new Transaction() { clientRequest = new ClientRequest() { SyncRequest = true, RequestFirstSyncFileInfoSeqNum = lastReceivedSyncFileInfoArraySeqNum } };
                    pendingTransactionList.Add(t);

                    ec = RunTransactions(ipf, t);

                    if (ec.IsNullOrEmpty())
                        ProcessCompletedTransaction(t);

                    PerformMainLoopService();

                    if (ec.IsNullOrEmpty())
                        return ec;
                }

                FileTracker waitForFileTracker = fileTrackerListSortedByFileID.LastOrDefault().Value;

                if (waitForFileTracker == null)
                    return "";

                pendingSyncTrackerList.Add(new SyncTracker()
                    {
                        ipf = ipf,
                        busyToken = CreateInternalBusyFlagHolderObject(startingActionName: ipf.ToString(ToStringSelect.MesgAndDetail)),
                        waitForFileTracker = waitForFileTracker,
                        waitForSyncInfo = waitForFileTracker.remoteSyncFileInfo,
                        progressUpdateTimer = new QpcTimer() { TriggerInterval = (0.5).FromSeconds(), AutoReset = true }.Start(),
                        referenceTotalTransferredNewBytes = totalTransferredNewBytes,
                        referencePendingBytes = pendingBytes,
                        nvs = new NamedValueSet()
                            {
                                { "current", 0L },
                                { "total", pendingBytes },
                            },
                        nominalWaitTimeLimit = nominalWaitTimeLimit,
                        waitLimitTimer = !nominalWaitTimeLimit.IsZero() ? new QpcTimer() { TriggerInterval = nominalWaitTimeLimit }.Start() : default,
                    });

                PerformMainLoopService();

                return null;
            }

            private class SyncTracker
            {
                public IProviderFacet ipf;
                public IDisposable busyToken;
                public FileTracker waitForFileTracker;
                public SyncFileInfo waitForSyncInfo;
                public QpcTimer progressUpdateTimer;
                public long referencePendingBytes;
                public long referenceTotalTransferredNewBytes;
                public NamedValueSet nvs;
                public TimeSpan nominalWaitTimeLimit;
                public QpcTimer waitLimitTimer;

                public void Complete(string resultCode)
                {
                    ipf.CompleteRequest(resultCode);
                    Fcns.DisposeOfObject(ref busyToken);
                }

                public void ServicePendingPublication(long pendingBytes, long totalTransferredNewBytes, QpcTimeStamp qpcTimeStamp)
                {
                    if (progressUpdateTimer.GetIsTriggered(qpcTimeStamp))
                        UpdateProgress(pendingBytes, totalTransferredNewBytes);
                }

                public void UpdateProgress(long pendingBytes, long totalTransferredNewBytes)
                {
                    if (referencePendingBytes < pendingBytes)
                    {
                        referencePendingBytes = pendingBytes;
                        nvs.SetValue("total", referencePendingBytes);
                    }

                    // note: the following is an estimate of the current progress.  This logic cannot determine if any increase in the pendingBytes relates to the file tracker we are waiting for or not.  As such this logic may be an overestimate of the work required to complete this sync requests.
                    var bytesTransferedSinceStartOfSync = totalTransferredNewBytes - referenceTotalTransferredNewBytes;
                    var pendingDecreaseSinceStartOfSync = referencePendingBytes - pendingBytes;

                    nvs.SetValue("current", Math.Min(bytesTransferedSinceStartOfSync, pendingDecreaseSinceStartOfSync));
                    ipf.UpdateNamedValues(nvs);
                }
            }

            private readonly IListWithCachedArray <SyncTracker> pendingSyncTrackerList = new IListWithCachedArray<SyncTracker>();
            private bool AreAnySyncOperationsInProgress { get { return (pendingSyncTrackerList.Count > 0) || haveNewSyncRequest; } }
            private bool haveNewSyncRequest = false;

            private void ServicePendingSyncs(QpcTimeStamp qpcTimeStamp)
            {
                if (pendingSyncTrackerList.Count > 0)
                {
                    foreach (var syncTracker in pendingSyncTrackerList.Array)
                    {
                        var waitForFileTracker = syncTracker.waitForFileTracker;
                        string resultCode = syncTracker.ipf.IsCancelRequestActive ? "Cancel requested" : waitForFileTracker.FaultCode.MapEmptyToNull();

                        if (resultCode == null && (waitForFileTracker.successfullyTransferredCount >= syncTracker.waitForSyncInfo.FileLength))
                            resultCode = string.Empty;

                        if (resultCode.IsNullOrEmpty() && waitForFileTracker.FaultCode.IsNeitherNullNorEmpty())
                            resultCode = waitForFileTracker.FaultCode;

                        if (resultCode.IsNullOrEmpty() && (waitForFileTracker.remoteSyncFileInfo?.Exists != true))
                        {
                            Log.Debug.Emit($"Nonstandard sync request completion: remote file {waitForFileTracker.fileID}:'{waitForFileTracker.fileNameAndRelativePath}' deleted unexpectedly");
                            resultCode = string.Empty;
                        }

                        if (resultCode == null && syncTracker.waitLimitTimer.GetIsTriggered(qpcTimeStamp))
                            resultCode = $"Sync wait time limit reached [after {syncTracker.waitLimitTimer.ElapsedTimeAtLastTrigger.TotalSeconds:f3} sec]";

                        if (resultCode != null)
                        {
                            pendingSyncTrackerList.Remove(syncTracker);
                            syncTracker.Complete(resultCode);

                            PublishBaseState(resultCode.IsNullOrEmpty() ? "Sync completed" : "Sync failed");
                        }
                        else
                        {
                            syncTracker.ServicePendingPublication(pendingBytes, totalTransferredNewBytes, qpcTimeStamp);
                        }
                    }

                    var baseState = BaseState;
                    if (!baseState.IsOnlineOrAttemptOnline || baseState.IsFaulted())
                        CancelPendingSyncs($"Part is not online normally [{baseState}]");
                    else if (!IsConnected)
                        CancelPendingSyncs($"Part is not connected [{baseState}]");
                }
                else if (haveNewSyncRequest)
                {
                    haveNewSyncRequest = false;
                }

                if (recalculatePendingBytes)
                {
                    recalculatePendingBytes = false;

                    long nextPendingBytes = 0;
                    foreach (var ft in fileTrackerByFileIDDictionary.ValueArray)
                    {
                        var ftPendingBytes = ft.remoteSyncFileInfo.FileLength - ft.successfullyTransferredCount;
                        nextPendingBytes += ftPendingBytes;
                    }

                    pendingBytes = nextPendingBytes;
                }
            }

            private void CancelPendingSyncs(string reason)
            {
                if (pendingSyncTrackerList.Count > 0)
                {
                    pendingSyncTrackerList.Array.DoForEach(syncTracker => syncTracker.Complete(reason));
                    pendingSyncTrackerList.Clear();
                }
            }

            #endregion

            #region Local file tracking support

            private void ResetLocalFileTracking(string reason)
            {
                CancelPendingSyncs(reason);

                fileTrackerByFileIDDictionary.Clear();
                maxKnownFileID = -1;
                fileTrackerListSortedByFileID.Clear();
                totalTransferredNewBytes = 0;
                pendingBytes = 0;
                lastReceivedSyncFileInfoArraySeqNum = 0;

                transactionBuilderList.Clear();
                pendingTransactionList.Clear();
                pendingSendFileTrackerList.Clear();
                pendingEvaluateFileTrackerList.Clear();
                pendingEvaluateFileTrackerSet.Clear();
            }

            private void StartLocalTrackingTimers()
            {
                backgroundScanSyncIntervalTimer.StartIfNeeded(Config.DefaultBackgroundScanSyncInterval);
                incrementalRecheckIfTrackedFileStillExistsTimer.StartIfNeeded(Config.IncrementalRecheckIfTrackedFileStillExistsInterval);
            }

            private class FileTracker
            {
                public long fileID;
                public string fileNameAndRelativePath;
                public string fileFullPath;
                public SyncFileInfo localSyncFileInfo;
                public SyncFileInfo remoteSyncFileInfo;
                public long successfullyTransferredCount;

                public string FaultCode
                {
                    get { return _FaultCode; }
                    set
                    {
                        IsDisabled = (_FaultCode = value).IsNeitherNullNorEmpty();
                        if (IsDisabled)
                            disableUntil.StartIfNeeded((1.0).FromMinutes());
                        else
                            disableUntil.StopIfNeeded();
                    }
                }

                private string _FaultCode;
                public QpcTimer disableUntil;

                public bool IsDisabled { get; set; }

                public bool recheckIfExists;
            }

            private readonly IDictionaryWithCachedArrays<long, FileTracker> fileTrackerByFileIDDictionary = new IDictionaryWithCachedArrays<long, FileTracker>();
            private long maxKnownFileID = -1;
            private readonly SortedList<long, FileTracker> fileTrackerListSortedByFileID = new SortedList<long, FileTracker>();
            private readonly IListWithCachedArray<FileTracker> pendingSendFileTrackerList = new IListWithCachedArray<FileTracker>();
            private readonly HashSet<FileTracker> pendingEvaluateFileTrackerSet = new HashSet<FileTracker>();
            private readonly List<FileTracker> pendingEvaluateFileTrackerList = new List<FileTracker>();

            private void AddToPendingEvaluateListIfNeeded(FileTracker ft)
            {
                if (ft != null && !pendingEvaluateFileTrackerSet.Contains(ft))
                {
                    pendingEvaluateFileTrackerList.Add(ft);
                    pendingEvaluateFileTrackerSet.Add(ft);
                }
            }

            private void RemoveFromPendingEvaluateListIfNeeded(FileTracker ft)
            {
                if (pendingEvaluateFileTrackerSet.Contains(ft))
                {
                    pendingEvaluateFileTrackerList.Remove(ft);
                    pendingEvaluateFileTrackerSet.Remove(ft);
                }
            }

            long totalTransferredNewBytes, pendingBytes;
            bool recalculatePendingBytes;
            long lastReceivedNewestSyncFileInfoSeqNum = -1; // initial value is destinct from all values the server can produce so we know if we have reeeived the initial response from the server.
            long lastReceivedSyncFileInfoArraySeqNum = 0;

            void UpdateLastReceivedNewestSyncInfoSeqNum(Transaction t)
            {
                lastReceivedNewestSyncFileInfoSeqNum = Math.Max(lastReceivedNewestSyncFileInfoSeqNum, t?.serverResponse?.NewestSyncFileInfoSeqNum ?? -1);
            }

            private readonly List<Transaction> transactionBuilderList = new List<Transaction>();
            private readonly List<Transaction> pendingTransactionList = new List<Transaction>();
            private QpcTimeStamp pendingTransactionListStartTime;
            private int pendingTransactionListByteCount, pendingTransactionListTransactionCount;
            private void StartPendingTrasactionListSet()
            {
                pendingTransactionListStartTime = QpcTimeStamp.Now;
                pendingTransactionListByteCount = 0;
                pendingTransactionListTransactionCount = 0;
            }

            private QpcTimer backgroundScanSyncIntervalTimer = new QpcTimer() { AutoReset = true };
            private QpcTimer rescanDisabledTrackersTimer = new QpcTimer() { TriggerInterval = (5.0).FromSeconds(), AutoReset = true }.Start();
            private int disabledTrackerCount;

            private QpcTimer incrementalRecheckIfTrackedFileStillExistsTimer = new QpcTimer() { AutoReset = true };
            private int rescanNextFileTrackerIndex = 0;

            private struct OpenFileTracker
            {
                public FileTracker fileTracker;
                public FileStream fileStream;
                public TCPFileSyncClient part;

                public long? pendingSetLength;
                public long? pendingCreationTimeUTCTicksUpdate;
                public long? pendingLastWriteTimeUTCTicksUpdate;

                public FileStream OpenIfNeeded(FileTracker fileTracker, TCPFileSyncClient part)
                {
                    if (Object.ReferenceEquals(this.fileTracker, fileTracker) && this.fileTracker != null)
                        return fileStream;

                    ReleaseAndApplyAnyPendingUpdatesIfNeeded();

                    this.part = part;

                    for (int tryCount = 0;;)
                    {
                        try
                        {
                            fileStream = new FileStream(fileTracker.fileFullPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                            break;
                        }
                        catch (System.IO.IOException ex)
                        {
                            tryCount++;

                            if (tryCount < 6)
                            {
                                part.Log.Trace.Emit($"{CurrentMethodName} issue for {fileTracker.fileID}:'{fileTracker.fileNameAndRelativePath}' at try:{tryCount}: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                                ((tryCount > 1) ? (0.1).FromSeconds() : TimeSpan.Zero).Sleep();
                            }
                            else
                            {
                                part.Log.Debug.Emit($"{CurrentMethodName} failed for {fileTracker.fileID}:'{fileTracker.fileNameAndRelativePath}' at try:{tryCount}: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                                throw;
                            }
                        }
                    }

                    this.fileTracker = fileTracker;
                    pendingCreationTimeUTCTicksUpdate = null;
                    pendingLastWriteTimeUTCTicksUpdate = null;

                    return fileStream;
                }

                public bool IsOpen { get { return fileStream != null; } }

                public void ReleaseAndApplyAnyPendingUpdatesIfNeeded()
                {
                    var ft = fileTracker;

                    fileTracker = null;

                    try
                    {
                        if (fileStream != null)
                        {
                            if (pendingSetLength != null)
                            {
                                var setLength = pendingSetLength ?? 0;
                                if (setLength == -1)
                                    setLength = ft.successfullyTransferredCount;

                                fileStream.SetLength(setLength);

                                ft.localSyncFileInfo.FileLength = setLength;
                                if (ft.successfullyTransferredCount > setLength)
                                    ft.successfullyTransferredCount = setLength;

                                pendingSetLength = null;
                            }

                            fileStream.Close();
                            Fcns.DisposeOfObject(ref fileStream);
                        }

                        if (pendingCreationTimeUTCTicksUpdate != null)
                        {
                            var entryCreationTimeUTCTicks = ft.localSyncFileInfo.CreationTimeUTCTicks;

                            var creationTimeUTCTicks = pendingCreationTimeUTCTicksUpdate ?? 0;
                            pendingCreationTimeUTCTicksUpdate = null;

                            var creationTimeUTC = new DateTime(creationTimeUTCTicks, DateTimeKind.Utc);

                            System.IO.File.SetCreationTimeUtc(ft.fileFullPath, creationTimeUTC);
                            ft.localSyncFileInfo.CreationTimeUTCTicks = creationTimeUTCTicks;

                            part.Log.Debug.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' creation time has been updated to '{creationTimeUTC.CvtToString(Dates.DateTimeFormat.RoundTrip)}' [from:{new DateTime(entryCreationTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)}]");
                        }

                        if (pendingLastWriteTimeUTCTicksUpdate != null)
                        {
                            var entryLastWriteTimeUTCTicks = ft.localSyncFileInfo.LastWriteTimeUTCTicks;

                            var lastWriteTimeUTCTicks = pendingLastWriteTimeUTCTicksUpdate ?? 0;
                            pendingLastWriteTimeUTCTicksUpdate = null;

                            var lastWriteTimeUTC = new DateTime(lastWriteTimeUTCTicks, DateTimeKind.Utc);

                            System.IO.File.SetLastWriteTime(ft.fileFullPath, lastWriteTimeUTC);
                            ft.localSyncFileInfo.LastWriteTimeUTCTicks = lastWriteTimeUTCTicks;

                            // if the time difference is large enough then log at debug otherwise log at trace level.
                            var absTimeDiff = new TimeSpan(Math.Abs(lastWriteTimeUTCTicks - entryLastWriteTimeUTCTicks));
                            var emitter = part.Log.Emitter((absTimeDiff > (100.0).FromMilliseconds()) ? Logging.MesgType.Debug : Logging.MesgType.Trace);

                            emitter.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' last write time has been updated to '{lastWriteTimeUTC.CvtToString(Dates.DateTimeFormat.RoundTrip)}' [from:{new DateTime(entryLastWriteTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)}]");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        part.DisableFileTracker(ft, $"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    }

                    pendingSetLength = null;
                    pendingCreationTimeUTCTicksUpdate = null;
                    pendingLastWriteTimeUTCTicksUpdate = null;
                }
            }

            OpenFileTracker openFileTracker = default;

            private void ServicePostedTransactions(QpcTimeStamp qpcTimeStamp)
            {
                try
                {
                    if (pendingTransactionList.Count > 0)
                    {
                        while (pendingTransactionList.Count > 0)
                        {
                            var t = pendingTransactionList[0];

                            if (t.serverResponse != null)
                            {
                                ProcessCompletedTransaction(t);
                                pendingTransactionList.RemoveAt(0);
                            }
                            else
                            {
                                break;
                            }
                        }

                        openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();

                        if (pendingTransactionList.Count == 0)
                        {
                            double runTime = pendingTransactionListStartTime.Age.TotalSeconds;
                            Log.Trace.Emit("TransactionSet processed count:{0} runTime:{1:f3} bytes:{2} rate:{3:f3} mb/s", pendingTransactionListTransactionCount, runTime, pendingTransactionListByteCount, pendingTransactionListByteCount * 0.000001 * runTime.SafeOneOver());
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit($"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }
            }

            private void ServiceFileTracking(QpcTimeStamp qpcTimeStamp)
            {
                if (disabledTrackerCount > 0 && rescanDisabledTrackersTimer.GetIsTriggered(qpcTimeStamp))
                {
                    var nextDisabledTrackerCount = 0;
                    // search for disabled trackers and for trackers that are setup pending
                    foreach (var ft in fileTrackerByFileIDDictionary.ValueArray.Where(ft => ft.IsDisabled))
                    {
                        if (ft.disableUntil.GetIsTriggered(qpcTimeStamp))
                        {
                            ft.IsDisabled = false;
                            AddToPendingEvaluateListIfNeeded(ft);
                        }
                        else
                        {
                            nextDisabledTrackerCount++;
                        }
                    }

                    disabledTrackerCount = nextDisabledTrackerCount;
                }

                while (pendingEvaluateFileTrackerList.Count > 0 && pendingSendFileTrackerList.Count < Config.MaximumPendingSendFiles)
                {
                    // NOTE: peek the ft at the head of the list here.  do not take the ft from the list here so that we can leave this ft at the head of the list later.
                    var ft = pendingEvaluateFileTrackerList.First();

                    // tracker has just been added - read the first local file info to see if it already exists and if it has the same contents.
                    if (ft.localSyncFileInfo == null)
                    {
                        try
                        {
                            var fileFullPath = ft.fileFullPath;
                            var syncFileInfo = ft.remoteSyncFileInfo;

                            if (System.IO.File.Exists(fileFullPath))
                            {
                                RemoveFromPendingEvaluateListIfNeeded(ft);

                                bool readInfoSuccess = ReadInitialLocalSyncInfo(ft);

                                if (readInfoSuccess && ft.remoteSyncFileInfo.FileLength > ft.successfullyTransferredCount)
                                    pendingSendFileTrackerList.Add(ft);
                            }
                            else if (pendingSendFileTrackerList.Count < Config.MaximumPendingCreateAndSendFiles)
                            {
                                RemoveFromPendingEvaluateListIfNeeded(ft);

                                bool createSuccess = CreateFileAndSetCreationTime(ft) && ReadInitialLocalSyncInfo(ft);

                                if (createSuccess && ft.remoteSyncFileInfo.FileLength > ft.successfullyTransferredCount)
                                    pendingSendFileTrackerList.Add(ft);
                            }
                            else
                            {
                                // leave the current file where it is in the list - this blocks further evaluation until we receive the first parts of this file.
                                break;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            DisableFileTracker(ft, $"{CurrentMethodName} service for {ft.fileID}:'{ft.fileNameAndRelativePath}' failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                        }
                    }
                    else if (ft.remoteSyncFileInfo.FileLength > ft.successfullyTransferredCount || ft.recheckIfExists)
                    {
                        if (Config.RecopyFilesOnAnyChange && ft.successfullyTransferredCount != 0)
                        {
                            ft.successfullyTransferredCount = 0;
                            recalculatePendingBytes = true;
                        }

                        pendingSendFileTrackerList.Add(ft);
                        RemoveFromPendingEvaluateListIfNeeded(ft);
                    }
                    else if (ft.recheckIfExists)
                    {
                        pendingSendFileTrackerList.Add(ft);
                        RemoveFromPendingEvaluateListIfNeeded(ft);
                    }
                    else if (ft.localSyncFileInfo.LastWriteTimeUTCTicks != ft.remoteSyncFileInfo.LastWriteTimeUTCTicks)
                    {
                        openFileTracker.OpenIfNeeded(ft, this);
                        openFileTracker.pendingLastWriteTimeUTCTicksUpdate = ft.remoteSyncFileInfo.LastWriteTimeUTCTicks;
                        openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();

                        RemoveFromPendingEvaluateListIfNeeded(ft);
                        AddToPendingEvaluateListIfNeeded(ft);
                    }
                    else
                    {
                        RemoveFromPendingEvaluateListIfNeeded(ft);
                    }
                }

                // occasionally reconfirm the existance of the each file.
                if (incrementalRecheckIfTrackedFileStillExistsTimer.GetIsTriggered(qpcTimeStamp))
                {
                    var ft = fileTrackerByFileIDDictionary.ValueArray.SafeAccess(rescanNextFileTrackerIndex++);
                    if (ft != null)
                    {
                        ft.recheckIfExists = true;
                        AddToPendingEvaluateListIfNeeded(ft);
                    }
                    else
                    {
                        rescanNextFileTrackerIndex = 0;
                    }
                }
            }

            private bool CreateFileAndSetCreationTime(FileTracker ft)
            {
                try
                {
                    var dirPartOfFullFilePath = System.IO.Path.GetDirectoryName(ft.fileFullPath);
                    if (!System.IO.Directory.Exists(dirPartOfFullFilePath))
                    {
                        Log.Debug.Emit($"Creating directory '{dirPartOfFullFilePath}' for file {ft.fileID}:'{ft.fileNameAndRelativePath}'");
                        System.IO.Directory.CreateDirectory(dirPartOfFullFilePath);
                    }

                    System.IO.File.Create(ft.fileFullPath).Close();

                    if (Config.UpdateCreateTimeFromSource)
                    {
                        var creationTimeUTC = new DateTime(ft.remoteSyncFileInfo.CreationTimeUTCTicks, DateTimeKind.Utc);

                        System.IO.File.SetCreationTimeUtc(ft.fileFullPath, creationTimeUTC);

                        Log.Debug.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' created and creation time has been set to '{creationTimeUTC.CvtToString(Dates.DateTimeFormat.RoundTrip)}'");
                    }
                    else
                    {
                        Log.Debug.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' has been created");
                    }

                    return true;
                }
                catch (System.Exception ex)
                {
                    DisableFileTracker(ft, $"{CurrentMethodName} failed for {ft.fileID}:'{ft.fileNameAndRelativePath}': {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    return false;
                }
            }

            private bool ReadInitialLocalSyncInfo(FileTracker ft)
            {
                try
                {
                    var sysFileInfo = new FileInfo(ft.fileFullPath);
                    ft.localSyncFileInfo = new SyncFileInfo(sysFileInfo, 0, 0, ft.fileNameAndRelativePath);

                    ft.successfullyTransferredCount = 0;
                    recalculatePendingBytes = true;

                    var lengthDiff = Math.Abs(ft.localSyncFileInfo.FileLength - ft.remoteSyncFileInfo.FileLength);
                    var createTimeDiff = new TimeSpan(Math.Abs(ft.localSyncFileInfo.CreationTimeUTCTicks - ft.remoteSyncFileInfo.CreationTimeUTCTicks));
                    var lastWriteTimeDiff = new TimeSpan(Math.Abs(ft.localSyncFileInfo.LastWriteTimeUTCTicks - ft.remoteSyncFileInfo.LastWriteTimeUTCTicks));

                    var retransferFileContents = Config.RecopyFilesOnAnyChange
                                            && (lengthDiff != 0)
                                            && (createTimeDiff > (0.01).FromSeconds())  // if updating create time is disabled then this is likely to trigger a copy on each new connection
                                            && (lastWriteTimeDiff > (0.01).FromSeconds())  // if updating last write time is disabled then this is likely to trigger a copy on each new connection
                                            ;

                    if (!retransferFileContents)
                        ft.successfullyTransferredCount = ft.localSyncFileInfo.FileLength;

                    recentlyTouchedFileFullPathSetForPruningManager?.Add(ft.fileFullPath);

                    AddToPendingEvaluateListIfNeeded(ft);

                    return true;
                }
                catch (System.Exception ex)
                {
                    DisableFileTracker(ft, $"{CurrentMethodName} failed for {ft.fileID}:'{ft.fileNameAndRelativePath}': {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    return false;
                }
            }

            private void ServiceStartNewTransactions(QpcTimeStamp qpcTimeStamp, bool allowStartNewTransactions = true)
            {
                if (pendingTransactionList.Count == 0)
                {
                    if (transactionBuilderList.Count > 0)
                       transactionBuilderList.Clear();

                    foreach (var ft in pendingSendFileTrackerList.Array)
                    {
                        var minBytesToRead = ft.remoteSyncFileInfo.FileLength - ft.successfullyTransferredCount;
                        if (minBytesToRead <= 0 && !ft.recheckIfExists)
                        {
                            pendingSendFileTrackerList.Remove(ft);
                            AddToPendingEvaluateListIfNeeded(ft);
                        }
                        else
                        {
                            // if there are at least 4096 bytes to transfer then round the starting read position down to multiples of 4096, otherwise round the starting read position down to multiples of 512;
                            var startNextTransferAt = (ft.successfullyTransferredCount & ((minBytesToRead < 4096) ? ~511L : ~4095L));

                            var bytesToRead = ft.remoteSyncFileInfo.FileLength - startNextTransferAt;

                            if (bytesToRead > 0)
                            {
                                while (transactionBuilderList.Count < Config.NominalMaximumTransactionsPerSet && bytesToRead > 0)
                                {
                                    int requestTransferSize = GetRequestTransferSize(bytesToRead);

                                    transactionBuilderList.Add(new Transaction() { clientRequest = new ClientRequest() { FileID = ft.fileID, FileOffset = startNextTransferAt, RequestedTransferSize = requestTransferSize } });
                                    startNextTransferAt += requestTransferSize;
                                    bytesToRead -= requestTransferSize;     // note: this result may be negative since we often ask to read more bytes than the file actually contains.

                                    ft.recheckIfExists = false;
                                }
                            }
                            else if (ft.recheckIfExists)
                            {
                                transactionBuilderList.Add(new Transaction() { clientRequest = new ClientRequest() { FileID = ft.fileID } });
                                ft.recheckIfExists = false;
                            }

                            if (transactionBuilderList.Count >= Config.NominalMaximumTransactionsPerSet)
                                break;
                        }
                    }

                    if (transactionBuilderList.Count == 0)
                    {
                        //NOTE: the following code explicitly asks for new sync file info objects since the last one that was received in the array portion of the reaponse even if the last received newest seq num is quite different.
                        // this is because deleted files bump the newest seq num but not the last one in the array (on their own).  They may even cause the last one in the array to decrease in seq num if it was the mostlyly changed file that was deleted.
                        bool issueNormalSyncNow = haveNewSyncRequest || backgroundScanSyncIntervalTimer.GetIsTriggered(qpcTimeStamp) || (lastReceivedNewestSyncFileInfoSeqNum == -1);
                        if (issueNormalSyncNow)
                            transactionBuilderList.Add(new Transaction() { clientRequest = new ClientRequest() { RequestFirstSyncFileInfoSeqNum = lastReceivedSyncFileInfoArraySeqNum + 1, IncludeFileNamesAfterFileID = maxKnownFileID } });
                    }
                    else
                    {
                        var lastTransaction = transactionBuilderList.LastOrDefault();
                        if (lastTransaction != null)
                            lastTransaction.clientRequest.RequestFirstSyncFileInfoSeqNum = lastReceivedSyncFileInfoArraySeqNum + 1;
                    }

                    if (transactionBuilderList.Count > 0)
                    {
                        if (pendingTransactionList.Count == 0)
                            StartPendingTrasactionListSet();

                        pendingTransactionList.AddRange(transactionBuilderList);
                        transactionBuilderList.Clear();

                        Log.Trace.Emit("TransactionSet post {0}", pendingTransactionList.Count);

                        postedTransactionSetBuffer.Add(pendingTransactionList.ToArray());
                    }
                }
            }

            private void DisableFileTracker(FileTracker ft, string faultCode)
            {
                Log.Debug.Emit(faultCode);

                ft.FaultCode = faultCode;

                disabledTrackerCount += 1;

                RemoveFromPendingEvaluateListIfNeeded(ft);
                if (pendingSendFileTrackerList.Contains(ft))
                    pendingSendFileTrackerList.Remove(ft);

                ft.successfullyTransferredCount = 0;
                recalculatePendingBytes = true;
            }

            /// <summary>
            /// This method is used to determine the largest transfer size to request for a given number of total bytes to read.
            /// <para/>Goals:  Never use a buffer size that will be pushed into the large object heap.  
            /// Use powers of two for buffer sizes to maximum the incremental transfer size while decreasing the total number of individual buffer sizes that might need to be pooled.
            /// Keep in mind that 4096 and 512 are standard sector sizes.  
            /// Normally the file offset that each transfer is started from will be rounded down to the next multiple of 4096 or 512
            /// to increase the likelyhood that the transfer will be performed on sector boundaries.
            /// <para/>The decision to never ask for fewer than 512 bytes is intentional as the added costs of transferring extra bytes in the buffer are generally offset by the decrease in the number of transactions and 
            /// </summary>
            private static int GetRequestTransferSize(long bytesToRead)
            {
                //if (bytesToRead > (4*65536 - 512))
                //    return 4*65536;
                //else if (bytesToRead > (2*65536 - 512))
                //    return 2*65536;
                //else 
                if (bytesToRead > (65536 - 512))
                    return 65536;
                else if (bytesToRead > (32768 - 512))
                    return 32768;
                else if (bytesToRead > (16384 - 512))
                    return 16384;
                else if (bytesToRead > (8192 - 512))
                    return 8192;
                else if (bytesToRead > (4096 - 512))
                    return 4096;
                else if (bytesToRead > (2048 - 512))
                    return 2048;
                else if (bytesToRead > (1024 - 512))
                    return 1024;
                else
                    return 512;
            }

            private void ProcessCompletedTransaction(Transaction t)
            {
                var clientRequest = t.clientRequest;
                var serverResponse = t.serverResponse;

                if (serverResponse.SyncFileInfoArray != null)
                    ProcessSyncFileInfoArray(serverResponse.SyncFileInfoArray, serverResponse);

                if (serverResponse.SyncFileInfo != null)
                    ProcessIncrementalFileContents(serverResponse);

                pendingTransactionListByteCount += serverResponse.DataCount;
                pendingTransactionListTransactionCount += 1;

                UpdateLastReceivedNewestSyncInfoSeqNum(t);
            }

            private void ProcessSyncFileInfoArray(SyncFileInfo[] syncFileInfoArray, ServerResponse serverResonse)
            {
                foreach (var syncFileInfo in syncFileInfoArray)
                {
                    var ft = fileTrackerByFileIDDictionary.SafeTryGetValue(syncFileInfo.FileID);
                    if (ft != null)
                        HandleSyncFileInfoForExistingTracker(ft, syncFileInfo, serverResonse);
                    else
                        HandleNewFile(syncFileInfo);

                    lastReceivedSyncFileInfoArraySeqNum = syncFileInfo.SeqNum;
                }
            }

            private void HandleSyncFileInfoForExistingTracker(FileTracker ft, SyncFileInfo remoteSyncFileInfo, ServerResponse serverResponse)
            {
                var entryRemoteSyncFileInfo = ft.remoteSyncFileInfo;
                ft.remoteSyncFileInfo = remoteSyncFileInfo;

                Log.Trace.Emit("SyncFileInfo for {0}:'{1}' changed to {2} [from {3}]", ft.fileID, ft.fileNameAndRelativePath, remoteSyncFileInfo, entryRemoteSyncFileInfo);

                bool addToPendingEvalList = false;

                if (ft.localSyncFileInfo != null)
                {
                    var createTimeDiff = new TimeSpan(ft.localSyncFileInfo.CreationTimeUTCTicks - remoteSyncFileInfo.CreationTimeUTCTicks);
                    var lastWriteTimeDiff = new TimeSpan(ft.localSyncFileInfo.LastWriteTimeUTCTicks - remoteSyncFileInfo.LastWriteTimeUTCTicks);

                    // create time and last write time update
                    if (Config.UpdateCreateTimeFromSource
                        && !createTimeDiff.IsZero()
                        && openFileTracker.pendingCreationTimeUTCTicksUpdate != remoteSyncFileInfo.CreationTimeUTCTicks)
                    {
                        if (!openFileTracker.IsOpen)
                            openFileTracker.OpenIfNeeded(ft, this);

                        openFileTracker.pendingCreationTimeUTCTicksUpdate = remoteSyncFileInfo.CreationTimeUTCTicks;

                        if (Config.RecopyFilesOnAnyChange)
                        {
                            ft.successfullyTransferredCount = 0;
                            recalculatePendingBytes = true;
                        }

                        addToPendingEvalList = true;
                    }

                    if (Config.UpdateLastWriteTimeFromSource
                        && !lastWriteTimeDiff.IsZero()
                        && (ft.successfullyTransferredCount >= remoteSyncFileInfo.FileLength || serverResponse.DataCount == 0)
                        && openFileTracker.pendingLastWriteTimeUTCTicksUpdate != remoteSyncFileInfo.LastWriteTimeUTCTicks)
                    {
                        if (!openFileTracker.IsOpen)
                            openFileTracker.OpenIfNeeded(ft, this);

                        openFileTracker.pendingLastWriteTimeUTCTicksUpdate = remoteSyncFileInfo.LastWriteTimeUTCTicks;

                        if (Config.RecopyFilesOnAnyChange)
                        {
                            ft.successfullyTransferredCount = 0;
                            recalculatePendingBytes = true;
                        }

                        addToPendingEvalList = true;
                    }
                }

                var newFileLength = remoteSyncFileInfo.FileLength;
                var lengthChange = (newFileLength - entryRemoteSyncFileInfo.FileLength);

                if (lengthChange == 0)
                {
                    // length did not change - nothing more to do here.
                }
                else if ((newFileLength < ft.successfullyTransferredCount || newFileLength < entryRemoteSyncFileInfo.FileLength))
                {
                    // reset of file contents is needed
                    Log.Debug.Emit($"Starting Content Reset (Trim zero) for {ft.fileID}:'{ft.fileNameAndRelativePath}' because length shrunk [newLen:{newFileLength}, xferCount:{ft.successfullyTransferredCount}, prevLen:{entryRemoteSyncFileInfo.FileLength}]");

                    ft.successfullyTransferredCount = 0;
                    recalculatePendingBytes = true;

                    try
                    {
                        openFileTracker.OpenIfNeeded(ft, this);
                        openFileTracker.pendingSetLength = 0;
                        openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();
                    }
                    catch (System.Exception ex)
                    {
                        DisableFileTracker(ft, $"Content Reset (Trim zero) for {ft.fileID}:'{ft.fileNameAndRelativePath}' failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                    }

                    // remove the tracker from the current send list and put it at the end of the pending evaluate list.
                    RemoveFromPendingEvaluateListIfNeeded(ft);
                    if (pendingSendFileTrackerList.Contains(ft))
                        pendingSendFileTrackerList.Remove(ft);

                    addToPendingEvalList = true;
                }
                else if (lengthChange != 0 && Config.RecopyFilesOnAnyChange)
                {
                    ft.successfullyTransferredCount = 0;
                    recalculatePendingBytes = true;
                    addToPendingEvalList = true;
                }
                else
                {
                    pendingBytes += lengthChange;
                    addToPendingEvalList = true;
                }

                if (addToPendingEvalList && !ft.IsDisabled)
                    AddToPendingEvaluateListIfNeeded(ft);
            }

            private void HandleNewFile(SyncFileInfo syncFileInfo)
            {
                var ft = new FileTracker()
                {
                    fileID = syncFileInfo.FileID,
                    fileNameAndRelativePath = syncFileInfo.FileNameAndRelativePath,
                    fileFullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(RootDirFullPath, syncFileInfo.FileNameAndRelativePath)),
                    remoteSyncFileInfo = syncFileInfo,
                };

                fileTrackerByFileIDDictionary[ft.fileID] = ft;
                fileTrackerListSortedByFileID[ft.fileID] = ft;

                maxKnownFileID = Math.Max(maxKnownFileID, ft.fileID);

                AddToPendingEvaluateListIfNeeded(ft);
            }

            private void ProcessIncrementalFileContents(ServerResponse serverResponse)
            {
                var startTime = QpcTimeStamp.Now;
                var syncFileInfo = serverResponse.SyncFileInfo;
                var fileID = syncFileInfo.FileID;
                var ft = fileTrackerByFileIDDictionary.SafeTryGetValue(fileID);

                // note: we do not use the syncFileInfo.SeqNum here as it is only meaningful as part of the SyncFileInfoArray portion of a server repsonse.

                if (!syncFileInfo.Exists)
                {
                    if (ft != null)
                    {
                        var lengthMatches = (ft.localSyncFileInfo.FileLength == (ft.remoteSyncFileInfo?.FileLength ?? 0));
                        var mismatchString = lengthMatches ? string.Empty : $" Length mismatch [local:{ft.localSyncFileInfo.FileLength}, remote:{ft.remoteSyncFileInfo?.FileLength ?? 0}]";

                        Log.Debug.Emit($"remote file {fileID}:'{ft.fileNameAndRelativePath}' has been deleted");

                        ft.remoteSyncFileInfo = syncFileInfo;

                        fileTrackerByFileIDDictionary.Remove(fileID);
                        fileTrackerListSortedByFileID.Remove(fileID);

                        RemoveFromPendingEvaluateListIfNeeded(ft);
                        pendingSendFileTrackerList.Remove(ft);
                    }
                    else
                    {
                        Log.Debug.Emit($"received unexpected indication of deleted file for remote fileID:{syncFileInfo.FileID} [no local file tracker found for this file id]");
                    }
                }
                else if (ft.IsDisabled)
                {
                    Log.Trace.Emit($"Ignoring file content update for disabled file {ft.fileID}:'{ft.fileNameAndRelativePath}'");
                }
                else if (ft.localSyncFileInfo == null)
                {
                    Log.Trace.Emit($"Ignoring data update for {ft.fileID}:'{ft.fileNameAndRelativePath}' which has not been created yet [offset {serverResponse.FileOffset}, length:{serverResponse.DataCount}]");
                }
                else if (serverResponse.Data == null)
                {
                    new System.ArgumentException($"received incremental file update syncFileInfo with null Data [file {syncFileInfo.FileID}:'{ft.fileNameAndRelativePath}']").Throw();     // this takes the connection down
                }
                else if (serverResponse.FileOffset > ft.successfullyTransferredCount && serverResponse.DataCount > 0)
                {
                    Log.Trace.Emit($"Ignoring non-contiguous data update for {ft.fileID}:'{ft.fileNameAndRelativePath}' [arrived offset {serverResponse.FileOffset}, length:{serverResponse.DataCount}]");
                }
                else
                {
                    try
                    {
                        if (serverResponse.DataCount > 0)
                        {
                            var fs = openFileTracker.OpenIfNeeded(ft, this);

                            if (fs.Position != serverResponse.FileOffset)
                                fs.Seek(serverResponse.FileOffset, SeekOrigin.Begin);

                            fs.Write(serverResponse.Data, 0, serverResponse.DataCount);

                            recentlyTouchedFileFullPathSetForPruningManager?.Add(ft.fileFullPath);

                            long endOfWritePosition = serverResponse.FileOffset + serverResponse.DataCount;
                            long numBytesAddedToFile = endOfWritePosition - ft.localSyncFileInfo.FileLength;

                            if (numBytesAddedToFile > 0)
                            {
                                totalTransferredNewBytes += numBytesAddedToFile;

                                if (numBytesAddedToFile < pendingBytes)
                                    pendingBytes -= numBytesAddedToFile;
                                else
                                    recalculatePendingBytes = true;

                                ft.successfullyTransferredCount = endOfWritePosition;
                            }

                            if (fs.Length > ft.successfullyTransferredCount)
                            {
                                openFileTracker.pendingSetLength = ft.successfullyTransferredCount;
                            }
                        }

                        HandleSyncFileInfoForExistingTracker(ft, serverResponse.SyncFileInfo, serverResponse);

                        if (serverResponse.EndOfFileReached)
                        {
                            openFileTracker.OpenIfNeeded(ft, this);

                            var setFileLength = serverResponse.FileOffset + serverResponse.DataCount;

                            openFileTracker.pendingSetLength = setFileLength;
                            ft.successfullyTransferredCount = setFileLength;
                            ft.localSyncFileInfo.FileLength = setFileLength;

                            openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();

                            Log.Trace.Emit("Finished receiving incremental update for {0}:'{1}' length:{2}", ft.fileID, ft.fileNameAndRelativePath, ft.localSyncFileInfo.FileLength);
                            pendingSendFileTrackerList.Remove(ft);

                            if (ft.remoteSyncFileInfo.FileLength != setFileLength)
                            {
                                Log.Debug.Emit($"End of file reported at different position than last reported file length for {ft.fileID}:'{ft.fileNameAndRelativePath}' remoteLen:{ft.remoteSyncFileInfo.FileLength} != setLen:{setFileLength}");
                                AddToPendingEvaluateListIfNeeded(ft);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DisableFileTracker(ft, $"Incremental write to file {ft.fileID}:'{ft.fileNameAndRelativePath}' failed at offset {serverResponse.FileOffset}, length:{serverResponse.DataCount}: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    }
                }

                var runTime = startTime.Age.TotalSeconds;
                var bytes = serverResponse.DataCount;

                Log.Trace.Emit("IncrementalFileContents {0}:'{1}' offset:{2} runTime:{3:f3} bytes:{4} rate:{5:f3} mb/s", fileID, ft?.fileNameAndRelativePath, serverResponse.FileOffset, runTime, bytes, bytes * 0.000001 * runTime.SafeOneOver());
            }

            #endregion

            #region TCP support

            private class Transaction
            {
                public ClientRequest clientRequest;
                public ServerResponse serverResponse;
            }

            private CancellationTokenSource cancellationTokenSource;
            private CancellationToken cancellationToken;

            private System.Collections.Concurrent.BlockingCollection<Transaction[]> postedTransactionSetBuffer;

            private Task tcpClientTask;
            private bool IsConnected { get { return !(tcpClientTask?.IsCompleted ?? true); } }

            private string StartNetworkingIfNeeded(IProviderFacet ipf, bool runPing = true)
            {
                string ec = string.Empty;

                try
                {
                    if (cancellationTokenSource == null)
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                        cancellationToken = cancellationTokenSource.Token;
                    }

                    if (postedTransactionSetBuffer == null)
                    {
                        postedTransactionSetBuffer = new System.Collections.Concurrent.BlockingCollection<Transaction[]>(10);
                    }

                    if (tcpClientTask == null || tcpClientTask.IsCompleted)
                        tcpClientTask = Task.Run(TcpClientTaskMethod);

                    if (runPing)
                    {
                        var pingRequestTransaction = new Transaction() { clientRequest = new ClientRequest() };

                        ec = RunTransactions(ipf, pingRequestTransaction);

                        UpdateLastReceivedNewestSyncInfoSeqNum(pingRequestTransaction);
                    }
                }
                catch (System.Exception ex)
                {
                    ec = $"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}";
                }

                return ec;
            }

            private string RunTransactions(IProviderFacet ipf, params Transaction[] transactionParamsArray)
            {
                if (transactionParamsArray.IsNullOrEmpty())
                    return string.Empty;

                string ec = string.Empty;

                try
                {
                    if (ec.IsNullOrEmpty() && !IsConnected)
                        ec = "Client is not connected";

                    if (ec.IsNullOrEmpty())
                        postedTransactionSetBuffer.Add(transactionParamsArray);

                    while (ec.IsNullOrEmpty())
                    {
                        WaitForSomethingToDo();
                        InnerServiceBackground(QpcTimeStamp.Now, allowStartNewTransactions: false);

                        if (transactionParamsArray.LastOrDefault()?.serverResponse != null)
                            break;

                        if (cancellationToken.IsCancellationRequested)
                            ec = "Cancel requested (token)";
                        else if ((ipf ?? CurrentAction)?.IsCancelRequestActive == true)
                            ec = "Cancel requested (action)";
                        else if (HasStopBeenRequested)
                            ec = "Part stop requested";
                        else if (!IsConnected)
                            ec = "Connection lost";
                    }
                }
                catch (System.Exception ex)
                {
                    ec = $"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}";
                }

                if (ec.IsNeitherNullNorEmpty())
                {
                    try
                    {
                        postedTransactionSetBuffer.TryTake(out Transaction[] abandonedTransactionsArray, 100, cancellationToken);
                    }
                    catch { }
                }

                return ec;
            }

            private void StopAndReleaseNetworking()
            {
                try
                {
                    cancellationTokenSource?.Cancel();

                    try
                    {
                        if (tcpClientTask?.IsCompleted == false)
                            tcpClientTask?.Wait();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Trace.Emit($"{CurrentMethodName}: tcpClientTask Wait generated exception: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    }

                    try
                    {
                        if (postedTransactionSetBuffer?.IsAddingCompleted == false)
                            postedTransactionSetBuffer.CompleteAdding();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Trace.Emit($"{CurrentMethodName}: postedTransactionSetBuffer CompleteAdding generated exception: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    }

                    Fcns.DisposeOfObject(ref tcpClientTask);
                    Fcns.DisposeOfObject(ref postedTransactionSetBuffer);

                    Fcns.DisposeOfObject(ref cancellationTokenSource);
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit($"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }
                finally
                {
                    tcpClientTask = default;
                    postedTransactionSetBuffer = null;
                    cancellationTokenSource = default;
                    cancellationToken = default;
                }
            }

            private async Task TcpClientTaskMethod()
            {
                long clientSeqNumSource = 0;

                try
                {
                    var selectedAddressFamily = (Config.UseIPV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);

                    using (var tcpClient = new TcpClient(selectedAddressFamily)
                    {
                        SendTimeout = (int)Config.TcpClientReceiveAndSendTimeout.TotalMilliseconds,
                        ReceiveTimeout = (int)Config.TcpClientReceiveAndSendTimeout.TotalMilliseconds,
                        LingerState = new LingerOption(true, 10),
                    })
                    {
                        var ipAddresses = await System.Net.Dns.GetHostAddressesAsync(Config.HostName);
                        var filteredIPAddresses = ipAddresses.Where(ipa => (ipa.AddressFamily == selectedAddressFamily));
                        var firstAddress = filteredIPAddresses.FirstOrDefault();

                        if (firstAddress == null)
                        {
                            string mesg = $"Cannot connect to '{Config.HostName}':{Config.Port} using {selectedAddressFamily}: there are no addresses of this type for this host";
                            Log.Debug.Emit(mesg);
                            new System.ArgumentException(mesg).Throw();
                        }

                        var ipEndpoint = new System.Net.IPEndPoint(firstAddress, Config.Port);
                        tcpClient.Connect(ipEndpoint);

                        using (var tcpStream = tcpClient.GetStream())
                        {
                            var blockReadHelper = new MPLengthPrefixedBlockReader(65536 + 512, cancellationToken);

                            while (!cancellationToken.IsCancellationRequested)
                            {
                                if (!postedTransactionSetBuffer.TryTake(out Transaction[] transactionSet, 100, cancellationToken) || transactionSet == null)
                                    continue;

                                QpcTimeStamp setStartTime = QpcTimeStamp.Now;

                                foreach (var t in transactionSet)
                                {
                                    if (t.clientRequest.ClientSeqNum == 0)
                                        t.clientRequest.ClientSeqNum = ++clientSeqNumSource;
                                }

                                using (var writeTask = WriteClientRequestsAsync(tcpStream, transactionSet))
                                {
                                    foreach (var transaction in transactionSet)
                                    {
                                        var clientRequest = transaction.clientRequest;
                                        await blockReadHelper.ReadNextLengthPrefixedBlockAsync(tcpStream);
                                        var serverResponse = ExtractServerResponse(blockReadHelper.buffer, blockReadHelper.bufferCount);

                                        if (clientRequest.ClientSeqNum != serverResponse.ClientSeqNum)
                                            new System.ArgumentException($"ServerResponse is out of order [{clientRequest.ClientSeqNum} != {serverResponse.ClientSeqNum}]").Throw();

                                        transaction.serverResponse = serverResponse;
                                    }

                                    writeTask.Wait(cancellationToken); // this should be a noop since each client request must have been written and processed to get to this point.
                                }

                                double runTime = setStartTime.Age.TotalSeconds;
                                int totalBytesRead = transactionSet.Sum(t => t.serverResponse?.DataCount ?? 0);
                                Log.Trace.Emit("TransactionSet received count:{0} runTime:{1:f3} bytes:{2} rate:{3:f3} mb/s", transactionSet.Length, runTime, totalBytesRead, totalBytesRead * 0.000001 * runTime.SafeOneOver());

                                Notify();   // notify the part that a transaction set has been completed

                                Config.PerTranscationSetRateLimitSleepPeriod.SleepIfNotNull();
                            }

                            await tcpStream.FlushAsync();

                            tcpClient.Client.Shutdown(SocketShutdown.Send);

                            (0.1).FromSeconds().Sleep();
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                        Log.Trace.Emit($"During cancellation {CurrentMethodName} ended with exception: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                    else
                        Log.Debug.Emit($"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ServerResponse ExtractServerResponse(byte[] rxByteArray, int currentLen)
            {
                var mpReader = new MessagePackReader(new ReadOnlyMemory<byte>(rxByteArray, 0, currentLen));
                return MessagePackSerializer.Deserialize<ServerResponse>(ref mpReader);
            }

            private readonly BufferWriter.ByteArrayBufferWriter byteArrayBufferWriter = new BufferWriter.ByteArrayBufferWriter(4096);
            private readonly BufferWriter.ByteArrayBufferWriter innerByteArrayBufferWriter = new BufferWriter.ByteArrayBufferWriter(128);

            private Task WriteClientRequestsAsync(NetworkStream tcpStream, Transaction[] transactionSet)
            {
                byteArrayBufferWriter.ResetCount();

                var mpWriter = new MessagePackWriter(byteArrayBufferWriter);

                foreach (var transaction in transactionSet)
                {
                    innerByteArrayBufferWriter.ResetCount();
                    var innerMPWriter = new MessagePackWriter(innerByteArrayBufferWriter);

                    MessagePackSerializer.Serialize(ref innerMPWriter, transaction.clientRequest);
                    innerMPWriter.Flush();

                    mpWriter.WriteInt32(innerByteArrayBufferWriter.CurrentCount);
                    mpWriter.WriteRaw(new ReadOnlySequence<byte>(innerByteArrayBufferWriter.ByteArray, 0, innerByteArrayBufferWriter.CurrentCount));
                }

                mpWriter.WriteInt32(0); // tell the server that we are done with this transaction set.

                mpWriter.Flush();

                return tcpStream.WriteAsync(byteArrayBufferWriter.ByteArray, 0, byteArrayBufferWriter.CurrentCount, cancellationToken);
            }

            #endregion
        }
    }

    namespace Server
    {
        public class TCPFileSyncServerConfig : ICopyable<TCPFileSyncServerConfig>
        {
            public TCPFileSyncServerConfig(string partID)
            {
                PartID = partID;
            }

            public string PartID { get; private set; }
            public string RootDirPath { get; set; } = ".";
            public bool CreateRootDirPathIfNeeded { get; set; } = true;
            public string FileFilterSpec { get; set; } = "*.*";
            public TimeSpan RescanForChangesInterval { get; set; } = (30.0).FromSeconds();
            public int FSWInternalBufferSize { get; set; } = 65536;
            public bool IncludeSubdirectories { get; set; } = true;
            public int Port { get; set; } = Common.Constants.DefaultTCPPort;
            public bool UseIPV6 { get; set; } = false;
            public int AcceptBacklog { get; set; } = 10;
            public TimeSpan TcpClientReceiveAndSendTimeout { get; set; } = (5.0).FromMinutes();
            public IValuesInterconnection IVI { get; set; }

            /// <summary>Defines the ActionLoggingConfig that is used for part's external actions</summary>
            public ActionLoggingConfig ActionLoggingConfig { get; set; } = ActionLoggingConfig.Debug_Debug_Trace_Trace.Update(actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);
            public Logging.LogGate InitialInstanceLogGate { get; set; } = Logging.LogGate.Debug;

            /// <summary>Defines the ActionLoggingConfig that is used for part's internal actions</summary>
            public ActionLoggingConfig InternalActionLoggingConfig { get; set; } = ActionLoggingConfig.Trace_Trace_Trace_Trace.Update(actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);

            public TimeSpan? PerTranscationSetRateLimitSleepPeriod { get; set; } = (0.001).FromSeconds();     // or null, or TimeSpan.Zero

            public TCPFileSyncServerConfig MakeCopyOfThis(bool deepCopy = true)
            {
                var copy = (TCPFileSyncServerConfig)MemberwiseClone();
                copy.IVI = IVI ?? Values.Instance;
                return copy;
            }
        }

        public class TCPFileSyncServer : SimpleActivePartBase, ISyncActionFactory
        {
            #region Construction, Release, PerformGoOnlineActionEx, PerformGoOfflineAction, PerformMainLoopService (et. al.)

            public TCPFileSyncServer(TCPFileSyncServerConfig config)
                : base(config.PartID, SimpleActivePartBaseSettings.DefaultVersion2.Build(partBaseIVI: config.IVI, disableBusyBehavior: true, waitTimeLimit: (0.2).FromSeconds(), initialInstanceLogGate: config.InitialInstanceLogGate))
            {
                Config = config.MakeCopyOfThis();
                RootDirFullPath = System.IO.Path.GetFullPath(Config.RootDirPath);

                if (Config.CreateRootDirPathIfNeeded && !System.IO.Directory.Exists(RootDirFullPath))
                    System.IO.Directory.CreateDirectory(RootDirFullPath);

                ActionLoggingReference.Config = Config.ActionLoggingConfig;
                InternalActionLoggingReference = new ActionLogging(Log, Config.InternalActionLoggingConfig);

                AddMainThreadStoppingAction(() => Release());

                ivaMBytesPerSec = Config.IVI.GetValueAccessor($"{PartID}.MBytesPerSec").Set(0.0);
                ivaTransactionsPerSec = Config.IVI.GetValueAccessor($"{PartID}.TransactionsPerSec").Set(0.0);
            }

            private TCPFileSyncServerConfig Config { get; set; }
            private readonly string RootDirFullPath;
            private ActionLogging InternalActionLoggingReference { get; set; }

            private readonly IValueAccessor ivaMBytesPerSec, ivaTransactionsPerSec;
            private readonly MosaicLib.Utils.Tools.EventRateTool mBytesPerSecTool = new MosaicLib.Utils.Tools.EventRateTool();
            private readonly MosaicLib.Utils.Tools.EventRateTool transactionsPerSecTool = new MosaicLib.Utils.Tools.EventRateTool();

            private void Release()
            {
                ReleaseDirectoryTracking();
                ReleaseNetworking();
            }

            protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
            {
                ReleaseDirectoryTracking();
                if (andInitialize)
                    ReleaseNetworking();

                // note that file tracking support can operate even if the FileSystemWatcher cannot be created.
                try
                {
                    fsw = new FileSystemWatcher()
                    {
                        Path = RootDirFullPath,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                        Filter = Config.FileFilterSpec,
                        IncludeSubdirectories = Config.IncludeSubdirectories,
                        EnableRaisingEvents = false,
                        InternalBufferSize = 65536, // defaults to 8192
                    };

                    fsw.Changed += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); Log.Trace.Emit($"FSW.Changed '{e.Name}'"); Notify(); };
                    fsw.Created += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); Log.Trace.Emit($"FSW.Created '{e.Name}'"); Notify(); };
                    fsw.Deleted += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); Log.Trace.Emit($"FSW.Deleted '{e.Name}'"); Notify(); };
                    fsw.Error += (o, e) => { asyncErrorCount++; Notify(); Log.Debug.Emit("FSW.Error: Received unexpected FileSystemWatcher Error Event: {0}", e); };
                    fsw.Renamed += (o, e) => { asyncTouchedFileNamesSet.Add(e.Name); asyncTouchedFileNamesSet.Add(e.OldName); Log.Trace.Emit($"FSW.Renamed '{e.Name}' [from '{e.OldName}']"); Notify(); };
                }
                catch (System.Exception ex)
                {
                    Fcns.DisposeOfObject(ref fsw);
                    Log.Debug.Emit($"Unable to construct a usable FileSystemWatcher for '{RootDirFullPath}': {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                }

                if (Config.RescanForChangesInterval > TimeSpan.Zero)
                    rescanIntervalTimer = new QpcTimer() { TriggerInterval = Config.RescanForChangesInterval, AutoReset = true }.Start();

                ServiceFileTracking(forceFullUpdate: true);

                string ec = StartNetworkingIfNeeded();

                return ec;
            }

            protected override string PerformGoOfflineAction(IProviderActionBase action)
            {
                Release();
                ResetDirectoryInfoAndStopFSW();

                return base.PerformGoOfflineAction(action);
            }

            protected override void PerformMainLoopService()
            {
                base.PerformMainLoopService();

                if (BaseState.IsOnline)
                {
                    ServiceFileTracking();
                }

                ServiceNetworking();
            }

            #endregion

            #region Sync support

            public IClientFacet Sync(TimeSpan nominalWaitTimeLimit = default, SyncFlags syncFlags = default)
            {
                return new BasicActionImpl(ActionQueue, (ipf) => PerformSync(ipf, nominalWaitTimeLimit, syncFlags), "Sync", ActionLoggingReference, mesgDetails: (nominalWaitTimeLimit.IsZero() ? $"{syncFlags}" : $"timeLimit:{nominalWaitTimeLimit.TotalSeconds:f3}s, {syncFlags}"));
            }

            private string PerformSync(IProviderFacet ipf, TimeSpan nominalWaitTimeLimit, SyncFlags syncFlags)
            {
                ServiceFileTracking(forceFullUpdate: syncFlags.IsSet(SyncFlags.Full));

                return string.Empty;
            }

            #endregion

            #region File tracking support logic

            private void ReleaseDirectoryTracking()
            {
                Fcns.DisposeOfObject(ref fsw);
            }

            private System.IO.FileSystemWatcher fsw;

            private class AsyncTouchedFileNamesSet
            {
                public object mutex = new object();
                public HashSet<string> set = new HashSet<string>();
                public volatile int setCount = 0;

                public void Add(string name) { lock (mutex) { set.Add(name); setCount = set.Count; } }
                public void Clear() { lock (mutex) { set.Clear(); setCount = set.Count; } }
                public void TransferContentsTo(HashSet<string> other)
                {
                    lock (mutex)
                    {
                        other.UnionWith(set);
                        set.Clear();
                        setCount = set.Count;
                    }
                }
            }
            private readonly AsyncTouchedFileNamesSet asyncTouchedFileNamesSet = new AsyncTouchedFileNamesSet();
            private volatile int asyncErrorCount;

            private readonly HashSet<string> pendingFileNameAndRelativePathSet = new HashSet<string>();

            private long fileIDGen; // use pre-increment as generate method.  FileID zero is reserved (aka is not used)
            private long syncFileInfoSeqNumGen;     // use pre-increment as generate method.  SyncFileInfoSeqNum zero is reserved (aka is not used)
            private readonly IDictionaryWithCachedArrays<string, FileTracker> fileTrackerByNameDictionary = new IDictionaryWithCachedArrays<string, FileTracker>();
            private readonly HashSet<FileTracker> pendingEvaluateFileTrackerSet = new HashSet<FileTracker>();
            private readonly IListWithCachedArray<FileTracker> pendingEvaluateFileTrackerList = new IListWithCachedArray<FileTracker>();

            private void AddToPendingEvaluateListIfNeeded(FileTracker ft)
            {
                if (ft != null && !pendingEvaluateFileTrackerSet.Contains(ft))
                {
                    pendingEvaluateFileTrackerList.Add(ft);
                    pendingEvaluateFileTrackerSet.Add(ft);
                }
            }

            private void RemoveFromPendingEvaluateListIfNeeded(FileTracker ft)
            {
                if (pendingEvaluateFileTrackerSet.Contains(ft))
                {
                    pendingEvaluateFileTrackerList.Remove(ft);
                    pendingEvaluateFileTrackerSet.Remove(ft);
                }
            }

            #region shared data - syncFileInfo related data that is maintained by parts primary thread and which is concurrently accessed on TcpClient Task threads.

            private readonly object syncFileInfoMutex = new object();
            private readonly SyncFileInfoSortedArray syncFileInfoSortedArray = new SyncFileInfoSortedArray();
            private readonly IDictionaryWithCachedArrays<long, SyncFileInfoPair> syncFileInfoPairDictionary = new IDictionaryWithCachedArrays<long, SyncFileInfoPair>();

            private struct SyncFileInfoPair
            {
                public SyncFileInfo item;
                public SyncFileInfo itemNoName;
            }

            private long lastAssignedSyncFileInfoSeqNum = 0;
            private volatile uint lastAssignedSyncFileInfoSeqNumSeqNumU4 = 0;

            private void NoteNewSyncFileInfoSeqNumAssigned(long seqNum)
            {
                lastAssignedSyncFileInfoSeqNum = seqNum;
                lastAssignedSyncFileInfoSeqNumSeqNumU4 = unchecked((uint)seqNum);
            }

            private AtomicInt32 byteTransferCounter, transactionCounter;
            private int lastByteTransferCounter, lastTransactionCounter;

            #endregion

            private class FileTracker
            {
                public long fileID;
                public string fileNameAndRelativePath;
                public string fileNameFromConfiguredPath;
                public string fullPath;
                public long seqNum;
                public FileInfo fileInfo;
                public Common.SyncFileInfo syncFileInfo;

                public string faultCode;
            }

            private void ResetDirectoryInfoAndStopFSW()
            {
                if (fsw != null)
                    fsw.EnableRaisingEvents = false;

                asyncTouchedFileNamesSet.Clear();
                asyncErrorCount = 0;

                fileTrackerByNameDictionary.ValueArray.DoForEach(ft => AddToPendingEvaluateListIfNeeded(ft));

                ignoreFileNameAndRelativePathSet.Clear();
            }

            private readonly HashSet<string> newFileNameAndRelativePathSet = new HashSet<string>();
            private readonly HashSet<string> ignoreFileNameAndRelativePathSet = new HashSet<string>();

            private bool fullUpdateNeeded = true;
            private bool rescanDirectoryNeeded = true;
            private QpcTimer rescanIntervalTimer;

            private int ServiceFileTracking(QpcTimeStamp qpcTimeStamp = default, bool forceFullUpdate = false)
            {
                qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

                long entrySyncFileInfoSeqNumGen = syncFileInfoSeqNumGen;

                fullUpdateNeeded |= forceFullUpdate || (asyncErrorCount > 0);
                rescanDirectoryNeeded |= fullUpdateNeeded | rescanIntervalTimer.GetIsTriggered(qpcTimeStamp);

                if (fullUpdateNeeded)
                {
                    ResetDirectoryInfoAndStopFSW();
                    fullUpdateNeeded = false;
                }
                else if (asyncTouchedFileNamesSet.setCount > 0)
                {
                    asyncTouchedFileNamesSet.TransferContentsTo(pendingFileNameAndRelativePathSet);
                }

                if (pendingFileNameAndRelativePathSet.Count > 0)
                {
                    foreach (var fileNameAndRelativePath in pendingFileNameAndRelativePathSet)
                    {
                        if (fileTrackerByNameDictionary.TryGetValue(fileNameAndRelativePath, out FileTracker ft) && ft != null)
                            AddToPendingEvaluateListIfNeeded(ft);
                        else
                            newFileNameAndRelativePathSet.Add(fileNameAndRelativePath);
                    }

                    pendingFileNameAndRelativePathSet.Clear();
                }

                if (rescanDirectoryNeeded)
                {
                    if (fsw != null && !fsw.EnableRaisingEvents)
                        fsw.EnableRaisingEvents = true;

                    // mark all of the files as touched (since we are going to enumerate them explicitly and then mark all the ones that were not found as touched - this is quicker.
                    fileTrackerByNameDictionary.ValueArray.DoForEach(ft => AddToPendingEvaluateListIfNeeded(ft));

                    foreach (var scanFileNameAndPath in System.IO.Directory.EnumerateFiles(RootDirFullPath, Config.FileFilterSpec, Config.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    {
                        var scanFileNameAndRelativePath = scanFileNameAndPath.MakeRelativePath(RootDirFullPath);
                        if (Config.IncludeSubdirectories && System.IO.Directory.Exists(scanFileNameAndPath))
                            continue;

                        if (!ignoreFileNameAndRelativePathSet.Contains(scanFileNameAndRelativePath) && (!fileTrackerByNameDictionary.TryGetValue(scanFileNameAndRelativePath, out FileTracker ft) || ft == null))
                        {
                            newFileNameAndRelativePathSet.Add(scanFileNameAndRelativePath);
                        }
                    }

                    rescanDirectoryNeeded = false;
                }

                if (newFileNameAndRelativePathSet.Count > 0)
                {
                    foreach (var newFileNameAndRelativePath in newFileNameAndRelativePathSet)
                    {
                        try
                        {
                            var fileNameFromConfiguredPath = System.IO.Path.Combine(Config.RootDirPath, newFileNameAndRelativePath);
                            var fullPath = System.IO.Path.Combine(RootDirFullPath, newFileNameAndRelativePath);

                            if (!System.IO.Directory.Exists(fullPath))
                            {
                                var ft = new FileTracker()
                                {
                                    fileID = ++fileIDGen,
                                    seqNum = 0,
                                    fileNameAndRelativePath = newFileNameAndRelativePath,
                                    fileNameFromConfiguredPath = fileNameFromConfiguredPath,
                                    fullPath = fullPath,
                                };

                                fileTrackerByNameDictionary[newFileNameAndRelativePath] = ft;
                                AddToPendingEvaluateListIfNeeded(ft);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ignoreFileNameAndRelativePathSet.Add(newFileNameAndRelativePath);
                            Log.Debug.Emit($"Unexpected issue generating FileTracker for '{newFileNameAndRelativePath}': {ex.ToString(ExceptionFormat.TypeAndMessage)} [added to ignore list]");
                        }
                    }

                    newFileNameAndRelativePathSet.Clear();
                }

                if (pendingEvaluateFileTrackerList.Count > 0)
                {
                    var pendingFTArray = pendingEvaluateFileTrackerList.Array;

                    pendingEvaluateFileTrackerList.Clear();
                    pendingEvaluateFileTrackerSet.Clear();

                    foreach (var ft in pendingFTArray)
                    {
                        try
                        {
                            var sysFileInfo = new FileInfo(ft.fullPath);

                            if (sysFileInfo.Exists)     // it might have been deleted/pruned since it was added here.
                            {
                                if (!ft.fileInfo.IsEqual(sysFileInfo))
                                {
                                    var entrySyncFileInfo = ft.syncFileInfo;
                                    var entryFileLen = entrySyncFileInfo?.FileLength ?? 0;
                                    var entrySyncFileInfoSeqNum = entrySyncFileInfo?.SeqNum ?? 0;
                                    var entryFTSeqNum = ft.seqNum;

                                    ft.seqNum = ++syncFileInfoSeqNumGen;
                                    ft.fileInfo = sysFileInfo;
                                    var syncFileInfo = new SyncFileInfo(sysFileInfo, ft.seqNum, ft.fileID, ft.fileNameAndRelativePath);
                                    var syncFileInfoNoName = syncFileInfo.MakeCopyWithoutFileName();

                                    Log.Trace.Emit("SyncFileInfo for {0}:'{1}' changed to {2} [from {3}]", ft.fileID, ft.fileNameAndRelativePath, syncFileInfo, entrySyncFileInfo);

                                    lock (syncFileInfoMutex)
                                    {
                                        if (entrySyncFileInfoSeqNum != default)
                                            syncFileInfoSortedArray.Remove(entrySyncFileInfoSeqNum);

                                        syncFileInfoSortedArray.Add(syncFileInfo, syncFileInfoNoName);

                                        syncFileInfoPairDictionary[syncFileInfo.FileID] = new SyncFileInfoPair() { item = syncFileInfo, itemNoName = syncFileInfoNoName };

                                        ft.syncFileInfo = syncFileInfo;

                                        NoteNewSyncFileInfoSeqNumAssigned(ft.seqNum);
                                    }
                                }
                            }
                            else
                            {
                                Log.Debug.Emit($"File '{ft.fileNameAndRelativePath}' has been removed");
                                ft.faultCode = null;

                                fileTrackerByNameDictionary.Remove(ft.fileNameAndRelativePath);

                                var syncFileInfo = ft.syncFileInfo;
                                if (syncFileInfo != null)
                                {
                                    lock (syncFileInfoMutex)
                                    {
                                        syncFileInfoSortedArray.Remove(syncFileInfo.SeqNum);
                                        syncFileInfoPairDictionary.Remove(syncFileInfo.FileID);
                                    }

                                    NoteNewSyncFileInfoSeqNumAssigned(++syncFileInfoSeqNumGen);
                                }
                            }

                            if (ft.faultCode.IsNeitherNullNorEmpty())
                            {
                                Log.Debug.Emit($"Previous issue with file '{ft.fileNameAndRelativePath}' has been resolved [{ft.faultCode}]");
                                ft.faultCode = string.Empty;
                                ft.seqNum = ++syncFileInfoSeqNumGen;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ft.faultCode = $"Rescan failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}";
                            Log.Debug.Emit($"Issue with file '{ft.fileNameAndRelativePath}': {ft.faultCode}");
                            ft.seqNum = ++syncFileInfoSeqNumGen;
                        }
                    }
                }

                return (syncFileInfoSeqNumGen != entrySyncFileInfoSeqNumGen).MapToInt();
            }

            #endregion

            #region TCP support

            private CancellationTokenSource cancellationTokenSource;
            private CancellationToken cancellationToken;

            private System.Net.Sockets.TcpListener tcpListener;
            private Task<System.Net.Sockets.TcpClient> tcpAcceptTask;
            private readonly DisposableList<Task> tcpClientTaskList = new DisposableList<Task>();

            private string StartNetworkingIfNeeded()
            {
                string ec = string.Empty;

                try
                {
                    if (cancellationTokenSource == null)
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                        cancellationToken = cancellationTokenSource.Token;
                    }

                    if (tcpListener == null)
                    {
                        var ipEndPoint = new System.Net.IPEndPoint(Config.UseIPV6 ? System.Net.IPAddress.IPv6Any : System.Net.IPAddress.Any, Config.Port);

                        tcpListener = new System.Net.Sockets.TcpListener(ipEndPoint);
                        tcpListener.Start(Config.AcceptBacklog);

                        ec = StartNextAccept();
                    }
                }
                catch (System.Exception ex)
                {
                    ec = $"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}";
                }

                return ec;
            }

            private string StartNextAccept()
            {
                string ec = string.Empty;

                try
                {
                    tcpAcceptTask = tcpListener.AcceptTcpClientAsync();
                }
                catch (System.Exception ex)
                {
                    ec = $"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}";
                }

                return ec;
            }

            private void ReleaseNetworking()
            {
                try
                {
                    try
                    {
                        tcpListener?.Stop();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Trace.Emit($"{CurrentMethodName}: tcpListener Stop generated exception: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    }

                    cancellationTokenSource?.Cancel();

                    try
                    {
                        tcpAcceptTask?.Wait();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Trace.Emit($"{CurrentMethodName}: tcpAcceptTask Wait generated exception: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    }

                    Fcns.DisposeOfObject(ref tcpListener);
                    Fcns.DisposeOfObject(ref tcpAcceptTask);

                    foreach (var task in tcpClientTaskList)
                    {
                        try
                        {
                            task?.Wait();
                        }
                        catch (System.Exception ex)
                        {
                            Log.Trace.Emit($"{CurrentMethodName}: tcp client task Wait generated exception: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                        }
                    }

                    tcpClientTaskList.Dispose();

                    Fcns.DisposeOfObject(ref cancellationTokenSource);
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit($"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }
                finally
                {
                    tcpListener = default;
                    tcpAcceptTask = default;
                    tcpClientTaskList.Clear();
                    cancellationTokenSource = default;
                    cancellationToken = default;
                }
            }

            private void ServiceNetworking()
            {
                try
                {
                    if (tcpAcceptTask != null)
                    {
                        if (tcpAcceptTask.IsCompleted)
                        {
                            var tcpClient = tcpAcceptTask.Result;

                            if (tcpClient != null)
                                tcpClientTaskList.Add(Task.Run(() => ServiceTcpClient(tcpClient)));

                            Fcns.DisposeOfObject(ref tcpAcceptTask);
                        }
                        else if (tcpAcceptTask.IsFaulted)
                        {
                            Log.Debug.Emit("AcceptTask failed: {0}", tcpAcceptTask.Exception.ToString(ExceptionFormat.TypeAndMessage));
                            Fcns.DisposeOfObject(ref tcpAcceptTask);
                        }
                        else if (tcpAcceptTask.IsCanceled)
                        {
                            Log.Debug.Emit("AcceptTask was cancelled");
                            Fcns.DisposeOfObject(ref tcpAcceptTask);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Debug.Emit($"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }

                if (tcpAcceptTask == null && BaseState.IsOnlineOrAttemptOnline)
                {
                    string ec = StartNextAccept();
                    if (ec.IsNeitherNullNorEmpty())
                    {
                        Log.Debug.Emit(ec);
                        tcpAcceptTask = new TaskFactory<System.Net.Sockets.TcpClient>(cancellationToken).StartNew(() => { (5.0).FromSeconds().Sleep(); return null; }); // create a stub accept task that acts as a holdoff for quick messages.
                    }
                }

                //service the average rate publication
                {
                    var capturedByteTransferCounter = byteTransferCounter.VolatileValue;
                    var bytesTransferDiff = capturedByteTransferCounter - lastByteTransferCounter;

                    lastByteTransferCounter = capturedByteTransferCounter;

                    mBytesPerSecTool.NoteEventHappened(bytesTransferDiff * 0.000001);
                    if (mBytesPerSecTool.HasNewAvgRate)
                        ivaMBytesPerSec.Set(mBytesPerSecTool.AvgRate);

                    var capturedTransactionCounter = transactionCounter.VolatileValue;
                    var transactionsDiff = capturedTransactionCounter - lastTransactionCounter;

                    lastTransactionCounter = capturedTransactionCounter;

                    transactionsPerSecTool.NoteEventHappened(transactionsDiff);
                    if (transactionsPerSecTool.HasNewAvgRate)
                        ivaTransactionsPerSec.Set(transactionsPerSecTool.AvgRate);
                }
            }

            private static readonly ArrayPool<byte> staticArrayPool = ArrayPool<byte>.Create();

            private struct TcpClientState : IDisposable
            {
                public void Setup(TcpClient tcpClient, CancellationToken cancellationToken)
                {
                    tcpStream = tcpClient.GetStream();

                    blockReadHelper = new MPLengthPrefixedBlockReader(4096, cancellationToken);
                    byteArrayBufferWriter = new ByteArrayBufferWriter(4096);
                    lenPrefixByteArrayBufferWriter = new ByteArrayBufferWriter(32);
                }

                public NetworkStream tcpStream;
                public MPLengthPrefixedBlockReader blockReadHelper;

                public BufferWriter.ByteArrayBufferWriter byteArrayBufferWriter;
                public BufferWriter.ByteArrayBufferWriter lenPrefixByteArrayBufferWriter;

                public long currentFileID;
                public long currentFileOffset;
                public System.IO.FileStream currentFileStream;

                public void CloseFileIfNeeded()
                {
                    if (currentFileStream != null)
                    {
                        currentFileID = -1;
                        currentFileOffset = -1;
                        currentFileStream.Flush();
                        currentFileStream.Close();
                        Fcns.DisposeOfObject(ref currentFileStream);
                    }
                }

                public void Dispose()
                {
                    currentFileID = -1;
                    currentFileOffset = -1;
                    Fcns.DisposeOfObject(ref currentFileStream);
                    Fcns.DisposeOfObject(ref byteArrayBufferWriter);
                    Fcns.DisposeOfObject(ref lenPrefixByteArrayBufferWriter);
                    Fcns.DisposeOfObject(ref tcpStream);
                }
            }

            private void ServiceTcpClient(TcpClient tcpClient)
            {
                string clientName = $"TcpClient({tcpClient.Client.RemoteEndPoint})";

                tcpClient.ReceiveTimeout = (int) Config.TcpClientReceiveAndSendTimeout.Milliseconds;
                tcpClient.SendTimeout = (int)Config.TcpClientReceiveAndSendTimeout.Milliseconds;

                bool stopRequested = false;
                System.Exception stoppedByException = null;
                TcpClientState state = default;
                Task responseWriteTask = null;

                try // part of try/finally
                {
                    state.Setup(tcpClient, cancellationToken);

                    Common.ServerResponse serverResponse = new Common.ServerResponse();

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // recieve client request
                        state.blockReadHelper.ReadNextLengthPrefixedBlockAsync(state.tcpStream).Wait();

                        if (cancellationToken.IsCancellationRequested)
                            break;

                        byteTransferCounter.Add(state.blockReadHelper.lenPrefixBufferCount + state.blockReadHelper.bufferCount);

                        if (state.blockReadHelper.blockLen == 0)
                        {
                            // this is the marker for the end of a transaction set (as viewed by the client).  Close any still open file and perform the per transaction set rate limit sleep (if needed)
                            state.CloseFileIfNeeded();

                            Config.PerTranscationSetRateLimitSleepPeriod.SleepIfNotNull();

                            continue;
                        }

                        QpcTimeStamp startTime = QpcTimeStamp.Now;

                        var clientRequest = ExtractClientRequest(state);

                        serverResponse.Clear();
                        serverResponse.ClientSeqNum = clientRequest.ClientSeqNum;

                        // decode client request and generate response

                        GenerateServerResponse(clientRequest, serverResponse, staticArrayPool, ref state);

                        if (serverResponse.ResultCode.IsNeitherNullNorEmpty())
                            Log.Debug.Emit($"{clientName}: {serverResponse.ResultCode}");

                        // wait for the prior posted write to complete.  
                        // It is writing from the buffer in the byteArrayBufferWriter so we can safely pre-generate the next ServerResponse before waiting for the prior write task to complete
                        responseWriteTask?.Wait(cancellationToken);
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        // serialize and post write for response.
                        responseWriteTask = WriteResponse(serverResponse, ref state);

                        // return any allocated file content Data buffer to the array pool
                        if (serverResponse.Data != null)
                        {
                            staticArrayPool.Return(serverResponse.Data);
                            serverResponse.Data = null;
                        }

                        double runTime = startTime.Age.TotalSeconds;
                        Log.Trace.Emit("ClientRequest: seqNum:{0} fileID:{1} syncArrayLen:{2} offset:{3} numBytes:{4} runTime:{5:f3} rate:{6:f3} mb/s", clientRequest.ClientSeqNum, clientRequest.FileID, serverResponse.SyncFileInfoArray.SafeLength(), serverResponse.FileOffset, serverResponse.DataCount, runTime, serverResponse.DataCount * 0.000001 * runTime.SafeOneOver());

                        if (clientRequest.ClientSeqNum < 0)
                        {
                            Log.Debug.Emit($"{clientName}: Closing by request");
                            stopRequested = true;

                            break;
                        }
                    }

                    responseWriteTask?.Wait(cancellationToken);

                    tcpClient.Client.Shutdown(SocketShutdown.Both);
                }
                catch (System.Exception ex)
                {
                    stoppedByException = ex;

                    if (!cancellationToken.IsCancellationRequested)
                        Log.Debug.Emit($"{clientName}: client service task stopped by exception: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    else
                        Log.Debug.Emit($"{clientName}: client service task stopped by cancellation with exception: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                }
                finally
                {
                    if (cancellationToken.IsCancellationRequested && !stopRequested && stoppedByException == null)
                        Log.Debug.Emit($"{clientName}: client service task stopped by cancellation request");

                    responseWriteTask?.Wait(10);

                    state.Dispose();

                    (0.010).FromSeconds().Sleep();

                    tcpClient.Close();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ClientRequest ExtractClientRequest(TcpClientState state)
            {
                var mpReader = new MessagePackReader(new ReadOnlySequence<byte>(state.blockReadHelper.buffer, 0, state.blockReadHelper.bufferCount));

                return MessagePackSerializer.Deserialize<Common.ClientRequest>(ref mpReader);
            }

            private void SyncIfNeeded(ClientRequest clientRequest)
            {
                if (clientRequest.SyncRequest && clientRequest.RequestFirstSyncFileInfoSeqNumU4 != lastAssignedSyncFileInfoSeqNumSeqNumU4)
                {
                    var icf = new BasicActionImpl(ActionQueue, () => { ServiceFileTracking(); return string.Empty; }, "InternalSync", InternalActionLoggingReference);
                    string ec = icf.Run();
                    if (ec.IsNeitherNullNorEmpty())
                        new System.InvalidOperationException($"{CurrentMethodName} failed: {icf.ToString(ToStringSelect.MesgDetailAndState)}").Throw();
                }
            }

            private void GenerateIncrementalFileContentResponse(ClientRequest clientRequest, ServerResponse serverResponse)
            {
                SyncIfNeeded(clientRequest);

                lock (syncFileInfoMutex)
                {
                    var currentSeqNum = lastAssignedSyncFileInfoSeqNum;
                    serverResponse.NewestSyncFileInfoSeqNum = currentSeqNum;
                }
            }

            private void GenerateServerResponse(ClientRequest clientRequest, ServerResponse serverResponse, ArrayPool<byte> dataBufferArrayPool, ref TcpClientState state)
            {
                SyncIfNeeded(clientRequest);

                SyncFileInfo syncFileInfo = null;

                bool clientProvidedFileName = clientRequest.FileID > 0;

                lock (syncFileInfoMutex)
                {
                    var currentSeqNum = lastAssignedSyncFileInfoSeqNum;
                    serverResponse.NewestSyncFileInfoSeqNum = currentSeqNum;

                    if (clientRequest.RequestFirstSyncFileInfoSeqNum <= currentSeqNum && clientRequest.RequestFirstSyncFileInfoSeqNum != -1)
                    {
                        serverResponse.SyncFileInfoArray = syncFileInfoSortedArray.GetItemsAtOrAfter(clientRequest.RequestFirstSyncFileInfoSeqNum, clientRequest.IncludeFileNamesAfterFileID);
                    }

                    if (clientProvidedFileName)
                    {
                        var sfiPair = syncFileInfoPairDictionary.SafeTryGetValue(clientRequest.FileID);

                        syncFileInfo = sfiPair.item;
                        serverResponse.SyncFileInfo = sfiPair.itemNoName ?? new SyncFileInfo() { FileID = clientRequest.FileID, Exists = false };
                    }
                }

                if (syncFileInfo != null)
                {
                    try
                    {
                        var availableLength = syncFileInfo.FileLength - clientRequest.FileOffset;
                        var lengthToRead = (int)Math.Min(availableLength, clientRequest.RequestedTransferSize);

                        bool fileAlreadyOpen = (state.currentFileStream != null);
                        if (fileAlreadyOpen && state.currentFileID != clientRequest.FileID)
                        {
                            state.CloseFileIfNeeded();
                            fileAlreadyOpen = false;
                        }

                        if (lengthToRead > 0)
                        {
                            int allocateLength = Math.Max(lengthToRead, 32);

                            // NOTE: the efficiency of the use of this concept for buffering depends on the client to ask for efficiently alligned and sized buffers.
                            serverResponse.Data = dataBufferArrayPool.Rent(allocateLength);

                            if (!fileAlreadyOpen)
                            {
                                var fullPath = System.IO.Path.Combine(RootDirFullPath, syncFileInfo.FileNameAndRelativePath);
                                state.currentFileStream = new System.IO.FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                state.currentFileID = syncFileInfo.FileID;
                                state.currentFileOffset = -1;
                            }

                            if (state.currentFileOffset < 0 || state.currentFileOffset != clientRequest.FileOffset)
                                state.currentFileStream.Seek(state.currentFileOffset = clientRequest.FileOffset, SeekOrigin.Begin);

                            serverResponse.FileOffset = state.currentFileOffset;
                            serverResponse.DataCount = state.currentFileStream.Read(serverResponse.Data, 0, lengthToRead);

                            serverResponse.EndOfFileReached = ((serverResponse.FileOffset + serverResponse.DataCount) >= Math.Min(syncFileInfo.FileLength, state.currentFileStream.Length));
                        }
                        else
                        {
                            serverResponse.Data = EmptyArrayFactory<byte>.Instance;
                            serverResponse.EndOfFileReached = ((serverResponse.FileOffset + serverResponse.DataCount) >= syncFileInfo.FileLength);

                            state.CloseFileIfNeeded();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        serverResponse.ResultCode = $"Incremental read file {syncFileInfo.FileID}:'{syncFileInfo.FileNameAndRelativePath}' offset:{clientRequest.FileOffset} len:{clientRequest.RequestedTransferSize} failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}";
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Task WriteResponse(ServerResponse response, ref TcpClientState state)
            {
                state.byteArrayBufferWriter.ResetCount();
                state.lenPrefixByteArrayBufferWriter.ResetCount();

                var mpWriter = new MessagePackWriter(state.byteArrayBufferWriter);

                MessagePackSerializer.Serialize(ref mpWriter, response);

                mpWriter.Flush();

                var lenPrefixMPWriter = new MessagePackWriter(state.lenPrefixByteArrayBufferWriter);

                lenPrefixMPWriter.WriteInt32(state.byteArrayBufferWriter.CurrentCount);

                lenPrefixMPWriter.Flush();

                var writeTask = WriteBufferedOutput(state);

                byteTransferCounter.Add(state.byteArrayBufferWriter.CurrentCount + state.lenPrefixByteArrayBufferWriter.CurrentCount);
                transactionCounter.Increment();

                return writeTask;
            }

            private Task WriteBufferedOutput(TcpClientState state)
            {
                var writeLenPrefixTask = state.tcpStream.WriteAsync(state.lenPrefixByteArrayBufferWriter.ByteArray, 0, state.lenPrefixByteArrayBufferWriter.CurrentCount, cancellationToken);
                return writeLenPrefixTask.ContinueWith(prevTask => state.tcpStream.WriteAsync(state.byteArrayBufferWriter.ByteArray, 0, state.byteArrayBufferWriter.CurrentCount, cancellationToken));
            }

            #endregion
        }
    }

    namespace Local
    {
        public class LocalFileSyncConfig
        {
            public LocalFileSyncConfig(string partID)
            {
                PartID = partID;
            }

            public string PartID { get; private set; }
            public FileSyncUpdateSelection FileSyncUpdateSelection { get; set; } = FileSyncUpdateSelection.AppendAsNeeded;
            public DestinationFileTimeHandling DestinationFileTimeHandling { get; set; } = DestinationFileTimeHandling.UpdateCreationAndLastWriteTimesFromSource;
            public string SourceRootDirPath { get; set; } = ".";
            public string DestinationRootDirPath { get; set; } = ".";
            public bool CreateDirPathsIfNeeded { get; set; } = true;
            public string FileFilterSpec { get; set; } = "*.*";
            public TimeSpan RescanForChangesInterval { get; set; } = (30.0).FromSeconds();
            public int FSWInternalBufferSize { get; set; } = 65536;
            public bool IncludeSubdirectories { get; set; } = true;
            public IValuesInterconnection IVI { get; set; }

            public MosaicLib.File.DirectoryTreePruningManager.Config DirectoryTreePruningManagerConfig { get; set; } = null;

            public bool RecopyFilesOnAnyChange { get { return FileSyncUpdateSelection == FileSyncUpdateSelection.RecopyOnAnyChange; } }
            public bool UpdateCreateTimeFromSource { get { return DestinationFileTimeHandling != DestinationFileTimeHandling.None; } }
            public bool UpdateLastWriteTimeFromSource { get { return DestinationFileTimeHandling == DestinationFileTimeHandling.UpdateCreationAndLastWriteTimesFromSource; } }

            /// <summary>Defines the ActionLoggingConfig that is used for part's external actions</summary>
            public ActionLoggingConfig ActionLoggingConfig { get; set; } = ActionLoggingConfig.Debug_Debug_Debug_Debug.Update(actionLoggingStyleSelect: ActionLoggingStyleSelect.IncludeRunTimeOnCompletion);
            public Logging.LogGate InitialInstanceLogGate { get; set; } = Logging.LogGate.Debug;

            public int NominalMaximumTransactionsPerSet { get; set; } = 20; // up to 5 mbytes of pending transfer per set.
            public int MaximumPendingTransferFiles { get; set; } = 5;
            public int MaximumPendingCreateAndSendFiles { get; set; } = 3;
            public TimeSpan? PerTranscationSetRateLimitSleepPeriod { get; set; } = null; // = TimeSpan.Zero;

            public LocalFileSyncConfig MakeCopyOfThis(bool deepCopy = true)
            {
                var copy = (LocalFileSyncConfig)MemberwiseClone();
                copy.IVI = IVI ?? Values.Instance;
                return copy;
            }
        }

        public class LocalFileSync : SimpleActivePartBase, IFileSyncClient
        {
            #region Construction, Release, PerformGoOnlineActionEx, PerformGoOfflineAction, PerformMainLoopService (et. al.)

            public LocalFileSync(LocalFileSyncConfig config)
                : base(config.PartID, SimpleActivePartBaseSettings.DefaultVersion2.Build(partBaseIVI: config.IVI, disableBusyBehavior: true, waitTimeLimit: (0.2).FromSeconds(), initialInstanceLogGate:  config.InitialInstanceLogGate))
            {
                Config = config.MakeCopyOfThis();

                SourceRootDirFullPath = System.IO.Path.GetFullPath(Config.SourceRootDirPath);
                DestinationRootDirFullPath = System.IO.Path.GetFullPath(Config.DestinationRootDirPath);

                if (Config.CreateDirPathsIfNeeded)
                {
                    if (!System.IO.Directory.Exists(SourceRootDirFullPath))
                        System.IO.Directory.CreateDirectory(SourceRootDirFullPath);
                    if (!System.IO.Directory.Exists(DestinationRootDirFullPath))
                        System.IO.Directory.CreateDirectory(DestinationRootDirFullPath);
                }

                ActionLoggingReference.Config = Config.ActionLoggingConfig;

                AddMainThreadStoppingAction(() => Release());

                SetupIVAPublication();
            }

            private LocalFileSyncConfig Config { get; set; }
            private readonly string SourceRootDirFullPath;
            private readonly string DestinationRootDirFullPath;

            private void Release()
            {
                ReleaseDirectoryTracking();
            }

            protected override string PerformGoOnlineActionEx(IProviderFacet ipf, bool andInitialize, INamedValueSet npv)
            {
                ReleaseDirectoryTracking();

                if (pruningManager == null && Config.DirectoryTreePruningManagerConfig != null)
                {
                    pruningManager = new MosaicLib.File.DirectoryTreePruningManager($"{PartID}.PruneMgr", Config.DirectoryTreePruningManagerConfig);
                    recentlyTouchedFileFullPathSetForPruningManager = new HashSet<string>();
                }

                // note that file tracking support can operate even if the FileSystemWatcher cannot be created.
                try
                {
                    sourceFSW = new FileSystemWatcher()
                    {
                        Path = SourceRootDirFullPath,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                        Filter = Config.FileFilterSpec,
                        IncludeSubdirectories = Config.IncludeSubdirectories,
                        EnableRaisingEvents = false,
                        InternalBufferSize = 65536, // defaults to 8192
                    };

                    sourceFSW.Changed += (o, e) => { asyncTouchedSourceFileNamesSet.Add(e.Name); Log.Trace.Emit($"Source FSW.Changed '{e.Name}'"); Notify(); };
                    sourceFSW.Created += (o, e) => { asyncTouchedSourceFileNamesSet.Add(e.Name); Log.Trace.Emit($"Source FSW.Created '{e.Name}'"); Notify(); };
                    sourceFSW.Deleted += (o, e) => { asyncTouchedSourceFileNamesSet.Add(e.Name); Log.Trace.Emit($"Source FSW.Deleted '{e.Name}'"); Notify(); };
                    sourceFSW.Error += (o, e) => { asyncErrorCount++; Notify(); Log.Debug.Emit("Source FSW.Error: Received unexpected FileSystemWatcher Error Event: {0}", e); };
                    sourceFSW.Renamed += (o, e) => { asyncTouchedSourceFileNamesSet.Add(e.Name); asyncTouchedSourceFileNamesSet.Add(e.OldName); Log.Trace.Emit($"Source FSW.Renamed '{e.Name}' [from '{e.OldName}']"); Notify(); };
                }
                catch (System.Exception ex)
                {
                    Fcns.DisposeOfObject(ref sourceFSW);
                    Log.Debug.Emit($"Unable to construct a usable FileSystemWatcher for '{SourceRootDirFullPath}': {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                }

                if (Config.RescanForChangesInterval > TimeSpan.Zero)
                    rescanIntervalTimer = new QpcTimer() { TriggerInterval = Config.RescanForChangesInterval, AutoReset = true }.Start();

                ServiceFileTracking(QpcTimeStamp.Now, forceFullUpdate: true);

                string ec = string.Empty;

                if (ec.IsNullOrEmpty() && SourceRootDirFullPath == DestinationRootDirFullPath)
                    ec = $"SourceRootDirPath cannot refer to the same path as DestinationRootDirPath ['{Config.SourceRootDirPath}', '{Config.DestinationRootDirPath}']";

                return ec;
            }

            protected override string PerformGoOfflineAction(IProviderActionBase action)
            {
                Release();
                ResetDirectoryInfoAndStopFSW();

                return base.PerformGoOfflineAction(action);
            }

            protected override void PerformMainLoopService()
            {
                base.PerformMainLoopService();

                var qpcTimeStamp = QpcTimeStamp.Now;

                if (BaseState.IsOnline)
                {
                    ServiceFileTracking(qpcTimeStamp, forceFullUpdate: false);
                }

                ServiceIVAPublication(qpcTimeStamp);

                ServicePendingSyncs(qpcTimeStamp);

                ServiceTransferBusyTracking(qpcTimeStamp);

                ServiceFileTransfers();

                if (pruningManager != null)
                {
                    if (BaseState.IsOnline)
                    {
                        pruningManager.Service();
                        if (recentlyTouchedFileFullPathSetForPruningManager.Count > 0)
                        {
                            recentlyTouchedFileFullPathSetForPruningManager.DoForEach(fileName => pruningManager.NotePathAdded(fileName));
                            recentlyTouchedFileFullPathSetForPruningManager.Clear();
                        }
                    }
                    else if (recentlyTouchedFileFullPathSetForPruningManager.Count > 0)
                    {
                        recentlyTouchedFileFullPathSetForPruningManager.Clear();
                    }
                }
            }

            private MosaicLib.File.DirectoryTreePruningManager pruningManager;
            private HashSet<string> recentlyTouchedFileFullPathSetForPruningManager;

            QpcTimer transferBusyUpdateHoldoffTimer = new QpcTimer() { TriggerInterval = (1.0).FromSeconds(), AutoReset = true }.Start();
            bool IsTransferBusy => (pendingTransferFileTrackerList.Count > 0);
            bool IsSyncBusy => (pendingSyncTrackerList.Count > 0);
            string transferBusyReason, syncBusyReason;
            string lastBusyReason = "";
            FileTracker[] lastPendingTransferFileTrackerListArray = null;

            private void ServiceTransferBusyTracking(QpcTimeStamp qpcTimeStamp)
            {
                if (!IsTransferBusy)
                {
                    if (transferBusyReason != null)
                    {
                        transferBusyReason = null;
                        transferBusyUpdateHoldoffTimer.StopIfNeeded();
                    }
                }
                else
                {
                    bool holdoffReached = transferBusyUpdateHoldoffTimer.StartIfNeeded().GetIsTriggered(qpcTimeStamp);
                    bool setBusyNow = (transferBusyReason == null) && IsTransferBusy && (pendingTransferFileTrackerList.Count > 1 || holdoffReached);
                    bool updateReason = (transferBusyReason != null) && (!object.ReferenceEquals(lastPendingTransferFileTrackerListArray, pendingTransferFileTrackerList.Array)) && holdoffReached;

                    if (setBusyNow || updateReason)
                    {
                        lastPendingTransferFileTrackerListArray = pendingTransferFileTrackerList.Array;
                        transferBusyReason = "Transferring {0} files [{1}]".CheckedFormat(pendingTransferFileTrackerList.Count, string.Join(",", lastPendingTransferFileTrackerListArray.Select(ft => ft.fileNameAndRelativePath)));
                    }
                }

                syncBusyReason = IsSyncBusy ? "Sync request is active" : null;

                string busyReason = transferBusyReason ?? syncBusyReason;
                var currentUseState = BaseState.UseState;

                if (lastBusyReason != busyReason && (currentUseState == UseState.Online || currentUseState == UseState.OnlineBusy))
                {
                    if (busyReason.IsNeitherNullNorEmpty())
                        SetBaseState(UseState.OnlineBusy, busyReason);
                    else
                        SetBaseState(UseState.Online, "Part is no longer busy");
                }

                lastBusyReason = busyReason;
            }

            #endregion

            #region Sync support (Sync, PerformSync, SyncTracker, ServicePendingSyncs, CancelPendingSyncs)

            public IClientFacet Sync(TimeSpan nominalWaitTimeLimit, SyncFlags syncFlags = default)
            {
                return new BasicActionImpl(ActionQueue, (ipf) => PerformSync(ipf, nominalWaitTimeLimit, syncFlags), "Sync", ActionLoggingReference, mesgDetails: (nominalWaitTimeLimit.IsZero() ? $"{syncFlags}" : $"timeLimit:{nominalWaitTimeLimit.TotalSeconds:f3}s, {syncFlags}"));
            }

            private string PerformSync(IProviderFacet ipf, TimeSpan nominalWaitTimeLimit, SyncFlags syncFlags)
            {
                haveNewSyncRequest = true;

                if (syncFlags.IsSet(SyncFlags.Full))
                {
                    ServiceFileTracking(QpcTimeStamp.Now, forceFullUpdate: true);
                }

                FileTracker waitForFileTracker = fileTrackerListSortedByFileID.LastOrDefault().Value;

                if (waitForFileTracker == null)
                    return "";

                pendingSyncTrackerList.Add(new SyncTracker()
                {
                    ipf = ipf,
                    busyToken = CreateInternalBusyFlagHolderObject(startingActionName: ipf.ToString(ToStringSelect.MesgAndDetail)),
                    waitForFileTracker = waitForFileTracker,
                    waitForSyncInfo = waitForFileTracker.sourceSyncFileInfo,
                    progressUpdateTimer = new QpcTimer() { TriggerInterval = (0.333).FromSeconds(), AutoReset = true }.Start(),
                    referenceTotalTransferredNewBytes = totalTransferredNewBytes,
                    referencePendingBytes = pendingBytes,
                    nvs = new NamedValueSet()
                            {
                                { "current", 0L },
                                { "total", pendingBytes },
                            },
                    nominalWaitTimeLimit = nominalWaitTimeLimit,
                    waitLimitTimer = !nominalWaitTimeLimit.IsZero() ? new QpcTimer() { TriggerInterval = nominalWaitTimeLimit }.Start() : default,
                });

                PerformMainLoopService();

                return null;
            }

            private class SyncTracker
            {
                public IProviderFacet ipf;
                public IDisposable busyToken;
                public FileTracker waitForFileTracker;
                public SyncFileInfo waitForSyncInfo;
                public QpcTimer progressUpdateTimer;
                public long referencePendingBytes;
                public long referenceTotalTransferredNewBytes;
                public NamedValueSet nvs;
                public TimeSpan nominalWaitTimeLimit;
                public QpcTimer waitLimitTimer;

                public void Complete(string resultCode)
                {
                    ipf.CompleteRequest(resultCode);
                    Fcns.DisposeOfObject(ref busyToken);
                }

                public void ServicePendingPublication(long pendingBytes, long totalTransferredNewBytes, QpcTimeStamp qpcTimeStamp)
                {
                    if (progressUpdateTimer.GetIsTriggered(qpcTimeStamp))
                        UpdateProgress(pendingBytes, totalTransferredNewBytes);
                }

                public void UpdateProgress(long pendingBytes, long totalTransferredNewBytes)
                {
                    if (referencePendingBytes < pendingBytes)
                    {
                        referencePendingBytes = pendingBytes;
                        nvs.SetValue("total", referencePendingBytes);
                    }

                    // note: the following is an estimate of the current progress.  This logic cannot determine if any increase in the pendingBytes relates to the file tracker we are waiting for or not.  As such this logic may be an overestimate of the work required to complete this sync requests.
                    var bytesTransferedSinceStartOfSync = totalTransferredNewBytes - referenceTotalTransferredNewBytes;
                    var pendingDecreaseSinceStartOfSync = referencePendingBytes - pendingBytes;

                    nvs.SetValue("current", Math.Min(bytesTransferedSinceStartOfSync, pendingDecreaseSinceStartOfSync));
                    ipf.UpdateNamedValues(nvs);
                }
            }

            private readonly IListWithCachedArray<SyncTracker> pendingSyncTrackerList = new IListWithCachedArray<SyncTracker>();
            private bool AreAnySyncOperationsInProgress { get { return (pendingSyncTrackerList.Count > 0) || haveNewSyncRequest; } }
            private bool haveNewSyncRequest = false;

            private void ServicePendingSyncs(QpcTimeStamp qpcTimeStamp)
            {
                if (pendingSyncTrackerList.Count > 0)
                {
                    foreach (var syncTracker in pendingSyncTrackerList.Array)
                    {
                        var waitForFileTracker = syncTracker.waitForFileTracker;
                        string resultCode = syncTracker.ipf.IsCancelRequestActive ? "Cancel requested" : waitForFileTracker.FaultCode.MapEmptyToNull();

                        if (resultCode == null && (waitForFileTracker.successfullyTransferredCount >= syncTracker.waitForSyncInfo.FileLength))
                            resultCode = string.Empty;

                        if (resultCode.IsNullOrEmpty() && waitForFileTracker.FaultCode.IsNeitherNullNorEmpty())
                            resultCode = waitForFileTracker.FaultCode;

                        if (resultCode.IsNullOrEmpty() && (waitForFileTracker.sourceSyncFileInfo?.Exists != true))
                        {
                            Log.Debug.Emit($"Nonstandard sync request completion: remote file {waitForFileTracker.fileID}:'{waitForFileTracker.fileNameAndRelativePath}' deleted unexpectedly");
                            resultCode = string.Empty;
                        }

                        if (resultCode == null && syncTracker.waitLimitTimer.GetIsTriggered(qpcTimeStamp))
                            resultCode = $"Sync wait time limit reached [after {syncTracker.waitLimitTimer.ElapsedTimeAtLastTrigger.TotalSeconds:f3} sec]";

                        if (resultCode != null)
                        {
                            pendingSyncTrackerList.Remove(syncTracker);
                            syncTracker.Complete(resultCode);

                            PublishBaseState(resultCode.IsNullOrEmpty() ? "Sync completed" : "Sync failed");
                        }
                        else
                        {
                            syncTracker.ServicePendingPublication(pendingBytes, totalTransferredNewBytes, qpcTimeStamp);
                        }
                    }

                    var baseState = BaseState;
                    if (!baseState.IsOnlineOrAttemptOnline || baseState.IsFaulted())
                        CancelPendingSyncs($"Part is not online normally [{baseState}]");
                }
                else if (haveNewSyncRequest)
                {
                    haveNewSyncRequest = false;
                }

                if (recalculatePendingBytes)
                {
                    recalculatePendingBytes = false;

                    long nextPendingBytes = 0;
                    foreach (var ft in fileTrackerByNameDictionary.ValueArray)
                    {
                        var ftPendingBytes = ft.sourceSyncFileInfo.FileLength - ft.successfullyTransferredCount;
                        nextPendingBytes += ftPendingBytes;
                    }

                    pendingBytes = nextPendingBytes;
                }
            }

            private void CancelPendingSyncs(string reason)
            {
                if (pendingSyncTrackerList.Count > 0)
                {
                    pendingSyncTrackerList.Array.DoForEach(syncTracker => syncTracker.Complete(reason));
                    pendingSyncTrackerList.Clear();
                }
            }

            #endregion

            #region File tracking support logic

            private void ReleaseDirectoryTracking()
            {
                Fcns.DisposeOfObject(ref sourceFSW);
            }

            private System.IO.FileSystemWatcher sourceFSW;

            private class AsyncTouchedFileNamesSet
            {
                public object mutex = new object();
                public HashSet<string> set = new HashSet<string>();
                public volatile int setCount = 0;

                public void Add(string name) { lock (mutex) { set.Add(name); setCount = set.Count; } }
                public void Clear() { lock (mutex) { set.Clear(); setCount = set.Count; } }
                public void TransferContentsTo(HashSet<string> other)
                {
                    lock (mutex)
                    {
                        other.UnionWith(set);
                        set.Clear();
                        setCount = set.Count;
                    }
                }
            }
            private readonly AsyncTouchedFileNamesSet asyncTouchedSourceFileNamesSet = new AsyncTouchedFileNamesSet();
            private volatile int asyncErrorCount;

            private readonly HashSet<string> pendingSourceFileNameAndRelativePathSet = new HashSet<string>();

            private long sourceFileIDGen; // use pre-increment as generate method.  FileID zero is reserved (aka is not used)
            private long syncFileInfoSeqNumGen;     // use pre-increment as generate method.  SyncFileInfoSeqNum zero is reserved (aka is not used)
            private readonly IDictionaryWithCachedArrays<string, FileTracker> fileTrackerByNameDictionary = new IDictionaryWithCachedArrays<string, FileTracker>();

            private readonly SortedList<long, FileTracker> fileTrackerListSortedByFileID = new SortedList<long, FileTracker>();
            private readonly IListWithCachedArray<FileTracker> pendingTransferFileTrackerList = new IListWithCachedArray<FileTracker>();
            private readonly HashSet<FileTracker> pendingEvaluateFileTrackerSet = new HashSet<FileTracker>();
            private readonly IListWithCachedArray<FileTracker> pendingEvaluateFileTrackerList = new IListWithCachedArray<FileTracker>();

            private void AddToPendingEvaluateListIfNeeded(FileTracker ft)
            {
                if (ft != null && !pendingEvaluateFileTrackerSet.Contains(ft))
                {
                    pendingEvaluateFileTrackerList.Add(ft);
                    pendingEvaluateFileTrackerSet.Add(ft);
                }
            }

            private void RemoveFromPendingEvaluateListIfNeeded(FileTracker ft)
            {
                if (pendingEvaluateFileTrackerSet.Contains(ft))
                {
                    pendingEvaluateFileTrackerList.Remove(ft);
                    pendingEvaluateFileTrackerSet.Remove(ft);
                }
            }

            long totalTransferredNewBytes, pendingBytes;
            bool recalculatePendingBytes;

            QpcTimer backgroundScanSyncIntervalTimer = new QpcTimer() { AutoReset = true };
            QpcTimer rescanDisabledTrackersTimer = new QpcTimer() { TriggerInterval = (5.0).FromSeconds(), AutoReset = true }.Start();
            int disabledTrackerCount;

            #region shared data - syncFileInfo related data that is maintained by parts primary thread and which is concurrently accessed on TcpClient Task threads.

            int byteTransferCount, blockTransferCount, fileScanCount;
            int lastByteTransferCount, lastBlockTransferCount, lastFileScanCount;

            private IValueAccessor ivaMBytesPerSec, ivaBlockTransfersPerSec;
            private readonly MosaicLib.Utils.Tools.EventRateTool mBytesPerSecTool = new MosaicLib.Utils.Tools.EventRateTool();
            private readonly MosaicLib.Utils.Tools.EventRateTool blockTransfersPerSecTool = new MosaicLib.Utils.Tools.EventRateTool();

            private void SetupIVAPublication()
            {
                ivaMBytesPerSec = Config.IVI.GetValueAccessor($"{PartID}.MBytesPerSec").Set(0.0);
                ivaBlockTransfersPerSec = Config.IVI.GetValueAccessor($"{PartID}.BlockTransfersPerSec").Set(0.0);                
            }

            private void ServiceIVAPublication(QpcTimeStamp qpcTimeStamp)
            {
                //service the average rate publication
                {
                    var bytesTransferDiff = byteTransferCount - lastByteTransferCount;
                    lastByteTransferCount = byteTransferCount;

                    var blockTransferDiff = blockTransferCount - lastBlockTransferCount;
                    lastBlockTransferCount = blockTransferCount;

                    var fileScanCountDiff = fileScanCount - lastFileScanCount;
                    lastFileScanCount = fileScanCount;

                    mBytesPerSecTool.NoteEventHappened(bytesTransferDiff * 0.000001);
                    if (mBytesPerSecTool.HasNewAvgRate)
                        ivaMBytesPerSec.Set(mBytesPerSecTool.AvgRate);

                    blockTransfersPerSecTool.NoteEventHappened(blockTransferDiff);
                    if (blockTransfersPerSecTool.HasNewAvgRate)
                        ivaBlockTransfersPerSec.Set(blockTransfersPerSecTool.AvgRate);
                }
            }

            #endregion

            private class FileTracker
            {
                public long fileID;
                public string fileNameAndRelativePath;

                public string fileNameFromConfiguredSourcePath;
                public string fullSourcePath;

                public long seqNum;
                public FileInfo sourceFileInfo;
                public SyncFileInfo sourceSyncFileInfo;

                public string fileNameFromConfiguredDestinationPath;
                public string fullDestinationPath;
                public FileInfo destinationLocalFileInfo;
                public SyncFileInfo destinationSyncFileInfo;
                public long successfullyTransferredCount;

                public string FaultCode
                {
                    get { return _FaultCode; }
                    set
                    {
                        IsDisabled = (_FaultCode = value).IsNeitherNullNorEmpty();
                        if (IsDisabled)
                            disableUntil.StartIfNeeded((1.0).FromMinutes());
                        else
                            disableUntil.StopIfNeeded();
                    }
                }

                private string _FaultCode;
                public QpcTimer disableUntil;

                public bool IsDisabled { get; set; }

                public string Name {get { return $"{fileID}:'{fileNameAndRelativePath}'"; } }
                public override string ToString()
                {
                    string isDisabled = IsDisabled ? " [Disabled]" : "";

                    return $"FileTracker: {Name}{isDisabled}";
                }
            }

            private void DisableFileTracker(FileTracker ft, string faultCode)
            {
                Log.Debug.Emit(faultCode);

                ft.FaultCode = faultCode;
                ft.seqNum = ++syncFileInfoSeqNumGen;

                disabledTrackerCount += 1;

                RemoveFromPendingEvaluateListIfNeeded(ft);
                if (pendingTransferFileTrackerList.Contains(ft))
                    pendingTransferFileTrackerList.Remove(ft);

                // intentionally find and remove any active transfers for this tracker.  Do not return these buffers to the free list.
                if (activeTransferBlockList.Any(tb => object.ReferenceEquals(tb.ft, ft)))
                    activeTransferBlockList.RemoveAll(tb => object.ReferenceEquals(tb.ft, ft));

                ft.successfullyTransferredCount = 0;
                recalculatePendingBytes = true;
            }

            private void ResetDirectoryInfoAndStopFSW()
            {
                if (sourceFSW != null)
                    sourceFSW.EnableRaisingEvents = false;

                asyncTouchedSourceFileNamesSet.Clear();
                asyncErrorCount = 0;

                fileTrackerByNameDictionary.ValueArray.DoForEach(ft => AddToPendingEvaluateListIfNeeded(ft));

                ignoreFileNameAndRelativePathSet.Clear();
            }

            private readonly HashSet<string> newFileNameAndRelativePathSet = new HashSet<string>();
            private readonly HashSet<string> ignoreFileNameAndRelativePathSet = new HashSet<string>();

            private bool fullUpdateNeeded = true;
            private bool rescanDirectoryNeeded = true;
            private QpcTimer rescanIntervalTimer;

            private int ServiceFileTracking(QpcTimeStamp qpcTimeStamp, bool forceFullUpdate = false)
            {
                qpcTimeStamp = qpcTimeStamp.MapDefaultToNow();

                long entrySyncFileInfoSeqNumGen = syncFileInfoSeqNumGen;

                fullUpdateNeeded |= forceFullUpdate || (asyncErrorCount > 0);
                rescanDirectoryNeeded |= fullUpdateNeeded | rescanIntervalTimer.GetIsTriggered(qpcTimeStamp);

                if (fullUpdateNeeded)
                {
                    ResetDirectoryInfoAndStopFSW();
                    fullUpdateNeeded = false;
                }
                else if (asyncTouchedSourceFileNamesSet.setCount > 0)
                {
                    asyncTouchedSourceFileNamesSet.TransferContentsTo(pendingSourceFileNameAndRelativePathSet);
                }

                if (pendingSourceFileNameAndRelativePathSet.Count > 0)
                {
                    foreach (var fileNameAndRelativePath in pendingSourceFileNameAndRelativePathSet)
                    {
                        if (fileTrackerByNameDictionary.TryGetValue(fileNameAndRelativePath, out FileTracker ft) && ft != null)
                            AddToPendingEvaluateListIfNeeded(ft);
                        else
                            newFileNameAndRelativePathSet.Add(fileNameAndRelativePath);
                    }

                    pendingSourceFileNameAndRelativePathSet.Clear();
                }

                if (rescanDirectoryNeeded)
                {
                    if (sourceFSW != null && !sourceFSW.EnableRaisingEvents)
                        sourceFSW.EnableRaisingEvents = true;

                    // mark all of the files as touched (since we are going to enumerate them explicitly and then mark all the ones that were not found as touched - this is quicker.
                    fileTrackerByNameDictionary.ValueArray.DoForEach(ft => AddToPendingEvaluateListIfNeeded(ft));

                    foreach (var scanFileNameAndPath in System.IO.Directory.EnumerateFiles(SourceRootDirFullPath, Config.FileFilterSpec, Config.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    {
                        var scanFileNameAndRelativePath = scanFileNameAndPath.MakeRelativePath(SourceRootDirFullPath);
                        if (Config.IncludeSubdirectories && System.IO.Directory.Exists(scanFileNameAndPath))
                            continue;

                        if (!ignoreFileNameAndRelativePathSet.Contains(scanFileNameAndRelativePath) && (!fileTrackerByNameDictionary.TryGetValue(scanFileNameAndRelativePath, out FileTracker ft) || ft == null))
                        {
                            newFileNameAndRelativePathSet.Add(scanFileNameAndRelativePath);
                        }
                    }

                    rescanDirectoryNeeded = false;
                }

                if (newFileNameAndRelativePathSet.Count > 0)
                {
                    foreach (var newFileNameAndRelativePath in newFileNameAndRelativePathSet)
                    {
                        try
                        {
                            var fileNameFromConfiguredSourcePath = System.IO.Path.Combine(Config.SourceRootDirPath, newFileNameAndRelativePath);
                            var fullSourcePath = System.IO.Path.Combine(SourceRootDirFullPath, newFileNameAndRelativePath);

                            var fileNameFromConfiguredDestinationPath = System.IO.Path.Combine(Config.DestinationRootDirPath, newFileNameAndRelativePath);
                            var fullDesitinationPath = System.IO.Path.Combine(DestinationRootDirFullPath, newFileNameAndRelativePath);

                            if (!System.IO.Directory.Exists(fullSourcePath))
                            {
                                var ft = new FileTracker()
                                {
                                    fileID = ++sourceFileIDGen,
                                    seqNum = 0,
                                    fileNameAndRelativePath = newFileNameAndRelativePath,
                                    fileNameFromConfiguredSourcePath = fileNameFromConfiguredSourcePath,
                                    fullSourcePath = fullSourcePath,
                                    fileNameFromConfiguredDestinationPath = fileNameFromConfiguredDestinationPath,
                                    fullDestinationPath = fullDesitinationPath,
                                };

                                fileTrackerByNameDictionary[newFileNameAndRelativePath] = ft;

                                AddToPendingEvaluateListIfNeeded(ft);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ignoreFileNameAndRelativePathSet.Add(newFileNameAndRelativePath);
                            Log.Debug.Emit($"Unexpected issue generating FileTracker for '{newFileNameAndRelativePath}': {ex.ToString(ExceptionFormat.TypeAndMessage)} [added to ignore list]");
                        }
                    }

                    newFileNameAndRelativePathSet.Clear();
                }

                if (disabledTrackerCount > 0 && rescanDisabledTrackersTimer.GetIsTriggered(qpcTimeStamp))
                {
                    var nextDisabledTrackerCount = 0;
                    // search for disabled trackers and for trackers that are setup pending
                    foreach (var ft in fileTrackerByNameDictionary.ValueArray.Where(ft => ft.IsDisabled))
                    {
                        if (ft.disableUntil.GetIsTriggered(qpcTimeStamp))
                        {
                            ft.IsDisabled = false;
                            AddToPendingEvaluateListIfNeeded(ft);
                        }
                        else
                        {
                            nextDisabledTrackerCount++;
                        }
                    }

                    disabledTrackerCount = nextDisabledTrackerCount;
                }

                if (pendingEvaluateFileTrackerList.Count > 0)
                {
                    foreach (var ft in pendingEvaluateFileTrackerList.Array)
                    {
                        if (pendingTransferFileTrackerList.Count >= Config.MaximumPendingTransferFiles)
                            break;

                        fileScanCount++;

                        try
                        {
                            var sysFileInfo = new FileInfo(ft.fullSourcePath);
                            var syncFileInfo = ft.sourceSyncFileInfo;

                            bool trackerEvaluationIsCompleteAndSuccessful = true;

                            // check if the file still exists: it might have been deleted/pruned since it was added here.
                            if (sysFileInfo.Exists)
                            {
                                if (!ft.sourceFileInfo.IsEqual(sysFileInfo))
                                {
                                    var entryFTSeqNum = ft.seqNum;
                                    var entrySourceSyncFileInfo = ft.sourceSyncFileInfo;

                                    ft.seqNum = ++syncFileInfoSeqNumGen;
                                    ft.sourceFileInfo = sysFileInfo;
                                    ft.sourceSyncFileInfo = syncFileInfo = new SyncFileInfo(sysFileInfo, ft.seqNum, ft.fileID, ft.fileNameAndRelativePath);

                                    bool lenBecameZero = (syncFileInfo.FileLength == 0) && (entrySourceSyncFileInfo?.FileLength != 0);
                                    if (lenBecameZero)
                                        trackerEvaluationIsCompleteAndSuccessful = false;   // do not remove this tracker from the pending set - force it to be evaulated again shortly.
                                }
                            }
                            else
                            {
                                DisableFileTracker(ft, $"File '{ft.fileNameAndRelativePath}' has been removed");
                                trackerEvaluationIsCompleteAndSuccessful = false;

                                fileTrackerByNameDictionary.Remove(ft.fileNameAndRelativePath);
                            }

                            if (ft.destinationLocalFileInfo == null)
                            {
                                var fullDestinationPath = ft.fullDestinationPath;

                                if (System.IO.File.Exists(fullDestinationPath))
                                {
                                    bool readInfoSuccess = ReadInitialLocalSyncInfo(ft);

                                    if (readInfoSuccess && ft.sourceSyncFileInfo.FileLength > ft.successfullyTransferredCount)
                                        pendingTransferFileTrackerList.Add(ft);
                                }
                                else if (pendingTransferFileTrackerList.Count < Config.MaximumPendingCreateAndSendFiles)
                                {
                                    bool createSuccess = CreateFileAndSetCreationTime(ft) && ReadInitialLocalSyncInfo(ft);

                                    if (createSuccess && ft.sourceSyncFileInfo.FileLength > ft.successfullyTransferredCount)
                                        pendingTransferFileTrackerList.Add(ft);
                                }
                                else
                                {
                                    // leave the current file where it is in the list - this blocks further evaluation until we receive the first parts of this file.
                                    trackerEvaluationIsCompleteAndSuccessful = false;
                                }
                            }
                            else
                            {
                                var fileLength = ft.sourceSyncFileInfo.FileLength;
                                if (ft.successfullyTransferredCount < fileLength
                                    || (ft.destinationSyncFileInfo.CreationTimeUTCTicks != ft.sourceSyncFileInfo.CreationTimeUTCTicks && Config.UpdateCreateTimeFromSource)
                                    || (ft.destinationSyncFileInfo.LastWriteTimeUTCTicks != ft.sourceSyncFileInfo.LastWriteTimeUTCTicks && Config.UpdateLastWriteTimeFromSource)
                                    )
                                {
                                    if (Config.RecopyFilesOnAnyChange && ft.successfullyTransferredCount > 0)
                                    {
                                        ft.successfullyTransferredCount = 0;
                                        recalculatePendingBytes = true;
                                    }

                                    if (pendingTransferFileTrackerList.Count < Config.MaximumPendingTransferFiles)
                                        pendingTransferFileTrackerList.Add(ft);
                                    else
                                        trackerEvaluationIsCompleteAndSuccessful = false;
                                }
                            }

                            if (trackerEvaluationIsCompleteAndSuccessful)
                            {
                                RemoveFromPendingEvaluateListIfNeeded(ft);

                                if (!ft.IsDisabled && ft.FaultCode.IsNeitherNullNorEmpty())
                                {
                                    Log.Debug.Emit($"Previous issue with file '{ft.fileNameAndRelativePath}' has been resolved [{ft.FaultCode}]");
                                    ft.FaultCode = string.Empty;
                                    ft.seqNum = ++syncFileInfoSeqNumGen;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            DisableFileTracker(ft, $"{CurrentMethodName} service for {ft.fileID}:'{ft.fileNameAndRelativePath}' failed: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                        }
                    }
                }

                return (syncFileInfoSeqNumGen != entrySyncFileInfoSeqNumGen).MapToInt();
            }

            private bool CreateFileAndSetCreationTime(FileTracker ft)
            {
                try
                {
                    var dirPartOfFullFilePath = System.IO.Path.GetDirectoryName(ft.fullDestinationPath);
                    if (!System.IO.Directory.Exists(dirPartOfFullFilePath))
                    {
                        Log.Debug.Emit($"Creating directory '{dirPartOfFullFilePath}' for file {ft.fileID}:'{ft.fileNameAndRelativePath}'");
                        System.IO.Directory.CreateDirectory(dirPartOfFullFilePath);
                    }

                    System.IO.File.Create(ft.fullDestinationPath).Close();

                    if (Config.UpdateCreateTimeFromSource)
                    {
                        var creationTimeUTC = new DateTime(ft.sourceSyncFileInfo.CreationTimeUTCTicks, DateTimeKind.Utc);

                        System.IO.File.SetCreationTimeUtc(ft.fullDestinationPath, creationTimeUTC);

                        Log.Debug.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' created and creation time has been set to '{creationTimeUTC.CvtToString(Dates.DateTimeFormat.RoundTrip)}'");
                    }
                    else
                    {
                        Log.Debug.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' has been created");
                    }

                    return true;
                }
                catch (System.Exception ex)
                {
                    DisableFileTracker(ft, $"{CurrentMethodName} failed for {ft.fileID}:'{ft.fileNameAndRelativePath}': {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    return false;
                }
            }

            private bool ReadInitialLocalSyncInfo(FileTracker ft)
            {
                try
                {
                    var sysFileInfo = new FileInfo(ft.fullDestinationPath);
                    ft.destinationLocalFileInfo = sysFileInfo;
                    ft.destinationSyncFileInfo = new SyncFileInfo(sysFileInfo, 0, 0, ft.fileNameAndRelativePath);

                    ft.successfullyTransferredCount = 0;
                    recalculatePendingBytes = true;

                    var lengthDiff = Math.Abs(ft.destinationSyncFileInfo.FileLength - ft.sourceSyncFileInfo.FileLength);
                    var createTimeDiff = new TimeSpan(Math.Abs(ft.destinationSyncFileInfo.CreationTimeUTCTicks - ft.sourceSyncFileInfo.CreationTimeUTCTicks));
                    var lastWriteTimeDiff = new TimeSpan(Math.Abs(ft.destinationSyncFileInfo.LastWriteTimeUTCTicks - ft.sourceSyncFileInfo.LastWriteTimeUTCTicks));

                    var retransferFileContents = Config.RecopyFilesOnAnyChange
                                            && (lengthDiff != 0)
                                            && (createTimeDiff > (0.01).FromSeconds())  // if updating create time is disabled then this is likely to trigger a copy on each new connection
                                            && (lastWriteTimeDiff > (0.01).FromSeconds())  // if updating last write time is disabled then this is likely to trigger a copy on each new connection
                                            ;

                    if (!retransferFileContents)
                        ft.successfullyTransferredCount = ft.destinationSyncFileInfo.FileLength;

                    recentlyTouchedFileFullPathSetForPruningManager?.Add(ft.fullDestinationPath);

                    AddToPendingEvaluateListIfNeeded(ft);

                    return true;
                }
                catch (System.Exception ex)
                {
                    DisableFileTracker(ft, $"{CurrentMethodName} failed for {ft.fileID}:'{ft.fileNameAndRelativePath}': {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    return false;
                }
            }
            #endregion

            #region file transfer support (ServiceFileTransfers, TransferBlock, related lists, OpenFileTracker and instance)

            private void ServiceFileTransfers()
            {
                QpcTimeStamp startTime = QpcTimeStamp.Now;
                int transferCount = 0;
                int wrByteCount = 0;
                int tsUpdateCount = 0;

                try
                {
                    foreach (var ft in pendingTransferFileTrackerList.Array)
                    {
                        if (activeTransferBlockList.Count >= Config.NominalMaximumTransactionsPerSet)
                            break;

                        try
                        {
                            var fileLength = ft.sourceSyncFileInfo.FileLength;
                            if (ft.successfullyTransferredCount > fileLength)
                            {
                                if (Config.RecopyFilesOnAnyChange)
                                    ft.successfullyTransferredCount = 0;
                                else
                                    ft.successfullyTransferredCount = fileLength;
                            }

                            var fs = openFileTracker.OpenIfNeeded(ft, this, useSourceSide: true);

                            if (fs.Length != fileLength)
                            {
                                Log.Debug.Emit($"{ft.fileID}:'{ft.fileNameAndRelativePath}' length ambiguity: stream length:{fs.Length} != last file info length:{fileLength}");
                                AddToPendingEvaluateListIfNeeded(ft);
                            }

                            long readScanPosition = ft.successfullyTransferredCount & ~TransferBlock.alignmentSize;

                            for (; ; )
                            {
                                var tb = transferBlockFreeList.Get();

                                tb.ft = ft;
                                tb.startOffset = readScanPosition;

                                tb.byteCount = (int)Math.Min(TransferBlock.bufferSize, fileLength - tb.startOffset);
                                tb.readStartTime = QpcTimeStamp.Now;

                                if (fs.Position != tb.startOffset)
                                    fs.Seek(tb.startOffset, SeekOrigin.Begin);

                                if (tb.byteCount > 0)
                                    tb.byteCount = fs.Read(tb.buffer, 0, tb.byteCount);

                                totalTransferredNewBytes += tb.byteCount;
                                byteTransferCount += tb.byteCount;

                                readScanPosition += tb.byteCount;

                                tb.readRunTime = tb.readStartTime.Age;

                                activeTransferBlockList.Add(tb);

                                var readEndPosition = tb.startOffset + tb.byteCount;
                                tb.lastBlockInFile = (readEndPosition == fileLength);

                                Log.Trace.Emit("Transfer Read {0}:'{1}' startOffset:{2} count:{3} times rd:{4:f3} total:{5:f3}{6}", ft.fileID, ft.fileNameAndRelativePath, tb.startOffset, tb.byteCount, tb.readRunTime.TotalSeconds, tb.totalRunTime.TotalSeconds, tb.lastBlockInFile ? " [LastBlockInFile]" : "");

                                if (tb.byteCount < TransferBlock.bufferSize  || activeTransferBlockList.Count >= Config.NominalMaximumTransactionsPerSet)
                                {
                                    break;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            DisableFileTracker(ft, $"{CurrentMethodName} failed (read): {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                            break;
                        }
                    }
                }
                finally
                {
                    openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();
                }

                try
                {
                    TransferBlock tb;
                    while ((tb = activeTransferBlockList.SafeTakeFirst()) != null)
                    {
                        var ft = tb.ft;
                        recalculatePendingBytes = true;

                        try
                        {
                            var fs = openFileTracker.OpenIfNeeded(ft, this, useSourceSide: false);

                            if (fs.Position != tb.startOffset)
                                fs.Seek(tb.startOffset, SeekOrigin.Begin);

                            if (tb.byteCount > 0)
                                fs.Write(tb.buffer, 0, tb.byteCount);

                            totalTransferredNewBytes += tb.byteCount;
                            byteTransferCount += tb.byteCount;
                            blockTransferCount++;

                            long endOfWritePosition = tb.startOffset + tb.byteCount;
                            long numBytesAddedToFile = endOfWritePosition - ft.destinationSyncFileInfo.FileLength;

                            tb.totalRunTime = tb.readStartTime.Age();

                            var tbIsLastBlockInFile = tb.lastBlockInFile;

                            Log.Trace.Emit("Transfer Wrote {0}:'{1}' startOffset:{2} count:{3} times rd:{4:f3} total:{5:f3}{6}", ft.fileID, ft.fileNameAndRelativePath, tb.startOffset, tb.byteCount, tb.readRunTime.TotalSeconds, tb.totalRunTime.TotalSeconds, tbIsLastBlockInFile ? " [LastBlockInFile]" : "");

                            wrByteCount += tb.byteCount;
                            transferCount += 1;

                            transferBlockFreeList.Release(ref tb);

                            if (tbIsLastBlockInFile)
                            {
                                if (fs.Length != endOfWritePosition)
                                    openFileTracker.pendingSetLength = endOfWritePosition;

                                openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();

                                AddToPendingEvaluateListIfNeeded(ft);
                            }

                            if (tbIsLastBlockInFile)
                                recentlyTouchedFileFullPathSetForPruningManager?.Add(ft.fullDestinationPath);

                            if (numBytesAddedToFile > 0 || tbIsLastBlockInFile)
                            {
                                ft.successfullyTransferredCount = endOfWritePosition;
                                if (ft.destinationSyncFileInfo.FileLength < ft.successfullyTransferredCount)
                                    ft.destinationSyncFileInfo.FileLength = ft.successfullyTransferredCount;

                                if (openFileTracker.pendingCreationTimeUTCTicksUpdate == null && ft.destinationSyncFileInfo.CreationTimeUTCTicks != ft.sourceSyncFileInfo.CreationTimeUTCTicks && Config.UpdateCreateTimeFromSource)
                                {
                                    openFileTracker.OpenIfNeeded(ft, this, useSourceSide: false);
                                    openFileTracker.pendingCreationTimeUTCTicksUpdate = ft.sourceSyncFileInfo.CreationTimeUTCTicks;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            DisableFileTracker(ft, $"{CurrentMethodName} failed (write): {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                        }
                    }

                    // remove completed transfers from the list after performing an attempt to update the creation and last write time if needed and enabled.
                    foreach (var ft in pendingTransferFileTrackerList.Array)
                    {
                        long fileLength = ft.sourceSyncFileInfo.FileLength;
                        if (ft.successfullyTransferredCount >= fileLength)
                        {
                            pendingTransferFileTrackerList.Remove(ft);

                            if (openFileTracker.pendingCreationTimeUTCTicksUpdate == null && ft.destinationSyncFileInfo.CreationTimeUTCTicks != ft.sourceSyncFileInfo.CreationTimeUTCTicks && Config.UpdateCreateTimeFromSource)
                            {
                                openFileTracker.OpenIfNeeded(ft, this, useSourceSide: false);
                                openFileTracker.pendingCreationTimeUTCTicksUpdate = ft.sourceSyncFileInfo.CreationTimeUTCTicks;
                            }

                            if (openFileTracker.pendingLastWriteTimeUTCTicksUpdate == null && ft.destinationSyncFileInfo.LastWriteTimeUTCTicks != ft.sourceSyncFileInfo.LastWriteTimeUTCTicks && Config.UpdateLastWriteTimeFromSource && ft.successfullyTransferredCount >= ft.sourceSyncFileInfo.FileLength)
                            {
                                openFileTracker.OpenIfNeeded(ft, this, useSourceSide: false);
                                openFileTracker.pendingLastWriteTimeUTCTicksUpdate = ft.sourceSyncFileInfo.LastWriteTimeUTCTicks;
                            }
                        }
                    }
                }
                finally
                {
                    if (openFileTracker.pendingCreationTimeUTCTicksUpdate != null || openFileTracker.pendingLastWriteTimeUTCTicksUpdate != null || openFileTracker.pendingSetLength != null)
                        tsUpdateCount += 1;
                    openFileTracker.ReleaseAndApplyAnyPendingUpdatesIfNeeded();
                }

                if (transferCount != 0 || wrByteCount != 0 || tsUpdateCount != 0)
                {
                    double runTime = startTime.Age.TotalSeconds;
                    Log.Trace.Emit("TransferSet {0} bytes:{1} updates:{2} runTime:{3:f3} mb/s:{4:f3}", transferCount, wrByteCount, tsUpdateCount, runTime, (wrByteCount * 0.000001) * runTime.SafeOneOver());

                    Notify(); // wake up the main part immediately if it did any transfer work (so that the next call to WaitForSomethingToDo will not block)
                }

                Config.PerTranscationSetRateLimitSleepPeriod.SleepIfNotNull();
            }

            private class TransferBlock
            {
                public const int alignmentSize = 4096;
                public const int bufferSize = 4*65536;    // this must be at least as large as the alignementSize

                public FileTracker ft;
                public long startOffset;
                public int byteCount;
                public byte[] buffer = new byte[bufferSize];
                public bool lastBlockInFile;

                public void Clear()
                {
                    ft = null;
                    startOffset = 0;
                    byteCount = 0;
                    lastBlockInFile = false;
                    readStartTime = QpcTimeStamp.Zero;
                    readRunTime = totalRunTime = TimeSpan.Zero;
                }

                public QpcTimeStamp readStartTime;
                public TimeSpan readRunTime, totalRunTime;
            }

            private readonly IListWithCachedArray<TransferBlock> activeTransferBlockList = new IListWithCachedArray<TransferBlock>();
            private readonly MosaicLib.Utils.Pooling.BasicFreeList<TransferBlock> transferBlockFreeList = new MosaicLib.Utils.Pooling.BasicFreeList<TransferBlock>() { FactoryDelegate = () => new TransferBlock(), ClearDelegate = (item) => item.Clear(), MaxItemsToKeep = 100 };

            private struct OpenFileTracker
            {
                public FileTracker fileTracker;
                public bool usingSourceSide;
                public FileStream fileStream;
                public LocalFileSync part;

                public long? pendingSetLength;
                public long? pendingCreationTimeUTCTicksUpdate;
                public long? pendingLastWriteTimeUTCTicksUpdate;

                public FileStream OpenIfNeeded(FileTracker fileTracker, LocalFileSync part, bool useSourceSide)
                {
                    if (Object.ReferenceEquals(this.fileTracker, fileTracker) && this.fileTracker != null && usingSourceSide == useSourceSide)
                        return fileStream;

                    this.part = part;
                    this.usingSourceSide = useSourceSide;

                    ReleaseAndApplyAnyPendingUpdatesIfNeeded();

                    for (int tryCount = 0; ;)
                    {
                        try
                        {
                            if (usingSourceSide)
                                fileStream = new FileStream(fileTracker.fullSourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            else
                                fileStream = new FileStream(fileTracker.fullDestinationPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                            break;
                        }
                        catch (System.IO.IOException ex)
                        {
                            tryCount++;

                            if (tryCount < 6)
                            {
                                part.Log.Trace.Emit($"{CurrentMethodName} issue for {fileTracker.fileID}:'{fileTracker.fileNameAndRelativePath}' at try:{tryCount}: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                                ((tryCount > 1) ? (0.1).FromSeconds() : TimeSpan.Zero).Sleep();
                            }
                            else
                            {
                                part.Log.Debug.Emit($"{CurrentMethodName} failed for {fileTracker.fileID}:'{fileTracker.fileNameAndRelativePath}' at try:{tryCount}: {ex.ToString(ExceptionFormat.TypeAndMessage)}");
                                throw;
                            }
                        }
                    }

                    this.fileTracker = fileTracker;
                    return fileStream;
                }

                public bool IsOpen { get { return fileStream != null; } }

                public void ReleaseAndApplyAnyPendingUpdatesIfNeeded()
                {
                    var ft = fileTracker;

                    fileTracker = null;

                    try
                    {
                        if (fileStream != null)
                        {
                            if (pendingSetLength != null && !usingSourceSide)
                            {
                                var setLength = pendingSetLength ?? 0;
                                if (setLength == -1)
                                    setLength = ft.successfullyTransferredCount;

                                fileStream.SetLength(setLength);

                                ft.destinationSyncFileInfo.FileLength = setLength;
                                if (ft.successfullyTransferredCount > setLength)
                                    ft.successfullyTransferredCount = setLength;

                                pendingSetLength = null;
                            }

                            fileStream.Close();
                            Fcns.DisposeOfObject(ref fileStream);
                        }

                        if (pendingCreationTimeUTCTicksUpdate != null && !usingSourceSide)
                        {
                            var entryCreationTimeUTCTicks = ft.destinationSyncFileInfo.CreationTimeUTCTicks;

                            var creationTimeUTCTicks = pendingCreationTimeUTCTicksUpdate ?? 0;
                            pendingCreationTimeUTCTicksUpdate = null;

                            var creationTimeUTC = new DateTime(creationTimeUTCTicks, DateTimeKind.Utc);

                            System.IO.File.SetCreationTimeUtc(ft.fullDestinationPath, creationTimeUTC);
                            ft.destinationSyncFileInfo.CreationTimeUTCTicks = creationTimeUTCTicks;

                            part.Log.Debug.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' creation time has been updated to '{creationTimeUTC.CvtToString(Dates.DateTimeFormat.RoundTrip)}' [from:{new DateTime(entryCreationTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)}]");
                        }

                        if (pendingLastWriteTimeUTCTicksUpdate != null && !usingSourceSide)
                        {
                            var entryLastWriteTimeUTCTicks = ft.destinationSyncFileInfo.LastWriteTimeUTCTicks;

                            var lastWriteTimeUTCTicks = pendingLastWriteTimeUTCTicksUpdate ?? 0;
                            pendingLastWriteTimeUTCTicksUpdate = null;

                            var lastWriteTimeUTC = new DateTime(lastWriteTimeUTCTicks, DateTimeKind.Utc);

                            System.IO.File.SetLastWriteTime(ft.fullDestinationPath, lastWriteTimeUTC);
                            ft.destinationSyncFileInfo.LastWriteTimeUTCTicks = lastWriteTimeUTCTicks;

                            // if the time difference is large enough then log at debug otherwise log at trace level.
                            var absTimeDiff = new TimeSpan(Math.Abs(lastWriteTimeUTCTicks - entryLastWriteTimeUTCTicks));
                            var emitter = part.Log.Emitter((absTimeDiff > (100.0).FromMilliseconds()) ? Logging.MesgType.Debug : Logging.MesgType.Trace);

                            emitter.Emit($"File {ft.fileID}:'{ft.fileNameAndRelativePath}' last write time has been updated to '{lastWriteTimeUTC.CvtToString(Dates.DateTimeFormat.RoundTrip)}' [from:{new DateTime(entryLastWriteTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)}]");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        part.DisableFileTracker(ft, $"{CurrentMethodName} failed: {ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace)}");
                    }

                    pendingSetLength = null;
                    pendingCreationTimeUTCTicksUpdate = null;
                    pendingLastWriteTimeUTCTicksUpdate = null;
                }
            }

            private OpenFileTracker openFileTracker = default;

            #endregion
        }
    }

    namespace Common
    {
        /// <summary>
        /// This enumeration specifies the behavior that is used to update (incrementally update) the destination file from the source.
        /// <para/>AppendAsNeeded (0 - default), RecopyOnChange
        /// </summary>
        public enum FileSyncUpdateSelection : int
        {
            /// <summary>
            /// All files are copied incrementally until the destination file reaches the same length as the source.  
            /// Source files that are observed to get longer restart the incremental append operation by generally retaining the destination contents on the assumption that only the appended bytes changed in the source.
            /// The destiation manager is allowed to retransfer old bytes as desired but it is not required to and it expects that any such re-copy will not change the contents of the previously transfered bytes.
            /// <para/>default
            /// </summary>
            AppendAsNeeded = 0,

            /// <summary>
            /// Any recognized content change (length, last write time, create time) cause the destination manager to truncate the file and then start an
            /// incremental append operation to repopulate its entire contents.
            /// </summary>
            RecopyOnAnyChange,
        }

        /// <summary>
        /// This enumeration is used to select the desired handling of file times in the destination.
        /// <para/>None (0), UpdateCreationTimeFromSource (default), UpdateCreationAndLastWriteTimesFromSource, 
        /// </summary>
        public enum DestinationFileTimeHandling : int
        {
            /// <summary>Selects that neither the source Creation time nor its LastWrite time values will be propagated to file created in the destination</summary>
            None = 0,

            /// <summary>Selects that the Creation time from the source (initial and as it might be changed) will be popagated to the destination but that the last write time will be unchanged</summary>
            UpdateCreationTimeFromSource,

            /// <summary>Selects that both the Creation and LastWrite times will be propagated from the source to the destination.</summary>
            UpdateCreationAndLastWriteTimesFromSource,
        }

        /// <summary>
        /// This interface is supported by all file sync client parts
        /// </summary>
        public interface IFileSyncClient : ISyncActionFactory, IActivePartBase
        { }

        /// <summary>
        /// This interface defines a sync action factory method.
        /// </summary>
        public interface ISyncActionFactory
        {
            /// <summary>
            /// Action factory method.  When run this action will attempt to perform the requested sync operation based on the given <paramref name="syncFlags"/>. 
            /// If the provider supports it, this action will limit its nominal maximum run time to the given <paramref name="nominalWaitTimeLimit"/> when it is non-zero.
            /// </summary>
            IClientFacet Sync(TimeSpan nominalWaitTimeLimit = default, SyncFlags syncFlags = default);
        }

        /// <summary>
        /// This flag enumeration is used to specify the synchronization operation characteristics that the client would like the provider to perform.
        /// <para/>Default (0x00), Full (0x01), Recent (0x02)
        /// </summary>
        [Flags, DataContract(Namespace = MosaicLib.Constants.ToolsLibNameSpace)]
        public enum SyncFlags
        {
            /// <summary>Placeholder default sync request - used to confirm that the provider has completed a normal background update iteration and wait until any related pending deliveries are complete [0x00]</summary>
            [EnumMember]
            Default = 0x00,

            /// <summary>Full sync request - used to request the provider to fully rescan all of the files and wait for all related transfers to be complete.  [0x01]</summary>
            [EnumMember]
            Full = 0x01,
        }

        [MessagePackObject]
        public class ClientRequest
        {
            /// <summary>sequential client transaction sequence numbers. -1 is used to explicitly close the connection.</summary>
            [Key(0)]
            public long ClientSeqNum { get; set; }
            /// <summary>Client provides this to indicate the SyncInfoSeqNum after which new SyncFileInfo items should be returned in the SyncFileInfoArray.  Use -1 to indicate that no such values should be returned.</summary>
            [Key(1)]
            public long RequestFirstSyncFileInfoSeqNum { get; set; } = -1;
            /// <summary>The client provides this to indicate which FileIDs it has already been made aware of and thus does not need to see the names for again.</summary>
            [Key(2)]
            public long IncludeFileNamesAfterFileID { get; set; } = -1;
            /// <summary>Gives the FileID that the client would like an explicit SyncFileInfo update for and usually would like to perform an incremental read for</summary>
            [Key(3)]
            public long FileID { get; set; }
            /// <summary>Gives the offset into the file that the client would like to start reading from (if RequestTransferSize is > 0)</summary>
            [Key(4)]
            public long FileOffset { get; set; }
            /// <summary>Gives the number of bytes that the client would like to read for the given FileID and starting at the given FileOffset.</summary>
            [Key(5)]
            public int RequestedTransferSize { get; set; }
            /// <summary>This flag is true if the client is performing a non-default sync request and would like the server's TcpClient specific task to ask the main part to run a Sync before obtaining the udpated SyncFileInfoArray that is typically generated in these requests.</summary>
            [Key(6)]
            public bool SyncRequest { get; set; }

            /// <summary>gives the lower order 32 bits from the RequestFirstSyncFileInfoSeqNum - used to quickly detect if there are no new SyncFileInfo objects that might need to be returned.</summary>
            [IgnoreMember]
            public uint RequestFirstSyncFileInfoSeqNumU4 { get { return unchecked((uint)RequestFirstSyncFileInfoSeqNum); } }
        }

        /// <summary></summary>
        [MessagePackObject]
        public class ServerResponse
        {
            /// <summary>Gives a copy of the client provided ClientSeqNum transaction sequence number.</summary>
            [Key(0)]
            public long ClientSeqNum { get; set; }
            /// <summary>Gives the most recently generated SyncFileInfo SeqNum in the server.  When this is greater than the last client provided RequestFirstSyncFileInfoSeqNum, it indicates that the server is aware of new SyncFileInfo records for updates to files it has observed that the client is not yet aware of.</summary>
            [Key(1)]
            public long NewestSyncFileInfoSeqNum { get; set; }
            /// <summary>When the client requests a SyncFileInfo update, this array contains all of the newly generated SyncFileInfo instances that came at or after the requested SyncFileInfo sequence number.  This value will be null if the client gave -1 for the RequestFirstSyncFileInfoSeqNum</summary>
            [Key(2)]
            public SyncFileInfo[] SyncFileInfoArray { get; set; }
            /// <summary>Gives the SyncFileInfo record (if any) for the client's requested FileID or null if the client request did not specify any specific FileID.</summary>
            [Key(3)]
            public SyncFileInfo SyncFileInfo { get; set; }
            /// <summary>Gives the offset in the file at which the data present in this response was read.</summary>
            [Key(4)]
            public long FileOffset { get; set; }
            /// <summary>Gives the array in which read file data is stored and serialized/deserialized.  The size of this array is often rounded up to a power of 2 but will not generally exceed 65536.</summary>
            [Key(5)]
            public byte[] Data { get; set; }
            /// <summary>Gives the number of bytes of data that came from the file and are currently in the Data buffer.  This value may be less than the Data buffer array length as the client often requests to read more bytes than the file actually contains.</summary>
            [Key(6)]
            public int DataCount { get; set; }
            /// <summary>Optional (may be null): When this is non-empty it gives the error message that was produced when attempting to perform the corresponding client request (typically asking to read part of a file)</summary>
            [Key(7)]
            public string ResultCode { get; set; }
            /// <summary>This is true when the data read includes the last byte in the file.</summary>
            [Key(8)]
            public bool EndOfFileReached { get; set; }

            public void Clear()
            {
                ClientSeqNum = 0;
                NewestSyncFileInfoSeqNum = 0;
                SyncFileInfoArray = default;
                SyncFileInfo = default;
                FileOffset = 0;
                Data = default;
                DataCount = 0;
                EndOfFileReached = false;
                ResultCode = default;
            }
        }

        /// <summary></summary>
        [MessagePackObject]
        public class SyncFileInfo
        {
            /// <summary>Gives the server assigned ID for the file that this information is related to.</summary>
            [Key(0)]
            public long FileID { get; set; }
            /// <summary>Gives the partial path from the configured root directory to the file itself.  For incremental file content updates, this property will be null</summary>
            [Key(1)]
            public string FileNameAndRelativePath { get; set; }
            /// <summary>Gives the server assigned sequence number for this SyncFileInfo.  This can be used to produce a strict time ordering for a set of two or more instances of this class.</summary>
            [Key(2)]
            public long SeqNum { get; set; }
            /// <summary>Gives the known file length in bytes at the time that this information was captured</summary>
            [Key(3)]
            public long FileLength { get; set; }
            /// <summary>Gives the known file creation time as UTC DateTime Ticks.</summary>
            [Key(4)]
            public long CreationTimeUTCTicks { get; set; }
            /// <summary>Gives the known last modified or last written time for the file as UTC DateTime Ticks.</summary>
            [Key(5)]
            public long LastWriteTimeUTCTicks { get; set; }
            /// <summary>Is true if the file exists and false in a SyncFileInfo response if the file for the given FileID is not longer present under the server's root directory.</summary>
            [Key(6)]
            public bool Exists { get; set; }

            public SyncFileInfo() { }

            public SyncFileInfo(FileInfo fileInfo, long seqNum, long fileID, string fileNameAndRelativePath)
            {
                FileID = fileID;
                FileNameAndRelativePath = fileNameAndRelativePath;
                SeqNum = seqNum;
                FileLength = fileInfo.Length;
                CreationTimeUTCTicks = fileInfo.CreationTimeUtc.Ticks;
                LastWriteTimeUTCTicks = fileInfo.LastWriteTimeUtc.Ticks;
                Exists = fileInfo.Exists;
            }

            public SyncFileInfo MakeCopyWithoutFileName()
            {
                var clone = (SyncFileInfo)MemberwiseClone();
                clone.FileNameAndRelativePath = default;
                return clone;
            }

            public override string ToString()
            {
                if (FileNameAndRelativePath.IsNullOrEmpty())
                    return $"fileID:{FileID}:[noFileName] seqNum:{SeqNum} len:{FileLength} cr:{new DateTime(CreationTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)} lw:{new DateTime(LastWriteTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)}";
                else
                    return $"fileID:{FileID}:'{FileNameAndRelativePath}' seqNum:{SeqNum} len:{FileLength} cr:{new DateTime(CreationTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)} lw:{new DateTime(LastWriteTimeUTCTicks, DateTimeKind.Utc).CvtToString(Dates.DateTimeFormat.RoundTrip)}";
            }
        }

        /// <summary>
        /// Constants
        /// </summary>
        public static partial class Constants
        {
            public const int DefaultTCPPort = 22972;
        }

        /// <summary>
        /// Extension Methods
        /// </summary>
        public static partial class ExtensionMethods
        {
            public static bool IsEqual(this FileInfo fileInfo, FileInfo other, bool compareLength = true, bool compareCreationTime = true, bool compareLastWriteTime = true)
            {
                if (object.ReferenceEquals(fileInfo, other))
                    return true;

                if (fileInfo == null || other == null)
                    return false;

                if (compareLength && fileInfo.Length != other.Length)
                    return false;

                if (compareCreationTime && fileInfo.CreationTimeUtc != other.CreationTimeUtc)
                    return false;

                if (compareLastWriteTime && fileInfo.LastWriteTimeUtc != other.LastWriteTimeUtc)
                    return false;

                return true;
            }
        }

        public class SyncFileInfoSortedArray
        {
            public SyncFileInfoSortedArray(int initialCapacity = 128)
            {
                ItemsArray = new SyncFileInfo[initialCapacity];
                ItemsNoNameArray = new SyncFileInfo[initialCapacity];
            }

            public int Count { get; set; }
            public SyncFileInfo[] ItemsArray { get { return _ItemsArray; } set { _ItemsArray = value; } }
            private SyncFileInfo[] _ItemsArray = null;

            public SyncFileInfo[] ItemsNoNameArray { get { return _ItemsNoNameArray; } set { _ItemsNoNameArray = value; } }
            private SyncFileInfo[] _ItemsNoNameArray = null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(SyncFileInfo item, SyncFileInfo itemNoName)
            {
                if (Count >= ItemsArray.Length)
                {
                    var nextSize = Math.Max(ItemsArray.Length << 1, Count + 1);
                    Array.Resize(ref _ItemsArray, nextSize);
                    Array.Resize(ref _ItemsNoNameArray, nextSize);
                }

                ItemsArray[Count] = item;
                ItemsNoNameArray[Count] = itemNoName;
                Count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Remove(long seqNum)
            {
                if (BinarySearch(seqNum, out int index))
                {
                    if (index < Count - 1)
                    {
                        Array.Copy(ItemsArray, index + 1, ItemsArray, index, Count - index - 1);
                        Array.Copy(ItemsNoNameArray, index + 1, ItemsNoNameArray, index, Count - index - 1);
                    }

                    Count -= 1;

                    ItemsArray[Count] = default;
                    ItemsNoNameArray[Count] = default;

                    return true;
                }

                return false;
            }

            public SyncFileInfo[] GetItemsAtOrAfter(long filterSeqNum, long includeFileNamesAfterFileID)
            {
                var resultArray = EmptyArrayFactory<SyncFileInfo>.Instance;

                if (Count > 0)
                {
                    BinarySearch(filterSeqNum, out int startOffset);

                    var newItemsCount = Count - startOffset;
                    if (newItemsCount > 0)
                    {
                        resultArray = new SyncFileInfo[newItemsCount];
                        for (int resultIndex = 0; resultIndex < newItemsCount; resultIndex++)
                        {
                            var syncFileInfoNoName = ItemsNoNameArray[resultIndex + startOffset];

                            if (syncFileInfoNoName.FileID < includeFileNamesAfterFileID)
                                resultArray[resultIndex] = syncFileInfoNoName;
                            else
                                resultArray[resultIndex] = ItemsArray[resultIndex + startOffset];
                        }
                    }
                }

                return resultArray;
            }

            private bool BinarySearch(long key, out int index)
            {
                index = -1;

                if (Count > 0)
                {
                    int firstIndex = 0;
                    int lastIndex = Count - 1;

                    while (firstIndex <= lastIndex)
                    {
                        int testIndex = firstIndex + ((lastIndex - firstIndex) >> 1);
                        var testItem = ItemsArray[testIndex];

                        if (testItem == null)
                        {
                            index = testIndex;
                            return false;
                        }

                        long testValue = testItem.SeqNum;

                        if (key == testValue)
                        {
                            index = testIndex;
                            return true;
                        }
                        else if (key > testValue)
                        {
                            firstIndex = testIndex + 1;
                        }
                        else // key < testValue
                        {
                            lastIndex = testIndex - 1;
                        }
                    }

                    index = firstIndex;
                    return false;
                }

                return false;
            }
        }

        /// <summary>
        /// This is a helper object that is used to manage a variable length buffer array and to act as a task factory that 
        /// allows the asychronous reading of a MP I4 length prefixed block of bytes (usually MP encoded)
        /// </summary>
        public class MPLengthPrefixedBlockReader
        {
            public MPLengthPrefixedBlockReader(int initialBufferSize, CancellationToken cancellationToken) 
            {
                this.cancellationToken = cancellationToken;
                lenPrefixBuffer = new byte[32];
                buffer = new byte[initialBufferSize];
            }

            public void Clear()
            {
                lenPrefixBufferCount = 0;
                bufferCount = 0;
                blockLen = 0;
            }

            public CancellationToken cancellationToken;

            /// <summary>The length of an I4 forced format MP record</summary>
            public const int lenPrefixLen = 5;
            public byte[] lenPrefixBuffer;
            public int lenPrefixBufferCount;

            public int blockLen;

            public byte[] buffer;
            public int bufferCount;

            /// <summary>
            /// Task factory asynchronous method.  Repeatedly runs async Reads on the given <paramref name="tcpStream"/> until it has received the correct number of bytes for a fixed MP I4 field.
            /// Then reads the trailing blockLen using that field, resizes the buffer array if needed to hold the given bufferLen number of bytes,
            /// and then repeatedly runs additional async Reads until it has received the indicated number of bytes into the buffer.
            /// </summary>
            public async Task ReadNextLengthPrefixedBlockAsync(NetworkStream tcpStream)
            {
                Clear();

                while (lenPrefixBufferCount < lenPrefixLen)
                {
                    int rxCount = await tcpStream.ReadAsync(lenPrefixBuffer, lenPrefixBufferCount, lenPrefixLen - lenPrefixBufferCount, cancellationToken);
                    if (rxCount > 0)
                        lenPrefixBufferCount += rxCount;
                    else if (lenPrefixBufferCount == 0)
                        return;
                    else
                        new System.IO.EndOfStreamException($"{Fcns.CurrentMethodName} failed while reading length prefix (got {lenPrefixBufferCount} of {lenPrefixLen} bytes): stream.Read gave zero bytes (socket closed)").Throw();
                }

                blockLen = ExtractMPI4PrefixLength();

                if (buffer.Length < blockLen)
                    System.Array.Resize(ref buffer, blockLen);

                while (bufferCount < blockLen)
                {
                    int rxCount = await tcpStream.ReadAsync(buffer, bufferCount, blockLen - bufferCount, cancellationToken);
                    if (rxCount > 0)
                        bufferCount += rxCount;
                    else
                        new System.IO.EndOfStreamException($"{Fcns.CurrentMethodName} failed while reading {blockLen} block: stream.Read gave zero bytes (socket closed)").Throw();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int ExtractMPI4PrefixLength()
            {
                var mpReader = new MessagePackReader(new ReadOnlySequence<byte>(lenPrefixBuffer, 0, lenPrefixBufferCount));
                return mpReader.ReadInt32();
            }
        }
    }
}
