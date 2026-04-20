using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Contracts;
using StatusLightChecker.Core.Models;
using System.Collections.ObjectModel;

namespace StatusLightChecker.Services;

public class GrpcClientService : IDisposable
{
    private readonly ILogger<GrpcClientService> _logger;
    private readonly string _clientId;
    private GrpcChannel? _channel;
    private global::StatusLightChecker.Contracts.StatusService.StatusServiceClient? _statusClient;
    private global::StatusLightChecker.Contracts.SettingsService.SettingsServiceClient? _settingsClient;
    private CancellationTokenSource? _statusStreamCts;
    private CancellationTokenSource? _configStreamCts;
    private readonly ObservableCollection<StatusUpdate> _statusUpdates;
    
    public event EventHandler<StatusUpdate>? StatusReceived;
    public event EventHandler<ColorConfigResponse>? ConfigurationReceived;
    public event EventHandler<ServiceState>? ServiceStateChanged;
    
    public bool IsConnected => _channel?.State == ConnectivityState.Ready;
    public ObservableCollection<StatusUpdate> StatusUpdates => _statusUpdates;

    public GrpcClientService(ILogger<GrpcClientService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _clientId = configuration["ClientConfiguration:ClientId"] ?? $"WpfClient_{Guid.NewGuid():N}";
        _statusUpdates = new ObservableCollection<StatusUpdate>();
        
        var endpoint = configuration["ClientConfiguration:GrpcEndpoint"] ?? "http://localhost:50051";
        InitializeChannel(endpoint);
    }

    private void InitializeChannel(string endpoint)
    {
        try
        {
            _logger.LogInformation("Initializing gRPC channel to {Endpoint}", endpoint);
            
            _channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
            {
                MaxReceiveMessageSize = 1024 * 1024,
                MaxSendMessageSize = 1024 * 1024
            });
            
            _statusClient = new global::StatusLightChecker.Contracts.StatusService.StatusServiceClient(_channel);
            _settingsClient = new global::StatusLightChecker.Contracts.SettingsService.SettingsServiceClient(_channel);
            
            _logger.LogInformation("gRPC channel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize gRPC channel");
            throw;
        }
    }

    public async Task StartStatusStreamAsync()
    {
        if (_statusClient == null) return;

        _statusStreamCts = new CancellationTokenSource();
        
        try
        {
            using var call = _statusClient.StreamStatusUpdates(new StatusRequest { ClientId = _clientId });
            
            await foreach (var update in call.ResponseStream.ReadAllAsync(_statusStreamCts.Token))
            {
                _statusUpdates.Insert(0, update);
                if (_statusUpdates.Count > 100) _statusUpdates.RemoveAt(_statusUpdates.Count - 1);
                
                StatusReceived?.Invoke(this, update);
                ServiceStateChanged?.Invoke(this, (ServiceState)update.StatusLevel);
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Status stream cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in status stream");
        }
    }

    public async Task StartConfigurationStreamAsync()
    {
        if (_settingsClient == null) return;

        _configStreamCts = new CancellationTokenSource();
        
        try
        {
            using var call = _settingsClient.StreamColorConfigUpdates(new ColorConfigRequest { ClientId = _clientId });
            
            await foreach (var config in call.ResponseStream.ReadAllAsync(_configStreamCts.Token))
            {
                ConfigurationReceived?.Invoke(this, config);
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Config stream cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in config stream");
        }
    }

    public async Task<HealthResponse?> GetServiceHealthAsync()
    {
        if (_statusClient == null) return null;
        
        try
        {
            return await _statusClient.GetServiceHealthAsync(new HealthRequest());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get service health");
            return null;
        }
    }

    public async Task<StatusLightChecker.Core.Models.ColorConfiguration?> GetColorConfigurationAsync()
    {
        if (_settingsClient == null) return null;
        
        try
        {
            var response = await _settingsClient.GetColorConfigAsync(new ColorConfigRequest { ClientId = _clientId });
            if (response.Success)
            {
                return MapFromProto(response.Config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get color configuration");
        }
        return null;
    }

    public async Task<bool> UpdateColorConfigurationAsync(StatusLightChecker.Core.Models.ColorConfiguration config)
    {
        if (_settingsClient == null) return false;
        
        try
        {
            var protoConfig = MapToProto(config);
            var response = await _settingsClient.UpdateColorConfigAsync(new UpdateColorConfigRequest 
            { 
                ClientId = _clientId, 
                Config = protoConfig 
            });
            return response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update color configuration");
            return false;
        }
    }

    public void StopStreams()
    {
        _statusStreamCts?.Cancel();
        _configStreamCts?.Cancel();
    }

    public void Dispose()
    {
        StopStreams();
        _statusStreamCts?.Dispose();
        _configStreamCts?.Dispose();
        _channel?.Dispose();
    }

    private static Contracts.ColorConfiguration MapToProto(StatusLightChecker.Core.Models.ColorConfiguration config)
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

    private static Contracts.StatusColor MapToProto(StatusLightChecker.Core.Models.StatusColor color)
    {
        return new Contracts.StatusColor
        {
            StatusLevel = (int)color.StatusLevel,
            Name = color.Name,
            HexColor = color.HexColor,
            IsBlinking = color.IsBlinking
        };
    }

    private static StatusLightChecker.Core.Models.ColorConfiguration MapFromProto(Contracts.ColorConfiguration proto)
    {
        return new StatusLightChecker.Core.Models.ColorConfiguration
        {
            Available = MapFromProto(proto.Available),
            Busy = MapFromProto(proto.Busy),
            DoNotDisturb = MapFromProto(proto.DoNotDisturb),
            Away = MapFromProto(proto.Away),
            Offline = MapFromProto(proto.Offline),
            Unknown = MapFromProto(proto.Unknown)
        };
    }

    private static StatusLightChecker.Core.Models.StatusColor MapFromProto(Contracts.StatusColor proto)
    {
        return new StatusLightChecker.Core.Models.StatusColor
        {
            StatusLevel = (StatusLightChecker.Core.Models.StatusLevel)proto.StatusLevel,
            Name = proto.Name,
            HexColor = proto.HexColor,
            IsBlinking = proto.IsBlinking
        };
    }
}
