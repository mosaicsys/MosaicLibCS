using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HostCycle
{
    /// <summary>
    /// Interaction logic for LoadPortControl.xaml
    /// </summary>
    public partial class LoadPortControl : UserControl
    {
        public LoadPortControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty PortIDProperty = DependencyProperty.Register("PortID", typeof(int), typeof(LoadPortControl), new PropertyMetadata(1));
        public static readonly DependencyProperty PPIDArrayProperty = DependencyProperty.Register("PPIDArray", typeof(IEnumerable<string>), typeof(LoadPortControl), new PropertyMetadata(new string[] { "has not been set yet" }));
        public int PortID { get => (int)GetValue(PortIDProperty); set => SetValue(PortIDProperty, value); }
        public IEnumerable<string> PPIDArray { get => (IEnumerable<string>)GetValue(PPIDArrayProperty); set => SetValue(PPIDArrayProperty, value); }
    }
}
