using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Core.Services;

namespace StatusLightChecker.Service.Services;

public class HealthHeartbeatService : BackgroundService
{
    private readonly ILogger<HealthHeartbeatService> _logger;
    private readonly IServiceHealthTracker _healthTracker;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    public HealthHeartbeatService(
        ILogger<HealthHeartbeatService> logger,
        IServiceHealthTracker healthTracker)
    {
        _logger = logger;
        _healthTracker = healthTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Health heartbeat service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            _healthTracker.RecordHeartbeat();
            _logger.LogDebug("Health heartbeat recorded");
            
            await Task.Delay(_heartbeatInterval, stoppingToken);
        }
    }
}