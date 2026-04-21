using Grpc.Core;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Contracts;
using StatusLightChecker.Core.Services;
using StatusLightChecker.Core.Models;
using System.Collections.Concurrent;

namespace StatusLightChecker.Service.Services;

public class GrpcSettingsService : global::StatusLightChecker.Contracts.SettingsService.SettingsServiceBase
{
    private readonly ILogger<GrpcSettingsService> _logger;
    private readonly IColorConfigurationService _colorConfigService;
    private readonly ISerialPortConfigurationService _serialPortConfigService;
    private readonly ConcurrentDictionary<string, IServerStreamWriter<ColorConfigResponse>> _activeStreams = new();

    public GrpcSettingsService(
        ILogger<GrpcSettingsService> logger,
        IColorConfigurationService colorConfigService,
        ISerialPortConfigurationService serialPortConfigService)
    {
        _logger = logger;
        _colorConfigService = colorConfigService;
        _serialPortConfigService = serialPortConfigService;

        _colorConfigService.ConfigurationChanged += OnConfigurationChanged;
    }

    public override Task<ColorConfigResponse> GetColorConfig(
        ColorConfigRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("GetColorConfig called by {ClientId}", request.ClientId);
        
        var config = _colorConfigService.GetCurrentConfiguration();
        return Task.FromResult(MapToResponse(config, true, null));
    }

    public override async Task<ColorConfigResponse> UpdateColorConfig(
        UpdateColorConfigRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("UpdateColorConfig called by {ClientId}", request.ClientId);
        
        try
        {
            var config = MapFromProto(request.Config);
            await _colorConfigService.UpdateConfigurationAsync(config);
            
            return MapToResponse(config, true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update color configuration");
            return MapToResponse(_colorConfigService.GetCurrentConfiguration(), false, ex.Message);
        }
    }

    public override async Task StreamColorConfigUpdates(
        ColorConfigRequest request,
        IServerStreamWriter<ColorConfigResponse> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("Client {ClientId} connected to config stream", request.ClientId);
        _activeStreams[request.ClientId] = responseStream;

        // Send current config immediately
        var currentConfig = _colorConfigService.GetCurrentConfiguration();
        await responseStream.WriteAsync(MapToResponse(currentConfig, true, null));

        // Keep stream open until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client {ClientId} disconnected from config stream", request.ClientId);
        }
        finally
        {
            _activeStreams.TryRemove(request.ClientId, out _);
        }
    }

    public override Task<SerialPortConfigResponse> GetSerialPortConfig(
        SerialPortConfigRequest request,
        ServerCallContext context)
    {
        _logger.LogDebug("GetSerialPortConfig called by {ClientId}", request.ClientId);

        var config = _serialPortConfigService.GetCurrentConfiguration();
        return Task.FromResult(new SerialPortConfigResponse
        {
            Config = new SerialPortConfig { ComPort = config.ComPort, BaudRate = config.BaudRate },
            Success = true
        });
    }

    public override async Task<SerialPortConfigResponse> UpdateSerialPortConfig(
        UpdateSerialPortConfigRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("UpdateSerialPortConfig called by {ClientId}", request.ClientId);

        try
        {
            var config = new Core.Services.SerialPortConfiguration
            {
                ComPort = request.Config.ComPort,
                BaudRate = request.Config.BaudRate
            };
            await _serialPortConfigService.UpdateConfigurationAsync(config);

            return new SerialPortConfigResponse
            {
                Config = new SerialPortConfig { ComPort = config.ComPort, BaudRate = config.BaudRate },
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update serial port configuration");
            var current = _serialPortConfigService.GetCurrentConfiguration();
            return new SerialPortConfigResponse
            {
                Config = new SerialPortConfig { ComPort = current.ComPort, BaudRate = current.BaudRate },
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private void OnConfigurationChanged(object? sender, ColorConfigurationChangedEventArgs e)
    {
        var response = MapToResponse(e.Configuration, true, null);
        
        foreach (var (clientId, stream) in _activeStreams.ToArray())
        {
            try
            {
                stream.WriteAsync(response).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send config update to client {ClientId}", clientId);
                _activeStreams.TryRemove(clientId, out _);
            }
        }
    }

    private static ColorConfigResponse MapToResponse(Core.Models.ColorConfiguration config, bool success, string? error)
    {
        return new ColorConfigResponse
        {
            Config = MapToProto(config),
            LastUpdatedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Success = success,
            ErrorMessage = error ?? string.Empty
        };
    }

    private static Contracts.ColorConfiguration MapToProto(Core.Models.ColorConfiguration config)
    {
        return new Contracts.ColorConfiguration
        {
            Available = MapToProto(config.Available),
            Busy = MapToProto(config.Busy),
            DoNotDisturb = MapToProto(config.DoNotDisturb),
            Away = MapToProto(config.Away),
            Offline = MapToProto(config.Offline),
            Unknown = MapToProto(config.Unknown)
        };
    }

    private static Contracts.StatusColor MapToProto(Core.Models.StatusColor color)
    {
        return new Contracts.StatusColor
        {
            StatusLevel = (int)color.StatusLevel,
            Name = color.Name,
            HexColor = color.HexColor,
            IsBlinking = color.IsBlinking
        };
    }

    private static Core.Models.ColorConfiguration MapFromProto(Contracts.ColorConfiguration proto)
    {
        return new Core.Models.ColorConfiguration
        {
            Available = MapFromProto(proto.Available),
            Busy = MapFromProto(proto.Busy),
            DoNotDisturb = MapFromProto(proto.DoNotDisturb),
            Away = MapFromProto(proto.Away),
            Offline = MapFromProto(proto.Offline),
            Unknown = MapFromProto(proto.Unknown)
        };
    }

    private static Core.Models.StatusColor MapFromProto(Contracts.StatusColor proto)
    {
        return new Core.Models.StatusColor
        {
            StatusLevel = (Core.Models.StatusLevel)proto.StatusLevel,
            Name = proto.Name,
            HexColor = proto.HexColor,
            IsBlinking = proto.IsBlinking
        };
    }
}