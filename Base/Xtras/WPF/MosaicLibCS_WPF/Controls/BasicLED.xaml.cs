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

namespace MosaicLibCS_WPF.Controls
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

            lastIsActive = false;
            lastActiveColor = lastInactiveColor = ellipseRGB_GS2.Color;
            lastHighlightColor = ellipseRGB_GS1.Color;
            lastBorderWidth = ellipse.StrokeThickness;
        }

        private static readonly DependencyProperty isActiveDP = DependencyProperty.Register("IsActive", typeof(Boolean), typeof(BasicLED));
        private static readonly DependencyProperty activeColorDP = DependencyProperty.Register("ActiveColor", typeof(Color), typeof(BasicLED));
        private static readonly DependencyProperty inactiveColorDP = DependencyProperty.Register("InactiveColor", typeof(Color), typeof(BasicLED));
        private static readonly DependencyProperty highlightColorDP = DependencyProperty.Register("HighlightColor", typeof(Color), typeof(BasicLED));
        private static readonly DependencyProperty borderWidthDP = DependencyProperty.Register("BorderWidth", typeof(double), typeof(BasicLED));

        public Boolean IsActive { get { return (Boolean)GetValue(isActiveDP); } set { SetValue(isActiveDP, value); } }
        public Color ActiveColor { get { return (Color)GetValue(activeColorDP); } set { SetValue(activeColorDP, value); } }
        public Color InactiveColor { get { return (Color)GetValue(inactiveColorDP); } set { SetValue(inactiveColorDP, value); } }
        public Color HighlightColor { get { return (Color)GetValue(highlightColorDP); } set { SetValue(highlightColorDP, value); } }
        public double BorderWidth { get { return (double)GetValue(borderWidthDP); } set { SetValue(borderWidthDP, value); } }

        private RadialGradientBrush ellipseRGB = null;
        private GradientStop ellipseRGB_GS1 = null;
        private GradientStop ellipseRGB_GS2 = null;

        private bool lastIsActive;
        private Color lastActiveColor;
        private Color lastInactiveColor;
        private Color lastHighlightColor;
        private double lastBorderWidth;

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method updates the Elipse from the current values of the known dp objects
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == isActiveDP)
                lastIsActive = (Boolean)e.NewValue;
            else if (e.Property == activeColorDP)
                lastActiveColor = (Color)e.NewValue;
            else if (e.Property == inactiveColorDP)
                lastInactiveColor = (Color)e.NewValue;
            else if (e.Property == highlightColorDP)
                lastHighlightColor = (Color)e.NewValue;
            else if (e.Property == borderWidthDP)
                lastBorderWidth = (double)e.NewValue;

            Update();

            base.OnPropertyChanged(e);
        }

        private void Update()
        {
            Color currentColor = (lastIsActive ? lastActiveColor : lastInactiveColor);

            if (ellipse != null && ellipse.StrokeThickness != lastBorderWidth)
                ellipse.StrokeThickness = lastBorderWidth;
            if (ellipseRGB_GS2 != null && ellipseRGB_GS1.Color != currentColor)
                ellipseRGB_GS2.Color = currentColor;
            if (ellipseRGB_GS1 != null && ellipseRGB_GS2.Color != lastHighlightColor)
                ellipseRGB_GS1.Color = lastHighlightColor;
        }
    }
}
