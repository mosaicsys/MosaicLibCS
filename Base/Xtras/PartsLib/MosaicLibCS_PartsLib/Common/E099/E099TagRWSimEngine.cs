//-------------------------------------------------------------------
/*! @file E099TagRWSimEngine.cs
 *  @brief an active part that support simulation of an Readonly or Read/Write Tag (such as a TIRIS tag).
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2014 Mosaic Systems Inc.
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
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;
using MosaicLib.Utils;
using MosaicLib.Utils.Collections;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;

using LPM = MosaicLib.PartsLib.Common.LPM;
using MosaicLib.PartsLib.Common.LPM;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Interconnect.Values;

namespace MosaicLib.PartsLib.Common.E099.Sim
{
    public interface ITagRWSimEngine : IActivePartBase
    {
        ITagRWSimEngineState State { get; }
        INotificationObject<ITagRWSimEngineState> StateNotifier { get; }

        IReadPagesAction CreateReadPagesAction(int startPageIdx, int numPages);
        IBasicAction CreateWritePagesAction(ITagPageContents [] pages);
        IBasicAction CreateIncrementCounterAction();
        IBasicAction CreateNoteCarrierHasBeenRemovedAction();
    }

    public interface IReadPagesAction : Modular.Action.IClientFacetWithResult<ITagPageContents []> {}

	public enum E099TagRWSimEngineMode : int
	{
        /// <summary>The ID portion of the page storage is extracted directly from the page contents.  There is no counter.</summary>
		IDOnly,
        /// <summary>The default ID portion of the page storage is extracted on startup from the persisted copy of the pages.  A 1 Digit count is overlayed on the pages and the tag.  Count is incremented, and page overlay is updated each time the carrier is removed from the corresponding PDO</summary>
        IDWith1DigitCounter,
        /// <summary>The default ID portion of the page storage is extracted on startup from the persisted copy of the pages.  A 2 Digit count is overlayed on the pages and the tag.  Count is incremented, and page overlay is updated each time the carrier is removed from the corresponding PDO</summary>
        IDWith2DigitCounter,
        /// <summary>The default ID portion of the page storage is extracted on startup from the persisted copy of the pages.  A 3 Digit count is overlayed on the pages and the tag.  Count is incremented, and page overlay is updated each time the carrier is removed from the corresponding PDO</summary>
        IDWith3DigitCounter,
        /// <summary>The default ID portion of the page storage is extracted on startup from the persisted copy of the pages.  A 4 Digit count is overlayed on the pages and the tag.  Count is incremented, and page overlay is updated each time the carrier is removed from the corresponding PDO</summary>
        IDWith4DigitCounter,
	}

    /// <summary>
    /// Defines the technology that the tag read/write engine is using.  The technology normally determines a number of details about how the tags operate.
    /// <para/>None=0, TIRIS
    /// </summary>
    public enum ReaderType : int
    {
        /// <summary>Value to use when there is no Reader installed</summary>
        None = 0,
        /// <summary>Devices that follow the TI HDX transponder (used to be called TIRIS) with related signaling, protocol, and operation.</summary>
        TIRIS = 1,
    }

	public class TagRWSimEngineConfig
	{
        public ReaderType ReaderType 
        { 
            get { return readerType; }
            set
            {
                readerType = value;
                switch (value)
                {
                    case ReaderType.TIRIS: 
                        if (NumPages == 0) 
                            NumPages = 17; 
                        if (PageDataSize == 0)
                            PageDataSize = 8;       // this does not include the embedded check/metadata bytes (believe that this is 3 for a total of 11 per page).
                        break;
                    default: break;
                }
            }
        }
        private ReaderType readerType;

        [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
        public int NumPages { get; set; }

        [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
        public int PageDataSize { get; set; }

        [ConfigItem(IsOptional = true)]
        public double PageReadDelay { get; set; }   // defaults to 0.0, used value is capped at 0.5

        [ConfigItem(IsOptional = true)]
        public double PageWriteDelay { get; set; }   // defaults to 0.0, used value is capped at 1.0

        public TimeSpan PageReadDelayTimeSpan { get { return TimeSpan.FromSeconds(Math.Min(PageWriteDelay, 0.500)); } }      // if you read all the pages this way it can be quite slow...
        public TimeSpan PageWriteDelayTimeSpan { get { return TimeSpan.FromSeconds(Math.Min(PageWriteDelay, 1.000)); } }      // if you write all the pages this way it can be quite slow...

        [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
        public String InitialPageContentsHex { get; set; }

        [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
        public String InitialPageContentsStrOverlay { get; set; }

        public int InitialCounterPutOffset { get { return (IDStartOffset + (InitialPageContentsStrOverlay ?? String.Empty).Length); } }
        
        public byte[] InitialPageContentsByteArray
        {
            get
            {
                byte[] byteArray = new byte[NumPages * PageDataSize];

                byteArray.SafePut(0, Utils.ByteArrayTranscoders.HexStringTranscoder.Decode(InitialPageContentsHex));
                byteArray.SafePut(IDStartOffset, Utils.ByteArrayTranscoders.ByteStringTranscoder.Decode(InitialPageContentsStrOverlay));

                return byteArray;
            }
        }

		[ConfigItem(IsOptional=true)]
		public int IDStartOffset { get; set; }

        [ConfigItem(IsOptional=true)]
		public int IDSize	{ get; set; }

        [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
        public int InitialCounterValue { get; set; }

        [ConfigItem(IsOptional=true)]
		public E099TagRWSimEngineMode Mode { get; set; }

        public TagRWSimEngineConfig()
        {
            ReaderType = ReaderType.None;
            NumPages = 0;
            PageDataSize = 0;
            PageReadDelay = 0.0;
            PageWriteDelay = 0.0;
            IDStartOffset = 0;
            IDSize = 0;
            InitialCounterValue = 0;
            InitialPageContentsHex = String.Empty;
            InitialPageContentsStrOverlay = String.Empty;
            Mode = E099TagRWSimEngineMode.IDOnly;
        }

		public TagRWSimEngineConfig(TagRWSimEngineConfig rhs)
		{
            ReaderType = rhs.ReaderType;
            NumPages = rhs.NumPages;
            PageDataSize = rhs.PageDataSize;
            PageReadDelay = rhs.PageReadDelay;
            PageWriteDelay = rhs.PageWriteDelay;
			IDStartOffset = rhs.IDStartOffset;
			IDSize = rhs.IDSize;
            InitialCounterValue = rhs.InitialCounterValue;
            InitialPageContentsHex = rhs.InitialPageContentsHex;
            InitialPageContentsStrOverlay = rhs.InitialPageContentsStrOverlay;
			Mode = rhs.Mode;
		}
		
		public bool IsEqualTo(TagRWSimEngineConfig rhs)
		{
			return (rhs != null
                    && ReaderType == rhs.ReaderType
                    && NumPages == rhs.NumPages
                    && PageDataSize == rhs.PageDataSize
                    && PageReadDelay == rhs.PageReadDelay
                    && PageWriteDelay == rhs.PageWriteDelay
					&& IDStartOffset == rhs.IDStartOffset
					&& IDSize == rhs.IDSize
                    && InitialCounterValue == rhs.InitialCounterValue
                    && InitialPageContentsHex == rhs.InitialPageContentsHex
                    && InitialPageContentsStrOverlay == rhs.InitialPageContentsStrOverlay
					&& Mode == rhs.Mode
					);
		}
	}

    public interface ITagRWSimEngineState
    {
        TagRWSimEngineConfig Config { get; }
        byte[] ContentByteArray { get; }
        string ID { get; }
        int Count { get; }

        /// <summary>Indicates that the user has set/cleared that a tag is actually present.  Tag read and write commands will fail if this is not true.</summary>
        bool TagIsPresent { get; }
        bool CounterIsEnabled { get; }

        ITagPageContents[] Pages { get; }

        bool IsEqualTo(ITagRWSimEngineState rhs);
    }

    public class TagRWSimEngineState : ITagRWSimEngineState
    {
        public TagRWSimEngineConfig Config { get; internal set; }

        public byte[] ContentByteArray { get; internal set; }
        private static readonly byte[] EmptyByteArray = EmptyArrayFactory<byte>.Instance;

        public int Count { get; internal set; }

        public bool CounterIsEnabled { get { return rawCounterIsEnabled && (Config.Mode != E099TagRWSimEngineMode.IDOnly); } internal set { rawCounterIsEnabled = value; } }
        private bool rawCounterIsEnabled = true;
        public bool TagIsPresent{ get; internal set; }            // may be used by a client to determine if a page read or write command should fail

        public string ID 
        {
            get
            {
                byte[] rawTagContentByteArray = ContentByteArray.SafeSubArray(Config.IDStartOffset, Config.IDSize);

                int startIdx = 0;
                int count = rawTagContentByteArray.Length;

                // trim any leading nulls
                while (count > 0 && rawTagContentByteArray[startIdx] == 0)
                {
                    startIdx++;
                    count--;
                }

                // trim any trailing nulls
                while (count > 0 && rawTagContentByteArray[startIdx + count - 1] == 0)
                    count--;

                // check for internal nulls and further trim the ID if an internal null is found
                for (int trialCount = count; trialCount > 0; trialCount -= 1)
                {
                    if (rawTagContentByteArray[startIdx + trialCount - 1] == 0)
                        count = trialCount-1;
                }

                byte[] zeroTrimmedTagContentByteArray = rawTagContentByteArray.SafeSubArray(startIdx, count);

                string trimmedTagContent = Utils.ByteArrayTranscoders.ByteStringTranscoder.Encode(zeroTrimmedTagContentByteArray).Trim();

                return trimmedTagContent;
            }
        }

        public ITagPageContents[] Pages { get; private set; }

        internal void BuildPages()
        {
            Pages = Enumerable.Range(0, Config.NumPages).Select(pageIdx => GetPage(pageIdx)).ToArray();
        }

        private ITagPageContents GetPage(int pageIndex)
        {
            int pageStartOffset = pageIndex * Config.PageDataSize;

            TagPageContents page = new TagPageContents() { PageIndex = pageIndex, ByteArray = ContentByteArray.SafeSubArray(pageStartOffset, Config.PageDataSize) };

            return page;
        }

        internal void UpdateCounterPostfix()
        {
            string countStr = String.Empty;

            if (CounterIsEnabled)
            {
                switch (Config.Mode)
                {
                    default:
                    case E099TagRWSimEngineMode.IDOnly: return;    // there is nothing to do for these modes
                    case E099TagRWSimEngineMode.IDWith1DigitCounter: countStr = (Count % 10).ToString("d1"); break;
                    case E099TagRWSimEngineMode.IDWith2DigitCounter: countStr = (Count % 100).ToString("d2"); break;
                    case E099TagRWSimEngineMode.IDWith3DigitCounter: countStr = (Count % 1000).ToString("d3"); break;
                    case E099TagRWSimEngineMode.IDWith4DigitCounter: countStr = (Count % 10000).ToString("d4"); break;
                }

                byte[] countAsByteArray = Utils.ByteArrayTranscoders.ByteStringTranscoder.Decode(countStr);
                ContentByteArray.SafePut(Config.InitialCounterPutOffset, countAsByteArray);
                BuildPages();
            }
        }

        public TagRWSimEngineState()
        {
            Config = new TagRWSimEngineConfig() { ReaderType = ReaderType.TIRIS, Mode = E099TagRWSimEngineMode.IDOnly };
            ContentByteArray = EmptyByteArray;
            Count = 0;
            TagIsPresent = false;
            BuildPages();
        }

        public TagRWSimEngineState(TagRWSimEngineState rhs) 
        {
            Config = new TagRWSimEngineConfig(rhs.Config);
            ContentByteArray = rhs.ContentByteArray.Clone() as byte [];
            Count = rhs.Count;
            TagIsPresent = rhs.TagIsPresent;
            rawCounterIsEnabled = rhs.rawCounterIsEnabled;
            BuildPages();
        }

        public bool IsEqualTo(ITagRWSimEngineState rhs)
        {
            return (Config.IsEqualTo(rhs.Config)
                    && ContentByteArray.IsEqualTo(rhs.ContentByteArray)
                    && Count == rhs.Count
                    && TagIsPresent == rhs.TagIsPresent
                    && CounterIsEnabled == rhs.CounterIsEnabled
                    );
        }
    }

    public class E099TagRWSimEngine : SimpleActivePartBase, ITagRWSimEngine
    {
        #region Construction

        /// <summary>
        /// Constructor for use without an lpmSimPart.  Caller provides full part name and object does not look for carrier removed events.
        /// </summary>
        public E099TagRWSimEngine(string partID)
            :this(partID, null)
        { }

        protected E099TagRWSimEngine(string partID, IValuesInterconnection ivi)
            : base(partID, initialSettings: SimpleActivePartBaseSettings.DefaultVersion2.Build(partBaseIVI: ivi))
        {
            ActionLoggingConfig = Modular.Action.ActionLoggingConfig.Info_Error_Trace_Trace;    // redefine the log levels for actions 

            IVI = ivi ?? Values.Instance;

            //This part is a simulated primary part
            PrivateBaseState = new BaseState(true, true) { ConnState = ConnState.NotApplicable };

            TagRWSimEngineConfig defaultConfig = new TagRWSimEngineConfig() { ReaderType = ReaderType.TIRIS, Mode = E099TagRWSimEngineMode.IDOnly };
            configAccessAdapter = new ConfigValueSetAdapter<TagRWSimEngineConfig>() { ValueSet = defaultConfig, SetupIssueEmitter = Log.Error, UpdateIssueEmitter = Log.Error, ValueNoteEmitter = Log.Debug }.Setup(PartID + ".");
            configAccessAdapter.UpdateNotificationList.AddItem(threadWakeupNotifier);
            AddExplicitDisposeAction(() => configAccessAdapter.UpdateNotificationList.RemoveItem(threadWakeupNotifier));

            InitializePrivateState();

            PublishBaseState("Constructor.Complete");
        }

        private IValuesInterconnection IVI { get; set; }

        #endregion

        #region Config and persistent storage

        ConfigValueSetAdapter<TagRWSimEngineConfig> configAccessAdapter;
        TagRWSimEngineConfig CurrentConfig { get { return configAccessAdapter.ValueSet; } }

        #endregion

        #region public state and interface methods

        TagRWSimEngineState privateState;
        IValueAccessor stateIVA;
        InterlockedNotificationRefObject<ITagRWSimEngineState> publicStateNotifier = new InterlockedNotificationRefObject<ITagRWSimEngineState>();
        public INotificationObject<ITagRWSimEngineState> StateNotifier { get { return publicStateNotifier; } }
        public ITagRWSimEngineState State { get { return publicStateNotifier.Object; } }

        IValueAccessor tagIsPresentIVA;
        IValueAccessor counterIsEnabledIVA;
        IValueAccessor isOnlineIVA;

        protected void PublishPrivateState()
        {
            ITagRWSimEngineState pubObj = new TagRWSimEngineState(privateState) as ITagRWSimEngineState;

            stateIVA.Set(pubObj);
            publicStateNotifier.Object = pubObj;
        }

        private void InitializePrivateState()
        {
            privateState = new TagRWSimEngineState()
            {
                Config = new TagRWSimEngineConfig(CurrentConfig),
                ContentByteArray = (byte[])CurrentConfig.InitialPageContentsByteArray.Clone(),
                Count = CurrentConfig.InitialCounterValue,
                TagIsPresent = true,
            };

            stateIVA = IVI.GetValueAccessor("{0}.State".CheckedFormat(PartID));
            tagIsPresentIVA = IVI.GetValueAccessor("{0}.TagIsPresent".CheckedFormat(PartID)).Set(privateState.TagIsPresent);
            counterIsEnabledIVA = IVI.GetValueAccessor("{0}.CounterIsEnabled".CheckedFormat(PartID)).Set(true);
            isOnlineIVA = IVI.GetValueAccessor("{0}.IsOnline".CheckedFormat(PartID)).Set(true);

            privateState.UpdateCounterPostfix();

            PublishPrivateState();
        }

        #endregion

        #region Internal implementation

        protected override void PerformMainLoopService()
        {
            base.PerformMainLoopService();

            if (configAccessAdapter.IsUpdateNeeded)
            {
                configAccessAdapter.Update();

                privateState.Config.IDSize = configAccessAdapter.ValueSet.IDSize;
                privateState.Config.IDStartOffset = configAccessAdapter.ValueSet.IDStartOffset;
                privateState.Config.Mode = configAccessAdapter.ValueSet.Mode;

                privateState.UpdateCounterPostfix();

                PublishPrivateState();
            }

            if (counterIsEnabledIVA.IsUpdateNeeded || tagIsPresentIVA.IsUpdateNeeded)
            {
                privateState.CounterIsEnabled = counterIsEnabledIVA.Update().VC.GetValue<bool>(false);
                privateState.TagIsPresent = tagIsPresentIVA.Update().VC.GetValue<bool>(false);

                PublishPrivateState();
            }

            if (isOnlineIVA.IsUpdateNeeded)
            {
                if (isOnlineIVA.Update().VC.GetValue<bool>(false))
                    PerformGoOnlineAction(false);
                else
                    PerformGoOfflineAction();
            }
        }

        int carrierRemovedCounter = 0;

        protected void CarrierHasBeenRemoved()
        {
            IncrementCount();

            carrierRemovedCounter++;
        }

        private void IncrementCount()
        {
            if (privateState.CounterIsEnabled)
            {
                privateState.Count++;
                privateState.UpdateCounterPostfix();

                PublishPrivateState();
            }
        }

        #endregion

        #region ITagRWSimEngine Members and related classes

        protected override string PerformGoOnlineAction(bool andInitialize)
        {
            if (andInitialize)
            {
                InitializePrivateState();
            }

            SetBaseState(UseState.Online, "Has been set online" + (andInitialize ? " and Initialized" : ""), true);
            isOnlineIVA.Set(true);

            return String.Empty;
        }

        protected override string PerformGoOfflineAction()
        {
            SetBaseState(UseState.Offline, "Has been set offline", true);
            isOnlineIVA.Set(false);

            return String.Empty;
        }

        public TagReaderType TagReaderType
        {
            get { return (TagReaderType.Reader | TagReaderType.Writer | TagReaderType.TIRIS); }
        }

        private class ReadPagesAction : Modular.Action.ActionImplBase<NullObj, ITagPageContents[]>, IReadPagesAction
        {
            public ReadPagesAction(ActionQueue actionQ, ActionMethodDelegateActionArgStrResult<NullObj, ITagPageContents[]> method, ActionLogging logging) 
                : base(actionQ, null, true, method, logging) 
            { }
        }

        public IReadPagesAction CreateReadPagesAction(int startPageIdx, int numPages)
        {
            ActionMethodDelegateActionArgStrResult<NullObj, ITagPageContents[]> method = ((providerFacet) => PerformReadPagesAction(providerFacet, startPageIdx, numPages));
            IReadPagesAction clientFacet = new ReadPagesAction(actionQ, method, new ActionLogging(Fcns.CheckedFormat("ReadPages(startPageIdx:{0}, numPages:{1})", startPageIdx, numPages), ActionLoggingReference));

            return clientFacet;
        }

        protected string PerformReadPagesAction(IProviderActionBase<NullObj, ITagPageContents[]> action, int startPageIdx, int numPages)
        {
            List<ITagPageContents> pageContentsList = new List<ITagPageContents>();

            // initialize the result to the empty array.
            action.ResultValue = pageContentsList.ToArray();

            if (!BaseState.IsOnline)
                return "Reader is not online";

            if (!privateState.TagIsPresent)
                return "Tag (or Antenna) is not marked as present";

            foreach (int pageIdx in Enumerable.Range(startPageIdx, numPages))
            {
                if (pageIdx < 0 || pageIdx >= CurrentConfig.NumPages)
                {
                    action.ResultValue = pageContentsList.ToArray();

                    return Fcns.CheckedFormat("Reached invalid page at index:{0} [limit:{1}]", pageIdx, privateState.Config.NumPages);
                }

                int pageStartOffset = pageIdx * privateState.Config.PageDataSize;
                ITagPageContents pageContents = new TagPageContents() { PageIndex = pageIdx, ByteArray = privateState.ContentByteArray.SafeSubArray(pageStartOffset, privateState.Config.PageDataSize) };

                pageContentsList.Add(pageContents);

                if (privateState.Config.PageReadDelayTimeSpan != TimeSpan.Zero)
                    System.Threading.Thread.Sleep(privateState.Config.PageReadDelayTimeSpan);
            }

            action.ResultValue = pageContentsList.ToArray();

            return String.Empty;
        }

        private readonly static ITagPageContents[] EmptyPageContentsArray = EmptyArrayFactory<ITagPageContents>.Instance;

        public IBasicAction CreateWritePagesAction(ITagPageContents[] pages)
        {
            string pageNumListStr = String.Join(",", pages.Select((page) => page.PageIndex).Select((pageIdx) => Fcns.CheckedFormat("${0:x2}", pageIdx + 1)).ToArray());
            ActionMethodDelegateStrResult method = (() => PerformWritePageAction(pages));
            IBasicAction clientFacet = new BasicActionImpl(actionQ, method, Fcns.CheckedFormat("WritePages({0})", pageNumListStr), ActionLoggingReference);

            return clientFacet;
        }

        protected string PerformWritePageAction(ITagPageContents[] pages)
        {
            if (!BaseState.IsOnline)
                return "Reader is not online";

            if (!privateState.TagIsPresent)
                return "Tag (or Antenna) is not marked as present";

            String ec = null;

            foreach (ITagPageContents page in pages ?? EmptyPageContentsArray)
            {
                int pageIndex = (page != null ? page.PageIndex : 0);

                if (page == null)
                    ec = "encoutered invalid null page contents object";
                else if (pageIndex < 0 || pageIndex >= CurrentConfig.NumPages)
                    ec = Fcns.CheckedFormat("Encoutered invalid pageIdx:{0}", pageIndex);
                else if (page.ByteArray == null)
                    ec = Fcns.CheckedFormat("Page at pageIdx:{0} has null data ByteArray", pageIndex);
                else if (page.ByteArray.Length != CurrentConfig.PageDataSize)
                    ec = Fcns.CheckedFormat("Page at pageIdx:{0} has invalid data ByteArray (length:{1} is not expected value of {2}", pageIndex, page.ByteArray.Length, CurrentConfig.PageDataSize);

                if (String.IsNullOrEmpty(ec))
                {
                    int putOffset = pageIndex * CurrentConfig.PageDataSize;

                    page.ByteArray.CopyTo(privateState.ContentByteArray, putOffset);

                    if (privateState.Config.PageWriteDelayTimeSpan != TimeSpan.Zero)
                        System.Threading.Thread.Sleep(privateState.Config.PageWriteDelayTimeSpan);
                }
                else
                {
                    break;
                }
            }

            privateState.UpdateCounterPostfix();
            PublishPrivateState();

            return Fcns.MapNullToEmpty(ec);
        }

        public IBasicAction CreateIncrementCounterAction()
        {
            IBasicAction clientFacet = new BasicActionImpl(actionQ, PerformIncrementCounterAction, "IncrementCount", ActionLoggingReference);

            return clientFacet;
        }

        protected string PerformIncrementCounterAction()
        {
            IncrementCount();
            return String.Empty;
        }

        public IBasicAction CreateNoteCarrierHasBeenRemovedAction()
        {
            return new BasicActionImpl(actionQ, PerformNoteCarrierHasBeenRemovedAction, "NoteCarrierHasBeenRemoved", ActionLoggingReference);
        }

        protected string PerformNoteCarrierHasBeenRemovedAction()
        {
            CarrierHasBeenRemoved();
            return string.Empty;
        }

        #endregion
    }
}

//-------------------------------------------------------------------
