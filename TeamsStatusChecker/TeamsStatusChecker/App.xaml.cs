using System.Windows;
using TeamsStatusChecker.ViewModels;

namespace TeamsStatusChecker
{
    internal delegate void StatusChangedEventHandler();
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            
        }
        
        protected override async void OnExit(ExitEventArgs e)
        {
            await MainViewModel.Instance?.SerialPort?.WriteAsync([0,0,0], 0, 3)!;
            await MainViewModel.Instance?.SerialPort?.FlushAsync()!;
            await Task.Delay(1000);
            MainViewModel.Instance?.SerialPort?.Close();
            base.OnExit(e);
        }
    }

}
