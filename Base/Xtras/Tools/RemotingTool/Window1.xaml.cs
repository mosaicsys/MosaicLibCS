//-------------------------------------------------------------------
/*! @file Window1.xaml.cs
 *  @brief 
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved
 * Copyright (c) 2018 Mosaic Systems Inc., All rights reserved
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
using System.Collections.Generic;
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

using MosaicLib;
using MosaicLib.Modular.Action;
using MosaicLib.Modular.Common;
using MosaicLib.Modular.Config;
using MosaicLib.Modular.Config.Attributes;
using MosaicLib.Modular.Interconnect.Values;
using MosaicLib.Modular.Interconnect.Remoting;
using MosaicLib.Modular.Interconnect.Remoting.MessageStreamTools;
using MosaicLib.Modular.Interconnect.Sets;
using MosaicLib.Modular.Part;
using MosaicLib.Time;
using MosaicLib.Utils;
using MosaicLib.Utils.StringMatching;
using MosaicLib.WPF.Interconnect;

using Modular = MosaicLib.Modular;
using MosaicLib.WPF.Timers;
using MosaicLib.Semi.E039;
using MosaicLib.WPF.Tools.Sets;

namespace RemotingTool
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Logging.Logger Logger;

        private RemotingClientConfig remotingClientConfig;
        private RemotingClient remotingClient;
        private List<IActivePartBase> partsList = new List<IActivePartBase>();

        private IValuesInterconnection IVI { get; set; }
        private IValuesInterconnection RemoteIVI { get; set; }
        private WPFValueInterconnectAdapter WVIA { get; set; }

        public class ConfigValues
        {
            public ConfigValues()
            {
                ConnectionType = "TCPv4";
                Port = "9000";
                IPAddress = "127.0.0.1";
                TraceSelect = null;
                IVITableName = string.Empty;
                AutoReconnectHoldoff = (10.0).FromSeconds();
                RemoteLogMessageSetName = "LogMessageHistory";
                RemoteLogMessageSetCapacity = 5000;
                RemoteLogMessageSetMaximumItemsPerMessage = 100;
                BufferSize = MosaicLib.Modular.Interconnect.Remoting.Buffers.BufferPool.DefaultBufferSize;
                SessionExpirationPeriod = (1.0).FromMinutes();
            }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public string ConnectionType { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public string Port { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public string IPAddress { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public string TraceSelect { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public TimeSpan AutoReconnectHoldoff { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public string IVITableName { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public string RemoteLogMessageSetName { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public int RemoteLogMessageSetCapacity { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public int RemoteLogMessageSetMaximumItemsPerMessage { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public int BufferSize { get; set; }

            [ConfigItem(IsOptional = true, ReadOnlyOnce = true)]
            public TimeSpan SessionExpirationPeriod { get; set; }
        }

        ConfigValues config = new ConfigValues();

        public Window1()
        {
            Logger = new Logging.Logger("Window1", Logging.LogGate.All);

            IConfig configInstance = Config.Instance;

            new ConfigValueSetAdapter<ConfigValues>(configInstance) { ValueSet = config, SetupIssueEmitter = Logger.Error, ValueNoteEmitter = Logger.Debug }.Setup();

            System.Reflection.Assembly currentExecAssy = System.Reflection.Assembly.GetExecutingAssembly();
            Title = currentExecAssy.FullName.Split(' ').SafeAccess(0).Trim(',');

            IVI = Values.Instance;
            RemoteIVI = new ValuesInterconnection("RemoteIVI");

            InitializeComponent();

            WVIA = new WPFValueInterconnectAdapter(IVI);

            // Remoting part
            remotingClientConfig = new RemotingClientConfig()
            {
                PartID = "Remoting.Client",
                PartIVI = IVI,
                ConfigNVS = new NamedValueSet() 
                { 
                    { "ConnectionType", config.ConnectionType }, 
                    { "Port", config.Port }, 
                    { "IPAddress", config.IPAddress }, 
                    { "Transport.TraceLogger.InitialInstanceLogGate", config.TraceSelect }, 
                    { "AutoReconnectHoldoff", config.AutoReconnectHoldoff },
                    { "BufferPool.BufferSize", config.BufferSize },
                    { "SessionExpirationPeriod", config.SessionExpirationPeriod },
                },
                StreamToolsConfigArray = new MessageStreamToolConfigBase[] 
                { 
                    new ActionRelayMessageStreamToolConfig(),
                    new IVIRelayMessageStreamToolConfig()
                    {
                        ClientIVI = RemoteIVI,
                        RemoteIVIName = config.IVITableName,
                        IVIRelayDirection = IVIRelayDirection.FromServer,
                        ResetClientSideIVAsOnCloseOrFailure = true,
                    },
                    new SetRelayMessageStreamToolConfig<Logging.LogMessage>()
                    {
                         ClearClientSetOnCloseOrFailure = true,
                         SetID = new Modular.Interconnect.Sets.SetID(config.RemoteLogMessageSetName, generateUUIDForNull: false),
                         MaximumItemsPerMessage = config.RemoteLogMessageSetMaximumItemsPerMessage,
                    },
                    new SetRelayMessageStreamToolConfig<E039Object>()
                    {
                        ClearClientSetOnCloseOrFailure = true,
                        SetID = new Modular.Interconnect.Sets.SetID("E039ObjectSet", generateUUIDForNull: false),
                    },
                }.WhereIsNotDefault().ToArray(),
            };
            remotingClient = new RemotingClient(remotingClientConfig);
            partsList.Add(remotingClient);

            IVI.GetValueAccessor("{0}.ConfigNVS".CheckedFormat(remotingClient.PartID)).Set(remotingClientConfig.ConfigNVS);

            // ANManager set view tab

            DataContext = WVIA;

            E039ObjectSetTracker = SetTracker.GetSetTracker("E039ObjectSet");
            var test = e039ListView;
        }

        private DependencyPropertyKey E039ObjectSetTrackerPropertyKey = DependencyProperty.RegisterReadOnly("E039ObjectSetTracker", typeof(SetTracker), typeof(Window1), new PropertyMetadata(null));
        public SetTracker E039ObjectSetTracker { get { return _e039ObjectSetTracker; } private set { SetValue(E039ObjectSetTrackerPropertyKey, (_e039ObjectSetTracker = value)); } }
        private SetTracker _e039ObjectSetTracker;

        bool firstActivation = true;

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (firstActivation)
            {
                foreach (IActivePartBase part in partsList)
                {
                    if (part.BaseState == null || !part.BaseState.IsOnline)
                        part.CreateGoOnlineAction(true).Start();        // only issue a go online action if the part is not already online
                }

                StartTimers();

                firstActivation = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopTimers();

            base.OnClosed(e);

            List<IClientFacet> offlineActionList = new List<IClientFacet>();

            foreach (IActivePartBase part in partsList)
            {
                IClientFacet action = part.CreateGoOfflineAction();
                offlineActionList.Add(action);
                action.Start();
            }

            foreach (IClientFacet action in offlineActionList)
                action.WaitUntilComplete();

            foreach (IActivePartBase part in partsList)
                part.StopPart();
        }

        #region Timer Related

        private ISharedDispatcherTimer timer10Hz = SharedDispatcherTimerFactory.GetSharedTimer((0.1).FromSeconds());
        private IDisposable runTimerToken;

        void StartTimers()
        {
            runTimerToken = timer10Hz.GetRunTimerToken(Logger.Name);
            timer10Hz.TickNotificationList.OnNotify += TimerEvent10Hz_Callback;
        }

        void StopTimers()
        {
            Fcns.DisposeOfObject(ref runTimerToken);
        }

        List<IServiceable> Timer10HzServiceList = new List<IServiceable>();

        void TimerEvent10Hz_Callback() 
        {
            QpcTimeStamp now = QpcTimeStamp.Now;

            foreach (IServiceable s in Timer10HzServiceList)
                s.Service(now);

            WVIA.Service();
        }

        #endregion

        #region Button click handlers

        private void RCButton_Clicked(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;

            switch ((string)(b.Tag))
            {
                // NOTE: these cases are now handled directly in the RemotingClient via its use of the SupportServiceActions and SupportMappedServiceActions behavior flags.

                //case "GoOnline": remotingClient.CreateGoOnlineAction(false).Start(); break;
                //case "GoOnlineAndInitialize": remotingClient.CreateGoOnlineAction(true).Start(); break;
                //case "GoOffline": remotingClient.CreateGoOfflineAction().Start(); break;
                //case "Connect": remotingClient.CreateGoOnlineAction(true).Start(); break;
                //case "Disconnect": remotingClient.CreateGoOfflineAction().Start(); break;
                default: remotingClient.CreateServiceAction((string)(b.Tag)).Start(); break;
                case "Ping": remotingClient.CreateServiceAction("Remote $RemotingServicePing$").Start(); break;
                case "BigPing": remotingClient.CreateServiceAction("Remote $RemotingServicePing$", namedParamValues: new NamedValueSet() { { "b", new byte [100 * 1024] } }).Start(); break;
            }
        }

        #endregion
    }
}
