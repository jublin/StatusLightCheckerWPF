using System.Windows;
using System.Windows.Input;
using StatusLightChecker.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace StatusLightChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(
                ApplicationTheme.Dark, // Theme type
                WindowBackdropType.Acrylic,  // Background type
                true                                      // Whether to change accents automatically
            );
            DataContext = MainViewModel.Instance;
        }

        private void LogBox_OnTextInput(object sender, TextCompositionEventArgs e)
        {
            LogBox.ScrollToEnd();
            if (LogBox.Document.Blocks.Count > 200)
            {
                LogBox.Document.Blocks.Clear();
            }
        }
    }
}