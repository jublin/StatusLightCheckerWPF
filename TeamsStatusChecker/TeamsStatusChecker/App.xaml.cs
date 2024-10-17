using System.Windows;

namespace TeamsStatusChecker
{
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
            await MainViewModel.Instance?.SerialPort?.WriteAsync(TeamsStatusColors.Offline.ToArray(), 0, TeamsStatusColors.Offline.Count)!;
            await Task.Delay(1000);
            MainViewModel.Instance?.SerialPort?.Close();
            base.OnExit(e);
        }
    }

}
