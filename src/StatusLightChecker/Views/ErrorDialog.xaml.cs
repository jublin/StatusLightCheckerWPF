using System.Windows;
using System.Windows.Controls;

namespace StatusLightChecker.Views;

public partial class ErrorDialog : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ErrorDialog), 
            new PropertyMetadata(string.Empty));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public ErrorDialog()
    {
        InitializeComponent();
    }
}