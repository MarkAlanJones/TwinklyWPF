using System.ComponentModel;
using System.Windows;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                //((MainViewModel)DataContext).Load();
               // ((MainViewModel)DataContext).GradientStops = SingleGradient.GradientStops.Clone();
            }
        }

    }
}
