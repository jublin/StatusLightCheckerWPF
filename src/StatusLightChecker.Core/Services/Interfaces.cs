using StatusLightChecker.Core.Models;

namespace StatusLightChecker.Core.Services;

public interface IColorConfigurationService
{
    ColorConfiguration GetCurrentConfiguration();
    Task UpdateConfigurationAsync(ColorConfiguration configuration);
    event EventHandler<ColorConfigurationChangedEventArgs>? ConfigurationChanged;
}

public interface IStatusDetector
{
    string ApplicationName { get; }
    Task<ApplicationStatus?> DetectStatusAsync(CancellationToken cancellationToken = default);
}

public interface IServiceHealthTracker
{
    ServiceHealth GetCurrentHealth();
    void SetState(ServiceState state);
    void RecordHeartbeat();
}

public record ServiceHealth
{
    public ServiceState State { get; init; } = ServiceState.Stopped;
    public string Version { get; init; } = "1.0.0";
    public long UptimeSeconds { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset LastHeartbeat { get; init; } = DateTimeOffset.UtcNow;
}

public enum ServiceState
{
    Unknown = 0,
    Running = 1,
    Stopped = 2,
    Paused = 3,
    Starting = 4,
    Stopping = 5
}