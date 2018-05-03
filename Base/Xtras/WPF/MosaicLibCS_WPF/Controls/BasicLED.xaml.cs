//-------------------------------------------------------------------
/*! @file BasicLED.cs
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

        public static readonly DependencyProperty ColorSelectIndexProperty = DependencyProperty.Register("ColorSelectIndex", typeof(object), typeof(BasicLED));
        public static readonly DependencyProperty ColorListProperty = DependencyProperty.Register("ColorList", typeof(string), typeof(BasicLED));
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register("IsActive", typeof(bool), typeof(BasicLED));
        public static readonly DependencyProperty ActiveColorProperty = DependencyProperty.Register("ActiveColor", typeof(Color), typeof(BasicLED));
        public static readonly DependencyProperty InactiveColorProperty = DependencyProperty.Register("InactiveColor", typeof(Color), typeof(BasicLED));
        public static readonly DependencyProperty HighlightColorProperty = DependencyProperty.Register("HighlightColor", typeof(Color), typeof(BasicLED));
        public static readonly DependencyProperty BorderWidthProperty = DependencyProperty.Register("BorderWidth", typeof(double), typeof(BasicLED), new PropertyMetadata(0.0, HandleBorderWidthPropertyChanged));
        public static new readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register("BorderThickness", typeof(Thickness), typeof(BasicLED), new PropertyMetadata(new Thickness(0), HandleBorderThicknessPropertyChanged));

        public object ColorSelectIndex { get { return GetValue(ColorSelectIndexProperty); } set { SetValue(ColorSelectIndexProperty, value); } }
        public string ColorList { get { return (string)GetValue(ColorListProperty); } set { SetValue(ColorListProperty, value); } }
        public bool IsActive { get { return (bool)GetValue(IsActiveProperty); } set { SetValue(IsActiveProperty, value); } }
        public Color ActiveColor { get { return (Color)GetValue(ActiveColorProperty); } set { SetValue(ActiveColorProperty, value); } }
        public Color InactiveColor { get { return (Color)GetValue(InactiveColorProperty); } set { SetValue(InactiveColorProperty, value); } }
        public Color HighlightColor { get { return (Color)GetValue(HighlightColorProperty); } set { SetValue(HighlightColorProperty, value); } }
        public double BorderWidth { get { return (double)GetValue(BorderWidthProperty); } set { SetValue(BorderWidthProperty, value); } }
        public new Thickness BorderThickness { get { return (Thickness)GetValue(BorderThicknessProperty); } set { SetValue(BorderThicknessProperty, value); } }

        private RadialGradientBrush ellipseRGB = null;
        private GradientStop ellipseRGB_GS1 = null;
        private GradientStop ellipseRGB_GS2 = null;

        private int lastColorSelectIndex;
        private Color lastHighlightColor, lastActiveColor, lastInactiveColor;
        private double lastBorderWidth;
        private System.Windows.Media.Brush lastBorderBrush = solidBlackBrush;
        private string colorListString;
        private Color[] colorListArray;

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method updates the Elipse from the current values of the known dp objects
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == ColorSelectIndexProperty)
                lastColorSelectIndex = ValueContainer.Create(e.NewValue, ContainerStorageType.I4).GetValue<int>(rethrow: false);
            else if (e.Property == ColorListProperty)
                colorListArray = ParseColorList(colorListString = (string)e.NewValue);
            else if (e.Property == IsActiveProperty)
                lastColorSelectIndex = ((bool)e.NewValue).MapToInt();
            else if (e.Property == ActiveColorProperty)
                colorListArray = new Color[] { lastInactiveColor, lastActiveColor = (Color)e.NewValue };
            else if (e.Property == InactiveColorProperty)
                colorListArray = new Color[] { lastInactiveColor = (Color)e.NewValue, lastActiveColor };
            else if (e.Property == HighlightColorProperty)
                lastHighlightColor = (Color)e.NewValue;
            else if (e.Property == BorderBrushProperty)
                lastBorderBrush = (Brush)e.NewValue;

            Update();

            base.OnPropertyChanged(e);
        }

        private static void HandleBorderWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            BasicLED me = d as BasicLED;
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
            BasicLED me = d as BasicLED;
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

        private void Update()
        {
            Color currentColor = colorListArray.SafeAccess(lastColorSelectIndex, lastInactiveColor);

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

        private static readonly Color blackColor = (Color)colorConverter.ConvertFrom("Black");
        private static readonly Brush solidBlackBrush = new SolidColorBrush(blackColor);
    }
}
