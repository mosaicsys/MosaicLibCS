//-------------------------------------------------------------------
/*! @file E099TagRWSimEngine.cs
 *  @brief an active part that support simulation of an Readonly or Read/Write Tag (such as a TIRIS tag).
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2014 Mosaic Systems Inc., All rights reserved
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
//-------------------------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;
using MosaicLib.Utils;
using MosaicLib.Time;
using MosaicLib.Modular;
using MosaicLib.Modular.Part;
using MosaicLib.PartsLib.Helpers;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;

using LPMSim = MosaicLib.PartsLib.Common.LPM.Sim;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Action;

namespace MosaicLib.PartsLib.Common.E099.Sim
{
    public interface ITagRWSimEngine : IActivePartBase
    {
        ITagRWSimEngineState State { get; }
        INotificationObject<ITagRWSimEngineState> StateNotifier { get; }

        void AttachToLP(LPMSim.ILPMSimPart lpmSimPart);

        IReadPagesAction CreateReadPagesAction(int startPageIdx, int numPages);
        IBasicAction CreateWritePageAction(ITagPageContents [] pages);
        IBasicAction CreateIncrementCounterAction();
    }

    public interface IReadPagesAction : Modular.Action.IClientFacetWithResult<ITagPageContents []> {}

    public interface ITagPageContents
    {
        int PageIndex { get; }
        byte [] ByteArray { get; }

        bool Equals(object rhsAsObject);

        string ToString(TagRWPageContentsStringFormat fmtToUse);
    }

    public enum TagRWPageContentsStringFormat
    {
        HexAndAscii = 0,
        DecimalPageSpaceBytesInHex,
        DecimalPageSpaceQuotedBytesInHex,
        Default = HexAndAscii,
    }

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

        ITagPageContents GetPage(int pageIdx);

        bool IsEqualTo(ITagRWSimEngineState rhs);
    }

    public class TagRWSimEngineState : ITagRWSimEngineState
    {
        public TagRWSimEngineConfig Config { get; internal set; }

        public byte[] ContentByteArray { get; internal set; }
        private static readonly byte[] EmptyByteArray = new byte[0];

        public int Count { get; internal set; }

        public bool TagIsPresent{ get; internal set; }            // may be used by a client to determine if a page read or write command should fail

        public string ID 
        {
            get
            {
                byte[] rawTagContentByteArray = ContentByteArray.SafeAccess(Config.IDStartOffset, Config.IDSize);

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

                byte [] zeroTrimmedTagContentByteArray = rawTagContentByteArray.SafeAccess(startIdx, count);

                string trimmedTagContent = Utils.ByteArrayTranscoders.ByteStringTranscoder.Encode(zeroTrimmedTagContentByteArray).Trim();

                return trimmedTagContent;
            }
        }

        public ITagPageContents GetPage(int pageIndex)
        {
            int pageStartOffset = pageIndex * Config.PageDataSize;

            TagPageContents page = new TagPageContents() { PageIndex = pageIndex, ByteArray = ContentByteArray.SafeAccess(pageStartOffset, Config.PageDataSize) };

            return page;
        }

        internal void UpdateCounterPostfix()
        {
            string countStr = String.Empty;

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
        }

        public TagRWSimEngineState()
        {
            Config = new TagRWSimEngineConfig() { ReaderType = ReaderType.TIRIS, Mode = E099TagRWSimEngineMode.IDOnly };
            ContentByteArray = EmptyByteArray;
            Count = 0;
            TagIsPresent = false;
        }

        public TagRWSimEngineState(TagRWSimEngineState rhs) 
        {
            Config = new TagRWSimEngineConfig(rhs.Config);
            ContentByteArray = rhs.ContentByteArray.Clone() as byte [];
            Count = rhs.Count;
            TagIsPresent = rhs.TagIsPresent;
        }

        public bool IsEqualTo(ITagRWSimEngineState rhs)
        {
            return (Config.IsEqualTo(rhs.Config)
                    && ContentByteArray.IsEqualTo(rhs.ContentByteArray)
                    && Count == rhs.Count
                    && TagIsPresent == rhs.TagIsPresent
                    );
        }
    }

    public class TagPageContents : ITagPageContents
    {
        public int PageIndex { get; set; }
        public byte[] ByteArray { get; set; }

        public override bool Equals(object rhsAsObject)
        {
            ITagPageContents rhs = rhsAsObject as ITagPageContents;
            if (rhs == null)
                return false;

            return (PageIndex == rhs.PageIndex && ByteArray.IsEqualTo(rhs.ByteArray));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return ToString(TagRWPageContentsStringFormat.Default);
        }

        public string ToString(TagRWPageContentsStringFormat fmtToUse)
        {
            switch (fmtToUse)
            {
                case TagRWPageContentsStringFormat.DecimalPageSpaceBytesInHex:
                    // version used by some drivers
                    return Fcns.CheckedFormat("{0:d2} {1}", (PageIndex + 1), ByteArrayTranscoders.HexStringTranscoderNoPadding.Encode(ByteArray));

                case TagRWPageContentsStringFormat.DecimalPageSpaceQuotedBytesInHex:
                    // version used by some drivers
                    return Fcns.CheckedFormat("{0:d2} '{1}'", (PageIndex + 1), ByteArrayTranscoders.HexStringTranscoderNoPadding.Encode(ByteArray));

                case TagRWPageContentsStringFormat.HexAndAscii:
                default:
                    // version used for screen display and logging.
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.CheckedAppendFormat("{0:d2}: {1} [", (PageIndex + 1), ByteArrayTranscoders.HexStringTranscoder.Encode(ByteArray));

                        foreach (byte b in ByteArray)
                        {
                            char c = unchecked((char)b);

                            if (Char.IsLetterOrDigit(c) || Char.IsPunctuation(c) || Char.IsSymbol(c))
                                sb.Append(c);
                            else
                                sb.Append('.');
                        }

                        sb.Append(']');

                        return sb.ToString();
                    }
            }
        }
    }


    public class E099TagRWSimEngine : SimpleActivePartBase, ITagRWSimEngine
    {
        #region Construction

        /// <summary>
        /// Constructor for use wihtout an lpmSimPart.  Caller provides full part name and object does not look for carrier removed events.
        /// </summary>
        public E099TagRWSimEngine(string partID)
            :this(partID, null)
        { }

        /// <summary>
        /// Constructor for use with an lpmSimPart.  PartID = lpmSimPart.PartID + ".E99Sim",  object automatically increments count on carrier removed events.
        /// </summary>
        public E099TagRWSimEngine(LPMSim.ILPMSimPart lpmSimPart)
            : this(lpmSimPart.PartID + ".E99Sim", lpmSimPart)
        { }

        protected E099TagRWSimEngine(string partID, LPMSim.ILPMSimPart lpmSimPart)
            : base(partID)
        {
            ActionLoggingConfig = Modular.Action.ActionLoggingConfig.Info_Error_Trace_Trace;    // redefine the log levels for actions 

            //This part is a simulated primary part
            PrivateBaseState = new BaseState(true, true) { ConnState = ConnState.NotApplicable };

            TagRWSimEngineConfig defaultConfig = new TagRWSimEngineConfig() { ReaderType = ReaderType.TIRIS, Mode = E099TagRWSimEngineMode.IDOnly };
            configAccessAdapter = new ConfigValueSetAdapter<TagRWSimEngineConfig>() { ValueSet = defaultConfig, SetupIssueEmitter = Log.Error, UpdateIssueEmitter = Log.Error, ValueNoteEmitter = Log.Debug }.Setup(PartID + ".");
            configAccessAdapter.UpdateNotificationList.AddItem(threadWakeupNotifier);
            AddExplicitDisposeAction(() => configAccessAdapter.UpdateNotificationList.RemoveItem(threadWakeupNotifier));

            InitializePrivateState();

            PublishBaseState("Constructor.Complete");
        }

        public void AttachToLP(LPMSim.ILPMSimPart lpmSimPart)
        {
            LpmSimPart = lpmSimPart;

            string lpmPartID = lpmSimPart.PartID;

            IBasicNotificationList notificationList = lpmSimPart.PublicStateNotifier.NotificationList;
            notificationList.AddItem(threadWakeupNotifier);
            AddExplicitDisposeAction(() => notificationList.RemoveItem(threadWakeupNotifier));

            lpmSimPartStateObserver = new SequencedRefObjectSourceObserver<LPMSim.State, int>(lpmSimPart.PublicStateNotifier);

            lpmSimPart.TagRWSimEngine = this;
            AddExplicitDisposeAction(() => { lpmSimPart.TagRWSimEngine = null; });
        }

        #endregion

        #region Config and persistent storage

        ConfigValueSetAdapter<TagRWSimEngineConfig> configAccessAdapter;
        TagRWSimEngineConfig CurrentConfig { get { return configAccessAdapter.ValueSet; } }

        #endregion

        #region public state and interface methods

        TagRWSimEngineState privateState;
        InterlockedNotificationRefObject<ITagRWSimEngineState> publicStateNotifier = new InterlockedNotificationRefObject<ITagRWSimEngineState>();
        public INotificationObject<ITagRWSimEngineState> StateNotifier { get { return publicStateNotifier; } }
        public ITagRWSimEngineState State { get { return publicStateNotifier.Object; } }

        protected void PublishPrivateState()
        {
            publicStateNotifier.Object = new TagRWSimEngineState(privateState);
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

            privateState.UpdateCounterPostfix();
            PublishPrivateState();
        }

        #endregion

        #region Internal implementation

        public LPMSim.ILPMSimPart LpmSimPart { get; private set; }

        ISequencedRefObjectSourceObserver<LPMSim.State, int> lpmSimPartStateObserver;
        LPMSim.PodPresenceSensorState LastLPMSimConfirmedPPState = new LPMSim.PodPresenceSensorState();

        protected override void PerformMainLoopService()
        {
            // track the LPMSim pod presance state and detect and handle each time that the carrier has been removed.  
            // Typically this increments the count

            if (lpmSimPartStateObserver != null && lpmSimPartStateObserver.Update())
            {
                LPMSim.State lpmSimState = lpmSimPartStateObserver.Object;
                if (lpmSimState != null && lpmSimState.InputsState.PodPresenceSensorState.DoesPlacedEqualPresent)
                {
                    if (!LastLPMSimConfirmedPPState.IsEqualTo(lpmSimState.InputsState.PodPresenceSensorState))
                    {
                        if (LastLPMSimConfirmedPPState.IsPlacedAndPresent && lpmSimState.InputsState.PodPresenceSensorState.IsNeitherPlacedNorPresent)
                            CarrierHasBeenRemoved();

                        LastLPMSimConfirmedPPState = lpmSimState.InputsState.PodPresenceSensorState;
                    }
                }
            }

            if (configAccessAdapter.IsUpdateNeeded)
            {
                configAccessAdapter.Update();

                privateState.Config.IDSize = configAccessAdapter.ValueSet.IDSize;
                privateState.Config.IDStartOffset = configAccessAdapter.ValueSet.IDStartOffset;
                privateState.Config.Mode = configAccessAdapter.ValueSet.Mode;

                privateState.UpdateCounterPostfix();

                PublishPrivateState();
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
            privateState.Count++;
            privateState.UpdateCounterPostfix();

            PublishPrivateState();
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

            return String.Empty;
        }

        protected override string PerformGoOfflineAction()
        {
            SetBaseState(UseState.Offline, "Has been set offline", true);

            return String.Empty;
        }

        protected override string PerformServiceAction(string serviceName)
        {
            switch (serviceName)
            {
                case "SetTagPresent":
                    privateState.TagIsPresent = true;
                    PublishPrivateState();
                    return String.Empty;
                case "ClearTagPresent":
                    privateState.TagIsPresent = false;
                    PublishPrivateState();
                    return String.Empty;
                default:
                    return base.PerformServiceAction(serviceName);
            }
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
                ITagPageContents pageContents = new TagPageContents() { PageIndex = pageIdx, ByteArray = privateState.ContentByteArray.SafeAccess(pageStartOffset, privateState.Config.PageDataSize) };

                pageContentsList.Add(pageContents);

                if (privateState.Config.PageReadDelayTimeSpan != TimeSpan.Zero)
                    System.Threading.Thread.Sleep(privateState.Config.PageReadDelayTimeSpan);
            }

            action.ResultValue = pageContentsList.ToArray();

            return String.Empty;
        }

        private readonly static ITagPageContents[] EmptyPageContentsArray = new ITagPageContents[0];

        public IBasicAction CreateWritePageAction(ITagPageContents[] pages)
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


        #endregion
    }
}

//-------------------------------------------------------------------
