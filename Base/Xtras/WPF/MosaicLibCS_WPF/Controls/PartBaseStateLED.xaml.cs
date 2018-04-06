//-------------------------------------------------------------------
/*! @file PartBaseStateLED.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc.
 * Copyright (c) 2016 Mosaic Systems Inc.
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using MosaicLib.Modular.Part;
using MosaicLib.Utils;

namespace MosaicLib.WPF.Controls
{
    /// <summary>
    /// Interaction logic for ConnectionLED.xaml
    /// </summary>
    public partial class PartBaseStateLED : UserControl
    {
        public PartBaseStateLED()
        {
            InitializeComponent();

            ellipseRGB = ellipse.Fill as RadialGradientBrush;
            ellipseRGB_GS1 = ellipseRGB.GradientStops[0];
            ellipseRGB_GS2 = ellipseRGB.GradientStops[1];

            lastPartBaseState = null;
            lastHighlightColor = ellipseRGB_GS1.Color;
            lastBorderWidth = ellipse.StrokeThickness;
        }

        private static readonly DependencyProperty partBaseStateDP = DependencyProperty.Register("PartBaseState", typeof(IBaseState), typeof(PartBaseStateLED));
        private static readonly DependencyProperty actionInfoDP = DependencyProperty.Register("ActionInfo", typeof(IActionInfo), typeof(PartBaseStateLED));
        private static readonly DependencyProperty borderWidthDP = DependencyProperty.Register("BorderWidth", typeof(double ?), typeof(PartBaseStateLED));
        private static readonly DependencyProperty highlightColorDP = DependencyProperty.Register("HighlightColor", typeof(Color), typeof(PartBaseStateLED));
        private static readonly DependencyProperty includeConnectionStateDP = DependencyProperty.Register("IncludeConnectionState", typeof(bool?), typeof(PartBaseStateLED));

        public IBaseState PartBaseState { get { return (IBaseState)GetValue(partBaseStateDP); } set { SetValue(partBaseStateDP, value); } }
        public IActionInfo ActionInfo { get { return (IActionInfo)GetValue(actionInfoDP); } set { SetValue(actionInfoDP, value); } }
        public double? BorderWidth { get { return (double?)GetValue(borderWidthDP); } set { SetValue(borderWidthDP, value); } }
        public Color HighlightColor { get { return (Color)GetValue(highlightColorDP); } set { SetValue(highlightColorDP, value); } }
        public bool? IncludeConnectionState { get { return (bool?)GetValue(borderWidthDP); } set { SetValue(borderWidthDP, value); } }

        private RadialGradientBrush ellipseRGB = null;
        private GradientStop ellipseRGB_GS1 = null;
        private GradientStop ellipseRGB_GS2 = null;

        private IBaseState lastPartBaseState;
        private IActionInfo lastActionInfo;
        private double lastBorderWidth;
        private Color lastHighlightColor;
        private bool lastIncludeConnectionState;

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method updates the Elipse from the current values of the known dp objects
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == partBaseStateDP)
                lastPartBaseState = (IBaseState)e.NewValue;
            else if (e.Property == actionInfoDP)
                lastActionInfo = (IActionInfo)e.NewValue;
            else if (e.Property == borderWidthDP)
                lastBorderWidth = ((double?)e.NewValue).MapDefaultTo(1).GetValueOrDefault();
            else if (e.Property == highlightColorDP)
                lastHighlightColor = (Color)e.NewValue;
            else if (e.Property == includeConnectionStateDP)
                lastIncludeConnectionState = ((bool?)e.NewValue).GetValueOrDefault();

            Update();

            base.OnPropertyChanged(e);
        }

        private void Update()
        {
            string toolTipMesg = "{0}".CheckedFormat(lastPartBaseState);

            Color color;

            if (lastPartBaseState == null)
                color = uninitializedColor;
            else if (lastPartBaseState.IsFaulted())
                color = errorColor;
            else if (lastPartBaseState.IsUninitialized())
                color = uninitializedColor;
            else if (lastPartBaseState.IsOffline())
                color = offlineColor;
            else if (lastPartBaseState.UseState == UseState.AttemptOnline || lastPartBaseState.IsConnecting)
                color = initializingColor;
            else if (lastIncludeConnectionState && !lastPartBaseState.IsConnected)
                color = disconnectedColor;
            else if (lastPartBaseState.IsOnline || lastPartBaseState.IsConnected)
            {
                if (lastPartBaseState.IsBusy)
                {
                    color = busyColor;
                    toolTipMesg = "Busy, {0}".CheckedFormat(lastActionInfo);
                }
                else if (lastActionInfo != null && lastActionInfo.ActionState.Failed)
                {
                    color = actionFailedColor;
                    toolTipMesg = "{0}".CheckedFormat(lastActionInfo);
                }
                else
                {
                    color = idleColor;
                    toolTipMesg = "Idle, {0}".CheckedFormat(lastActionInfo);
                }
            }
            else
            {
                color = undefinedStateColor;
            }

            UpdateAndSetColor(color);

            if (ellipse != null)
            {
                ellipse.ToolTip = toolTipMesg;
            }
        }

        private void UpdateAndSetColor(Color currentColor)
        {
            if (ellipse != null && ellipse.StrokeThickness != lastBorderWidth)
                ellipse.StrokeThickness = lastBorderWidth;
            if (ellipseRGB_GS2 != null && ellipseRGB_GS1.Color != currentColor)
                ellipseRGB_GS2.Color = currentColor;
            if (ellipseRGB_GS1 != null && ellipseRGB_GS2.Color != lastHighlightColor)
                ellipseRGB_GS1.Color = lastHighlightColor;
        }

        private static TypeConverter colorConverter = TypeDescriptor.GetConverter (typeof (Color));

        private Color uninitializedColor = (Color)colorConverter.ConvertFrom("Goldenrod");
        private Color initializingColor = (Color)colorConverter.ConvertFrom("Gold");
        private Color idleColor = (Color) colorConverter.ConvertFrom("DarkGreen");
        private Color busyColor = (Color)colorConverter.ConvertFrom("Lime");
        private Color errorColor = (Color)colorConverter.ConvertFrom("Red");
        private Color offlineColor = (Color)colorConverter.ConvertFrom("DarkGray");
        private Color disconnectedColor = (Color)colorConverter.ConvertFrom("DarkRed");
        private Color undefinedStateColor = (Color)colorConverter.ConvertFrom("Pink");
        private Color actionFailedColor = (Color)colorConverter.ConvertFrom("Orange");
    }
}
