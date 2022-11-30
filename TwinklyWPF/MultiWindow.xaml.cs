using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for MultiWindow.xaml
    /// </summary>
    public partial class MultiWindow : Window
    {
        public MultiWindow()
        {
            InitializeComponent();
        }

        private LinearGradientBrush Grad;

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            Grad = Resources["SingleGradient"] as LinearGradientBrush;
            Refresh(this, null);
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                ((MainViewModel)DataContext).Message = "Searching 🕵...";
                ((MainViewModel)DataContext).Load(Grad);
            }
        }
    }
}
