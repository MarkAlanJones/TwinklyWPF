using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using System.Windows;

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

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            Refresh(this, null);
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                ((MainViewModel)DataContext).Message = "Searching 🕵...";
                ((MainViewModel)DataContext).Load();
            }
        }
    }
}
