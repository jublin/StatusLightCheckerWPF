using Grpc.Core;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Contracts;
using StatusLightChecker.Core.Services;
using StatusLightChecker.Core.Models;
using System.Collections.Concurrent;

namespace StatusLightChecker.Service.Services;

public class GrpcStatusService : global::StatusLightChecker.Contracts.StatusService.StatusServiceBase
{
    private readonly ILogger<GrpcStatusService> _logger;
    private readonly StatusMonitoringService _monitoringService;
    private readonly IColorConfigurationService _colorConfigService;
    private readonly ServiceHealthTracker _healthTracker;
    private readonly ConcurrentDictionary<string, IServerStreamWriter<StatusUpdate>> _activeStreams = new();

    public GrpcStatusService(
        ILogger<GrpcStatusService> logger,
        StatusMonitoringService monitoringService,
        IColorConfigurationService colorConfigService,
        ServiceHealthTracker healthTracker)
    {
        _logger = logger;
        _monitoringService = monitoringService;
        _colorConfigService = colorConfigService;
        _healthTracker = healthTracker;

        _monitoringService.StatusChanged += OnStatusChanged;
    }

    public override async Task StreamStatusUpdates(
        StatusRequest request,
        IServerStreamWriter<StatusUpdate> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("Client {ClientId} connected to status stream", request.ClientId);
        _activeStreams[request.ClientId] = responseStream;

        // Send current status immediately
        var currentStatus = await _monitoringService.GetCurrentStatusAsync();
        if (currentStatus != null)
        {
            await responseStream.WriteAsync(MapToStatusUpdate(currentStatus));
        }

        // Keep stream open until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client {ClientId} disconnected from status stream", request.ClientId);
        }
        finally
        {
            _activeStreams.TryRemove(request.ClientId, out _);
        }
    }

    public override async Task<StatusUpdate> GetCurrentStatus(
        StatusRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("GetCurrentStatus called by {ClientId}", request.ClientId);
        
        var status = await _monitoringService.GetCurrentStatusAsync();
        return status != null 
            ? MapToStatusUpdate(status) 
            : new StatusUpdate { StatusLevel = (int)Core.Models.StatusLevel.Unknown };
    }

    public override Task<HealthResponse> GetServiceHealth(
        HealthRequest request,
        ServerCallContext context)
    {
        var health = _healthTracker.GetCurrentHealth();
        return Task.FromResult(new HealthResponse
        {
            State = (Contracts.ServiceState)health.State,
            Version = health.Version,
            UptimeSeconds = health.UptimeSeconds,
            Message = health.Message
        });
    }

    private void OnStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        var update = MapToStatusUpdate(e.Status);
        
        foreach (var (clientId, stream) in _activeStreams)
        {
            try
            {
                stream.WriteAsync(update).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send status update to client {ClientId}", clientId);
                _activeStreams.TryRemove(clientId, out _);
            }
        }
    }

    private StatusUpdate MapToStatusUpdate(Core.Models.ApplicationStatus status)
    {
        var colorConfig = _colorConfigService.GetCurrentConfiguration();
        var statusColor = status.StatusLevel switch
        {
            Core.Models.StatusLevel.Available => colorConfig.Available,
            Core.Models.StatusLevel.Busy => colorConfig.Busy,
            Core.Models.StatusLevel.DoNotDisturb => colorConfig.DoNotDisturb,
            Core.Models.StatusLevel.Away => colorConfig.Away,
            Core.Models.StatusLevel.Offline => colorConfig.Offline,
            _ => colorConfig.Unknown
        };

        return new StatusUpdate
        {
            StatusId = Guid.NewGuid().ToString(),
            ApplicationName = status.ApplicationName,
            StatusValue = status.StatusValue,
            StatusLabel = status.StatusLabel,
            StatusLevel = (int)status.StatusLevel,
            ColorHex = statusColor.HexColor,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Details = status.Details
        };
    }
}