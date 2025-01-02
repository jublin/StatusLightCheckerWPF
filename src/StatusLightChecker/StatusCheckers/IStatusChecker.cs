using System.Windows.Media;
using FlaUI.Core.AutomationElements;

namespace StatusLightChecker.StatusCheckers;

internal interface IStatusChecker :IDisposable
{
    string StatusButtonId { get; set; }
    public AutomationElement? FindWindow();
    SolidColorBrush GetColorFromStatus();

    Task GetCurrentStatus();

    void StartChecking();
    void StopChecking();
    
    void InitializeTimer();


}