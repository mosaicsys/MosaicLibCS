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
using MosaicLib.Modular.Part;
using System.ComponentModel;

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

            lastPartBaseState = new BaseState();
            //lastActiveColor = lastInactiveColor = ellipseRGB_GS2.Color;
            lastHighlightColor = ellipseRGB_GS1.Color;
            lastBorderWidth = ellipse.StrokeThickness;
        }

        private static readonly DependencyProperty partBaseStateDP = DependencyProperty.Register("PartBaseState", typeof(IBaseState), typeof(PartBaseStateLED));
        private static readonly DependencyProperty borderWidthDP = DependencyProperty.Register("BorderWidth", typeof(double), typeof(PartBaseStateLED));
        private static readonly DependencyProperty highlightColorDP = DependencyProperty.Register("HighlightColor", typeof(Color), typeof(PartBaseStateLED));
        //private static readonly DependencyProperty idleColorDP = DependencyProperty.Register("IdleColor", typeof(Color), typeof(PartBaseStateLED));
        //private static readonly DependencyProperty inactiveColorDP = DependencyProperty.Register("InactiveColor", typeof(Color), typeof(PartBaseStateLED));

        public IBaseState PartBaseState { get { return (IBaseState)GetValue(partBaseStateDP); } set { SetValue(partBaseStateDP, value); } }
        public double BorderWidth { get { return (double)GetValue(borderWidthDP); } set { SetValue(borderWidthDP, value); } }
        public Color HighlightColor { get { return (Color)GetValue(highlightColorDP); } set { SetValue(highlightColorDP, value); } }
        //public Color ActiveColor { get { return (Color)GetValue(idleColorDP); } set { SetValue(idleColorDP, value); } }
        //public Color InactiveColor { get { return (Color)GetValue(inactiveColorDP); } set { SetValue(inactiveColorDP, value); } }

        private RadialGradientBrush ellipseRGB = null;
        private GradientStop ellipseRGB_GS1 = null;
        private GradientStop ellipseRGB_GS2 = null;

        private IBaseState lastPartBaseState;
        private double lastBorderWidth;
        private Color lastHighlightColor;
        //private Color lastActiveColor;
        //private Color lastInactiveColor;

        /// <summary>
        /// Callback from WPF to tell us that one of the dependency properties has been changed.
        /// For the dependency properties that are registered here, this method updates the Elipse from the current values of the known dp objects
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == partBaseStateDP)
                lastPartBaseState = (IBaseState)e.NewValue;
            else if (e.Property == borderWidthDP)
                lastBorderWidth = (double)e.NewValue;
            else if (e.Property == highlightColorDP)
                lastHighlightColor = (Color)e.NewValue;
            //else if (e.Property == idleColorDP)
            //    lastActiveColor = (Color)e.NewValue;
            //else if (e.Property == inactiveColorDP)
            //    lastInactiveColor = (Color)e.NewValue;

            Update();

            base.OnPropertyChanged(e);
        }

        private void Update()
        {
            Color color = uninitializedColor;

            if (lastPartBaseState.IsFaulted())
                color = errorColor;
            else if (lastPartBaseState.IsUninitialized())
                color = uninitializedColor;
            else if (lastPartBaseState.IsOffline())
                color = offlineColor;
            else if (lastPartBaseState.UseState == UseState.AttemptOnline || lastPartBaseState.IsConnecting)
                color = initializingColor;
            else if (lastPartBaseState.IsOnline || lastPartBaseState.IsConnected)
                color = lastPartBaseState.IsBusy ? busyColor : idleColor;
            else
                color = undefinedStateColor;

            UpdateAndSetColor(color);
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
        private Color busyColor = (Color)colorConverter.ConvertFrom("Green");
        private Color errorColor = (Color)colorConverter.ConvertFrom("Red");
        private Color offlineColor = (Color)colorConverter.ConvertFrom("DarkGray");
        private Color undefinedStateColor = (Color)colorConverter.ConvertFrom("Pink");
    }
}
