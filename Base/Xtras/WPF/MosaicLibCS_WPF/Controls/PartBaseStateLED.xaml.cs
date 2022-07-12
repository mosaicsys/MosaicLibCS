//-------------------------------------------------------------------
/*! @file PartBaseStateLED.xaml.cs
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

using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        public static readonly DependencyProperty PartBaseStateProperty = DependencyProperty.Register("PartBaseState", typeof(IBaseState), typeof(PartBaseStateLED));
        public static readonly DependencyProperty ActionInfoProperty = DependencyProperty.Register("ActionInfo", typeof(IActionInfo), typeof(PartBaseStateLED));
        public static readonly DependencyProperty HighlightColorProperty = DependencyProperty.Register("HighlightColor", typeof(Color), typeof(PartBaseStateLED));
        public static readonly DependencyProperty IncludeConnectionStateProperty = DependencyProperty.Register("IncludeConnectionState", typeof(bool), typeof(PartBaseStateLED));
        public static readonly DependencyProperty IncludePartIDProperty = DependencyProperty.Register("IncludePartID", typeof(bool), typeof(PartBaseStateLED));
        public static readonly DependencyProperty BorderWidthProperty = DependencyProperty.Register("BorderWidth", typeof(double), typeof(PartBaseStateLED), new PropertyMetadata(0.0, HandleBorderWidthPropertyChanged));
        public static new readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register("BorderThickness", typeof(Thickness), typeof(PartBaseStateLED), new PropertyMetadata(new Thickness(0), HandleBorderThicknessPropertyChanged));
        public static readonly DependencyProperty ColorListProperty = DependencyProperty.Register("ColorList", typeof(string), typeof(PartBaseStateLED), new PropertyMetadata(defaultColorListString, HandleColorListPropertyChanged));
        public static new readonly DependencyProperty ToolTipProperty = DependencyProperty.Register("ToolTip", typeof(object), typeof(PartBaseStateLED), new PropertyMetadata(null, HandleToolTipPropertyChanged));

        public IBaseState PartBaseState { get { return (IBaseState)GetValue(PartBaseStateProperty); } set { SetValue(PartBaseStateProperty, value); } }
        public IActionInfo ActionInfo { get { return (IActionInfo)GetValue(ActionInfoProperty); } set { SetValue(ActionInfoProperty, value); } }
        public Color HighlightColor { get { return (Color)GetValue(HighlightColorProperty); } set { SetValue(HighlightColorProperty, value); } }
        public bool IncludeConnectionState { get { return (bool)GetValue(IncludeConnectionStateProperty); } set { SetValue(IncludeConnectionStateProperty, value); } }
        public bool IncludePartID { get { return (bool)GetValue(IncludePartIDProperty); } set { SetValue(IncludePartIDProperty, value); } }
        public double BorderWidth { get { return (double)GetValue(BorderWidthProperty); } set { SetValue(BorderWidthProperty, value); } }
        public new Thickness BorderThickness { get { return (Thickness)GetValue(BorderThicknessProperty); } set { SetValue(BorderThicknessProperty, value); } }
        public string ColorList { get { return (string)GetValue(ColorListProperty); } set { SetValue(ColorListProperty, value); } }
        public new object ToolTip { get { return GetValue(ToolTipProperty); } set { SetValue(ToolTipProperty, value); } }

        private RadialGradientBrush ellipseRGB = null;
        private GradientStop ellipseRGB_GS1 = null;
        private GradientStop ellipseRGB_GS2 = null;

        private IBaseState lastPartBaseState;
        private IActionInfo lastActionInfo;
        private double lastBorderWidth;
        private Color lastHighlightColor;
        private bool lastIncludeConnectionState = true, lastIncludePartID = false;
        private System.Windows.Media.Brush lastBorderBrush = solidBlackBrush;
        private Colors colors = defaultColors;

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method updates the Elipse from the current values of the known dp objects
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == PartBaseStateProperty)
                lastPartBaseState = (IBaseState)e.NewValue;
            else if (e.Property == ActionInfoProperty)
                lastActionInfo = (IActionInfo)e.NewValue;
            else if (e.Property == HighlightColorProperty)
                lastHighlightColor = (Color)e.NewValue;
            else if (e.Property == IncludeConnectionStateProperty)
                lastIncludeConnectionState = (bool)e.NewValue;
            else if (e.Property == IncludePartIDProperty)
                lastIncludePartID = (bool)e.NewValue;
            else if (e.Property == BorderBrushProperty)
                lastBorderBrush = (Brush)e.NewValue;

            Update();

            base.OnPropertyChanged(e);
        }

        private static void HandleBorderWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PartBaseStateLED me = d as PartBaseStateLED;
            if (me != null)
            {
                double selectedWidth = (double)e.NewValue;

                if (me.lastBorderWidth != selectedWidth)
                {
                    me.lastBorderWidth = selectedWidth;
                    me.Update();
                }
            }
        }

        private static void HandleBorderThicknessPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PartBaseStateLED me = d as PartBaseStateLED;
            if (me != null)
            {
                Thickness selectedThickness = (Thickness)e.NewValue;
                double selectedWidth = (selectedThickness.Bottom + selectedThickness.Top + selectedThickness.Left + selectedThickness.Right) * 0.25;

                if (me.lastBorderWidth != selectedWidth)
                {
                    me.lastBorderWidth = selectedWidth;
                    me.Update();
                }
            }
        }

        private static void HandleColorListPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PartBaseStateLED me = d as PartBaseStateLED;
            if (me != null)
            {
                string selectedColorsListString = (string)e.NewValue;

                if (me.colors.colorListString != selectedColorsListString)
                {
                    me.colors = new Colors(selectedColorsListString);
                    me.Update();
                }
            }
        }

        private static void HandleToolTipPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var me = d as PartBaseStateLED;

            if (me != null)
            {
                if (me.locallySettingToolTip)
                {
                    // locally assigned tool tip values are not handled here.
                }
                else
                {
                    if (!me.externallyProvidedToolTipIsActive)
                        me.externallyProvidedToolTipIsActive = true;    // suppress locally assigned values from this point on

                    me.ellipse.ToolTip = e.NewValue;
                }
            }
        }

        private bool externallyProvidedToolTipIsActive = false;
        private bool locallySettingToolTip = false;

        private void Update()
        {
            var ttPartIDPrefix = (IncludePartID ? "{0} ".CheckedFormat(lastPartBaseState != null ? lastPartBaseState.PartID : string.Empty) : "");
            string toolTipMesg = (lastPartBaseState != null) ? "{0}{1}".CheckedFormat(ttPartIDPrefix, lastPartBaseState.ToString(BaseState.ToStringSelect.All & ~BaseState.ToStringSelect.PartID)) : "[Part BaseState is null]";

            Color color;

            bool connectionStateIsApplicable = lastIncludeConnectionState && (lastPartBaseState != null) ? (lastPartBaseState.ConnState != ConnState.NotApplicable && lastPartBaseState.ConnState != ConnState.Undefined) : false;

            if (lastPartBaseState == null)
                color = colors.nullStateColor;
            else if (lastPartBaseState.IsFaulted())
                color = colors.errorColor;
            else if (lastPartBaseState.IsUninitialized())
                color = colors.uninitializedColor;
            else if (lastPartBaseState.IsOffline())
                color = colors.offlineColor;
            else if (lastPartBaseState.UseState == UseState.AttemptOnline || (connectionStateIsApplicable && lastPartBaseState.IsConnecting))
                color = colors.initializingColor;
            else if (connectionStateIsApplicable && !lastPartBaseState.ConnState.IsConnected(acceptConnectionDegraded: true))
                color = colors.disconnectedColor;
            else if (lastPartBaseState.IsOnline || (connectionStateIsApplicable && lastPartBaseState.ConnState.IsConnected(acceptConnectionDegraded: true)))
            {
                if (lastPartBaseState.IsBusy)
                {
                    color = colors.busyColor;

                    toolTipMesg = (lastActionInfo != null) ? "{0}Busy, {1} [{2}]".CheckedFormat(ttPartIDPrefix, lastActionInfo, lastPartBaseState.Reason) : "{0}Busy [{1}]".CheckedFormat(ttPartIDPrefix, lastPartBaseState.Reason);
                }
                else if ((lastActionInfo != null) && lastActionInfo.ActionState.Failed)
                {
                    color = colors.actionFailedColor;
                    toolTipMesg = "{0}".CheckedFormat(lastActionInfo);
                }
                else
                {
                    color = colors.idleColor;
                    toolTipMesg = (lastActionInfo != null) ? "{0}Idle, {1}".CheckedFormat(ttPartIDPrefix, lastActionInfo) : "{0}Idle".CheckedFormat(ttPartIDPrefix);
                }

                if (connectionStateIsApplicable && lastPartBaseState.ConnState == ConnState.ConnectionDegraded)
                {
                    color = colors.connectionDegradedColor;
                }
            }
            else
            {
                color = colors.undefinedStateColor;
            }

            UpdateAndSetColor(color);

            if (!externallyProvidedToolTipIsActive && ellipse != null && toolTipMesg != lastToolTipMesg)
            {
                locallySettingToolTip = true;
                ToolTip = ellipse.ToolTip = (lastToolTipMesg = toolTipMesg);
                locallySettingToolTip = false;
            }
        }

        private string lastToolTipMesg = null;

        private void UpdateAndSetColor(Color currentColor)
        {
            if (ellipse != null)
            {
                if (ellipse.StrokeThickness != lastBorderWidth)
                    ellipse.StrokeThickness = lastBorderWidth;

                if (ellipse.Stroke != lastBorderBrush)
                    ellipse.Stroke = lastBorderBrush;
            }

            if (ellipseRGB_GS2 != null && ellipseRGB_GS1.Color != currentColor)
                ellipseRGB_GS2.Color = currentColor;

            if (ellipseRGB_GS1 != null && ellipseRGB_GS2.Color != lastHighlightColor)
                ellipseRGB_GS1.Color = lastHighlightColor;
        }

        private static TypeConverter colorConverter = TypeDescriptor.GetConverter(typeof (Color));

        private static readonly Color blackColor = (Color)colorConverter.ConvertFrom("Black");
        private static readonly Brush solidBlackBrush = new SolidColorBrush(blackColor);

        private const string defaultColorListString = "LightGray,Goldenrod,Gold,DarkGreen,Lime,Red,DarkGray,DarkRed,Pink,DarkOrange,Orange";
        private static readonly string[] defaultColorListStringArray = defaultColorListString.Split(',');
        private static readonly Color[] defaultColorsArray = defaultColorListStringArray.Select(colorName => (Color)colorConverter.ConvertFrom(colorName)).ToArray();
        private static readonly Colors defaultColors = new Colors(defaultColorListString);

        private static readonly Color defaultFallbackColor = (Color)colorConverter.ConvertFrom("LightGray");

        private class Colors
        {
            public Colors(string colorListString = defaultColorListString)
            {
                this.colorListString = colorListString;

                string[] colorListStringArray = colorListString.Split(',', ' ')
                    .Select(colorName => colorName.Trim())
                    .Select((colorName, index) => colorName.MapNullOrEmptyTo(defaultColorListStringArray.SafeAccess(index, "")))
                    .ToArray();

                colorsArray = colorListStringArray.Select(colorName =>
                {
                    try
                    {
                        return (Color)colorConverter.ConvertFrom(colorName);
                    }
                    catch { return defaultFallbackColor; }
                }).ToArray();

                int stateIndex = 0;

                nullStateColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                uninitializedColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                initializingColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                idleColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                busyColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                errorColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                offlineColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                disconnectedColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                undefinedStateColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                actionFailedColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
                connectionDegradedColor = colorsArray.SafeAccess(stateIndex++, defaultFallbackColor);
            }

            public string colorListString;
            public Color[] colorsArray;

            public Color nullStateColor;
            public Color uninitializedColor;
            public Color initializingColor;
            public Color idleColor;
            public Color busyColor;
            public Color errorColor;
            public Color offlineColor;
            public Color disconnectedColor;
            public Color undefinedStateColor;
            public Color actionFailedColor;
            public Color connectionDegradedColor;
        }
    }
}
