using Microsoft.Extensions.DependencyInjection;
using StatusLightChecker.ViewModels;
using System.Windows;
using System.ComponentModel;

namespace StatusLightChecker;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<MainViewModel>();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Dispose();
        }
        base.OnClosing(e);
    }
}