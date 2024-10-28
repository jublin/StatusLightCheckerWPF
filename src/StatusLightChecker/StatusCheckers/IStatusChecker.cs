using System.IO.Ports;
using System.Windows.Automation;
using System.Windows.Media;
using Serilog;
using TeamsStatusChecker.Enumerations;

namespace TeamsStatusChecker.StatusCheckers;

internal interface IStatusChecker :IDisposable
{
    bool NamePropertyContains(AutomationElement element, string value);
    Task<AutomationElement?> FindWindowAsync();
    SolidColorBrush GetColorFromStatus();

    Task GetCurrentStatus();

    void StartChecking();
    void StopChecking();


}