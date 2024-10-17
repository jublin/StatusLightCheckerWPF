using System.Windows;
using System.Windows.Input;
using TeamsStatusChecker.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TeamsStatusChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            DataContext = MainViewModel.Instance;
        }

        private void LogBox_OnTextInput(object sender, TextCompositionEventArgs e)
        {
            LogBox.ScrollToEnd();
        }
    }
}