using System.Diagnostics;

namespace StatusLightChecker.Core.Services;

public class ServiceHealthTracker : IServiceHealthTracker
{
    private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();
    private ServiceState _currentState = ServiceState.Stopped;
    private DateTimeOffset _lastHeartbeat = DateTimeOffset.UtcNow;
    private readonly string _version;

    public ServiceHealthTracker()
    {
        _version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    public ServiceHealth GetCurrentHealth()
    {
        return new ServiceHealth
        {
            State = _currentState,
            Version = _version,
            UptimeSeconds = (long)_uptimeStopwatch.Elapsed.TotalSeconds,
            Message = GetStateMessage(_currentState),
            LastHeartbeat = _lastHeartbeat
        };
    }

    public void SetState(ServiceState state)
    {
        if (_currentState != state)
        {
            _currentState = state;
            if (state == ServiceState.Running)
            {
                _uptimeStopwatch.Restart();
            }
        }
    }

    public void RecordHeartbeat()
    {
        _lastHeartbeat = DateTimeOffset.UtcNow;
    }

    private static string GetStateMessage(ServiceState state) => state switch
    {
        ServiceState.Running => "Service is running normally",
        ServiceState.Stopped => "Service is stopped",
        ServiceState.Paused => "Service is paused",
        ServiceState.Starting => "Service is starting...",
        ServiceState.Stopping => "Service is stopping...",
        _ => "Service state unknown"
    };
}