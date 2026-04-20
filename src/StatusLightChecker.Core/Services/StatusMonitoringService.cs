using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Core.Models;

namespace StatusLightChecker.Core.Services;

public class StatusMonitoringService : BackgroundService
{
    private readonly ILogger<StatusMonitoringService> _logger;
    private readonly IEnumerable<IStatusDetector> _detectors;
    private readonly IColorConfigurationService _colorConfigService;
    private ApplicationStatus? _currentStatus;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    public StatusMonitoringService(
        ILogger<StatusMonitoringService> logger,
        IEnumerable<IStatusDetector> detectors,
        IColorConfigurationService colorConfigService)
    {
        _logger = logger;
        _detectors = detectors;
        _colorConfigService = colorConfigService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Status Monitoring Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllDetectorsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status detectors");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Status Monitoring Service stopped");
    }

    private async Task CheckAllDetectorsAsync(CancellationToken cancellationToken)
    {
        ApplicationStatus? highestPriorityStatus = null;

        foreach (var detector in _detectors)
        {
            try
            {
                var status = await detector.DetectStatusAsync(cancellationToken);
                if (status != null)
                {
                    _logger.LogDebug("Detected status from {App}: {Status}", 
                        detector.ApplicationName, status.StatusLabel);
                    
                    // Higher status level = higher priority
                    if (highestPriorityStatus == null || status.StatusLevel > highestPriorityStatus.StatusLevel)
                    {
                        highestPriorityStatus = status;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting status from {App}", detector.ApplicationName);
            }
        }

        if (highestPriorityStatus != null && !IsSameStatus(_currentStatus, highestPriorityStatus))
        {
            _currentStatus = highestPriorityStatus;
            _logger.LogInformation("Status changed to: {Status} from {App}", 
                highestPriorityStatus.StatusLabel, highestPriorityStatus.ApplicationName);
            
            StatusChanged?.Invoke(this, new StatusChangedEventArgs(highestPriorityStatus));
        }
    }

    public Task<ApplicationStatus?> GetCurrentStatusAsync()
    {
        return Task.FromResult(_currentStatus);
    }

    private static bool IsSameStatus(ApplicationStatus? a, ApplicationStatus? b)
    {
        if (a == null || b == null) return false;
        return a.StatusLevel == b.StatusLevel && 
               a.ApplicationName == b.ApplicationName &&
               a.StatusValue == b.StatusValue;
    }
}