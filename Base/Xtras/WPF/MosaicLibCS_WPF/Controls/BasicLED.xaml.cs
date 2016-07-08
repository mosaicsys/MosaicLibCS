//-------------------------------------------------------------------
/*! @file BasicLED.cs
 *  @brief
 * 
 * Copyright (c) Mosaic Systems Inc., All rights reserved.
 * Copyright (c) 2016 Mosaic Systems Inc., All rights reserved.
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
using MosaicLib.Utils;
using MosaicLib.Modular.Common;

namespace MosaicLib.WPF.Controls
{
    /// <summary>
    /// Interaction logic for BasicLED.xaml
    /// </summary>
    public partial class BasicLED : UserControl
    {
        public BasicLED()
        {
            InitializeComponent();

            ellipseRGB = ellipse.Fill as RadialGradientBrush;
            ellipseRGB_GS1 = ellipseRGB.GradientStops[0];
            ellipseRGB_GS2 = ellipseRGB.GradientStops[1];

            lastColorSelectIndex = 0;
            colorListArray = new Color[] { lastInactiveColor = ellipseRGB_GS2.Color, lastActiveColor = ellipseRGB_GS2.Color };
            lastHighlightColor = ellipseRGB_GS1.Color;
            lastBorderWidth = ellipse.StrokeThickness;
        }

        private static readonly DependencyProperty colorSelectIndexDP = DependencyProperty.Register("ColorSelectIndex", typeof(object), typeof(BasicLED));
        private static readonly DependencyProperty colorListDP = DependencyProperty.Register("ColorList", typeof(string), typeof(BasicLED));
        private static readonly DependencyProperty isActiveDP = DependencyProperty.Register("IsActive", typeof(Boolean?), typeof(BasicLED));
        private static readonly DependencyProperty activeColorDP = DependencyProperty.Register("ActiveColor", typeof(Color), typeof(BasicLED));
        private static readonly DependencyProperty inactiveColorDP = DependencyProperty.Register("InactiveColor", typeof(Color), typeof(BasicLED));
        private static readonly DependencyProperty highlightColorDP = DependencyProperty.Register("HighlightColor", typeof(Color), typeof(BasicLED));
        private static readonly DependencyProperty borderWidthDP = DependencyProperty.Register("BorderWidth", typeof(double), typeof(BasicLED));

        public object ColorSelectIndex { get { return GetValue(colorSelectIndexDP); } set { SetValue(colorSelectIndexDP, value); } }
        public string ColorList { get { return (string)GetValue(colorListDP); } set { SetValue(colorListDP, value); } }
        public Boolean? IsActive { get { return (Boolean?)GetValue(isActiveDP); } set { SetValue(isActiveDP, value); } }
        public Color ActiveColor { get { return (Color)GetValue(activeColorDP); } set { SetValue(activeColorDP, value); } }
        public Color InactiveColor { get { return (Color)GetValue(inactiveColorDP); } set { SetValue(inactiveColorDP, value); } }
        public Color HighlightColor { get { return (Color)GetValue(highlightColorDP); } set { SetValue(highlightColorDP, value); } }
        public double BorderWidth { get { return (double)GetValue(borderWidthDP); } set { SetValue(borderWidthDP, value); } }

        private RadialGradientBrush ellipseRGB = null;
        private GradientStop ellipseRGB_GS1 = null;
        private GradientStop ellipseRGB_GS2 = null;

        private int lastColorSelectIndex;
        private Color lastHighlightColor, lastActiveColor, lastInactiveColor;
        private double lastBorderWidth;
        private Color[] colorListArray;

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method updates the Elipse from the current values of the known dp objects
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == colorSelectIndexDP)
                lastColorSelectIndex = LocalConvertToInt(e.NewValue);
            else if (e.Property == colorListDP)
                colorListArray = ParseColorList((string)e.NewValue);
            else if (e.Property == isActiveDP)
                lastColorSelectIndex = ((Boolean?)e.NewValue).GetValueOrDefault().MapToInt();
            else if (e.Property == activeColorDP)
                colorListArray = new Color[] { lastInactiveColor, lastActiveColor = (Color)e.NewValue };
            else if (e.Property == inactiveColorDP)
                colorListArray = new Color[] { lastInactiveColor = (Color)e.NewValue, lastActiveColor };
            else if (e.Property == highlightColorDP)
                lastHighlightColor = (Color)e.NewValue;
            else if (e.Property == borderWidthDP)
                lastBorderWidth = (double)e.NewValue;

            Update();

            base.OnPropertyChanged(e);
        }

        private int LocalConvertToInt(object newValue)
        {
            try
            {
                if (newValue == null)
                    return default(int);

                int value = (int) System.Convert.ChangeType(newValue, typeof(int));
                return value;
            }
            catch
            {
                return 0;
            }
        }

        private void Update()
        {
            Color currentColor = colorListArray.SafeAccess(lastColorSelectIndex, lastInactiveColor);

            if (ellipse != null && ellipse.StrokeThickness != lastBorderWidth)
                ellipse.StrokeThickness = lastBorderWidth;
            if (ellipseRGB_GS2 != null && ellipseRGB_GS1.Color != currentColor)
                ellipseRGB_GS2.Color = currentColor;
            if (ellipseRGB_GS1 != null && ellipseRGB_GS2.Color != lastHighlightColor)
                ellipseRGB_GS1.Color = lastHighlightColor;
        }

        private Color[] ParseColorList(string colorList)
        {
            string[] colorNamesArray = colorList.Split(',', ' ');

            Color[] colorArray = colorNamesArray.Select(colorName => colorName.Trim()).Select(colorName =>
                {
                    try {
                        if (colorName.IsNullOrEmpty())
                            return lastInactiveColor;
                        else
                            return (Color)colorConverter.ConvertFrom(colorName); 
                    }
                    catch { return lastInactiveColor; }
                }).ToArray();

            return colorArray;
        }

        private static TypeConverter colorConverter = TypeDescriptor.GetConverter(typeof(Color));

        private Color uninitializedColor = (Color)colorConverter.ConvertFrom("Goldenrod");

    }
}
