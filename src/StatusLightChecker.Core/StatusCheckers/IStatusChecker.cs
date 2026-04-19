using StatusLightChecker.Core.Colors;

namespace StatusLightChecker.Core.StatusCheckers;

public delegate void StatusChangedEventHandler();

public interface IStatusChecker : IDisposable
{
    event StatusChangedEventHandler StatusChanged;

    string StatusButtonId { get; set; }

    Task GetCurrentStatus();
    void StartChecking();
    void StopChecking();

    StatusColors GetColorFromStatus(StatusColors? colors);
}
