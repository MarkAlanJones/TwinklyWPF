using System.Windows;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            MainWindow = new MultiWindow() { DataContext = new MainViewModel(e.Args) };
            MainWindow.Show();
        }
    }
}
