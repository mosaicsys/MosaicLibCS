//-------------------------------------------------------------------
/*! @file DelayedConstructionWrapperUserControl.xaml.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2019 Mosaic Systems Inc.
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
using System.ComponentModel;

using MosaicLib.Modular.Common;
using MosaicLib.Time;
using MosaicLib.Utils;

namespace MosaicLib.WPF.Controls
{
    /// <summary>
    /// This enum used by the <seealso cref="DelayedConstructionWrapperUserControl"/> to select which event triggers the wrapper to attempt to construct the underlying control
    /// and whether or not to post the construction attempt.
    /// </summary>
    public enum DelayedConstructionTrigger : int
    {
        /// <summary>Selects that the creation and assignment of the wrapper's content will be posted to the current synchronization context on the first transition from not visible to visible.</summary>
        PostOnFirstVisiblity = 0,

        /// <summary>Selects that the creation and assignment of the wrapper's content will be performed during the IsVisibleChanged event callback handler for the first transition from not visible to visible.</summary>
        OnFirstVisible,

        /// <summary>Selects that the creation and assignment of the wrapper's content will be posted to the current synchronization context when the Loaded event is signaled.</summary>
        PostOnLoaded,

        /// <summary>Selects that the creation and assignment of the wrapper's content will be performed during the Loaded event callback handler.</summary>
        OnLoaded,
    }

    public partial class DelayedConstructionWrapperUserControl : UserControl, IServiceable
    {
        public DelayedConstructionWrapperUserControl()
            : this(true, true)
        { }

        protected DelayedConstructionWrapperUserControl(bool hookIsVisibleChanged, bool hookLoaded)
        {
            if (hookIsVisibleChanged)
                IsVisibleChanged += Handle_IsVisibleChanged;

            if (hookLoaded)
                Loaded += Handle_Loaded;

            InitializeComponent();
        }

        public DelayedConstructionTrigger DelayedConstructionTrigger { get; set; }
        public Func<object> ContentFactoryDelegate { get; set; }
        public string Comment { get; set; }

        protected static readonly MosaicLib.Logging.ILogger Logger = new MosaicLib.Logging.Logger(Fcns.CurrentClassLeafName);

        private bool hasBeenVisible =false;

        protected void Handle_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            bool isVisible = (bool)e.NewValue;

            if (isVisible && !hasBeenVisible)
            {
                hasBeenVisible = true;
                switch (DelayedConstructionTrigger)
                {
                    case DelayedConstructionTrigger.OnFirstVisible: 
                        PerformAttemptToConstructContentOnce(); 
                        break;
                    case DelayedConstructionTrigger.PostOnFirstVisiblity:
                        System.Threading.SynchronizationContext.Current.Post(o => PerformAttemptToConstructContentOnce(), this);
                        break;
                    default:
                        break;
                }
            }
        }

        protected void Handle_Loaded(object sender, RoutedEventArgs e)
        {
            switch (DelayedConstructionTrigger)
            {
                case DelayedConstructionTrigger.OnLoaded:
                    PerformAttemptToConstructContentOnce();
                    break;
                case DelayedConstructionTrigger.PostOnLoaded:
                    System.Threading.SynchronizationContext.Current.Post(o => PerformAttemptToConstructContentOnce(), this);
                    break;
                default:
                    break;
            }
        }

        protected bool contentConstructionAttempted = false;
        protected object constructedContent = null;

        protected void PerformAttemptToConstructContentOnce()
        {
            string comment = null;

            try
            {
                if (ContentFactoryDelegate != null && !contentConstructionAttempted)
                {
                    contentConstructionAttempted = true;

                    comment = Comment.MapEmptyToNull() ?? Name.MapEmptyToNull() ?? "[NoCommentOrNameFound]";

                    using (var eeLog = new MosaicLib.Logging.EnterExitTrace(Logger.Debug, "{0}: Constructing Content".CheckedFormat(comment)))
                    {
                        constructedContent = ContentFactoryDelegate();
                    }

                    using (var eeLog = new MosaicLib.Logging.EnterExitTrace(Logger.Debug, "{0}: Assigning Constructed Content".CheckedFormat(comment)))
                    {
                        Content = constructedContent;
                    }
                }
            }
            catch (System.Exception ex)
            {
                string commentPart = (comment.IsNeitherNullNorEmpty() ? " [{0}]".CheckedFormat(comment) : "");
 
                Logger.Error.Emit("{0}{1} caught unexpected exception: {2}", Fcns.CurrentMethodName, commentPart, ex.ToString(ExceptionFormat.TypeAndMessageAndStackTrace));
            }
        }

        int IServiceable.Service(QpcTimeStamp qpcTimeStamp)
        {
            var serviceable = constructedContent as IServiceable;
            if (serviceable != null)
                return serviceable.Service(qpcTimeStamp);

            return 0;
        }
    }
}
