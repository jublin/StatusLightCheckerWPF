using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Core.Models;
using StatusLightChecker.Services;
using System.Collections.ObjectModel;
using System.ServiceProcess;
using System.Windows.Media;

namespace StatusLightChecker.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly GrpcClientService _grpcClient;
    private readonly ServiceControllerWrapper _serviceController;
    private System.Timers.Timer? _refreshTimer;
    private readonly System.Threading.CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _serviceStatus = "Unknown";

    [ObservableProperty]
    private Brush _serviceStatusColor = Brushes.Gray;

    [ObservableProperty]
    private ApplicationStatus? _currentStatus;

    [ObservableProperty]
    private ObservableCollection<StatusUpdateViewModel> _statusHistory = new();

    [ObservableProperty]
    private ColorConfigurationViewModel _colorConfiguration = new();

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private bool _serviceInstalled;

    [ObservableProperty]
    private bool _canControlService;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        GrpcClientService grpcClient,
        ServiceControllerWrapper serviceController)
    {
        _logger = logger;
        _grpcClient = grpcClient;
        _serviceController = serviceController;

        _serviceController.StatusChanged += OnServiceStatusChanged;
        _grpcClient.StatusReceived += OnStatusReceived;
        _grpcClient.ConfigurationReceived += OnConfigurationReceived;

        // Setup refresh timer using System.Timers.Timer instead of DispatcherTimer
        _refreshTimer = new System.Timers.Timer(5000);
        _refreshTimer.Elapsed += async (s, e) => await RefreshServiceStatusAsync();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            ServiceInstalled = _serviceController.IsInstalled;
            CanControlService = ServiceInstalled;
            
            await RefreshServiceStatusAsync();
            _ = StartGrpcStreamsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initialization");
        }
    }

    private async Task StartGrpcStreamsAsync()
    {
        try
        {
            _ = Task.Run(async () => await _grpcClient.StartStatusStreamAsync());
            _ = Task.Run(async () => await _grpcClient.StartConfigurationStreamAsync());
            
            IsConnected = true;
            ConnectionStatus = "Connected";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start gRPC streams");
            IsConnected = false;
            ConnectionStatus = "Disconnected";
        }
    }

    private void OnStatusReceived(object? sender, StatusLightChecker.Contracts.StatusUpdate update)
    {
        var viewModel = new StatusUpdateViewModel
        {
            ApplicationName = update.ApplicationName,
            StatusLabel = update.StatusLabel,
            StatusLevel = (StatusLevel)update.StatusLevel,
            ColorHex = update.ColorHex,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(update.TimestampUnixMs).LocalDateTime,
            Details = update.Details
        };

        // Use App.Current.Dispatcher for UI thread marshaling
        _ = App.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusHistory.Insert(0, viewModel);
            if (StatusHistory.Count > 50) StatusHistory.RemoveAt(StatusHistory.Count - 1);
            
            CurrentStatus = new ApplicationStatus
            {
                ApplicationName = update.ApplicationName,
                StatusLabel = update.StatusLabel,
                StatusLevel = (StatusLevel)update.StatusLevel
            };
        });
    }

    private void OnConfigurationReceived(object? sender, StatusLightChecker.Contracts.ColorConfigResponse config)
    {
        _ = App.Current.Dispatcher.InvokeAsync(() =>
        {
            if (config.Config != null)
            {
                ColorConfiguration = new ColorConfigurationViewModel(config.Config);
            }
        });
    }

    private void OnServiceStatusChanged(object? sender, ServiceControllerStatus status)
    {
        _ = App.Current.Dispatcher.InvokeAsync(async () => await RefreshServiceStatusAsync());
    }

    private async Task RefreshServiceStatusAsync()
    {
        try
        {
            _serviceController.Refresh();
            
            var status = _serviceController.CurrentStatus;
            if (status.HasValue)
            {
                // Update on UI thread
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    ServiceStatus = status.Value.ToString();
                    ServiceStatusColor = status.Value switch
                    {
                        ServiceControllerStatus.Running => Brushes.Green,
                        ServiceControllerStatus.Stopped => Brushes.Red,
                        ServiceControllerStatus.Paused => Brushes.Orange,
                        ServiceControllerStatus.StartPending or 
                        ServiceControllerStatus.StopPending or 
                        ServiceControllerStatus.PausePending or 
                        ServiceControllerStatus.ContinuePending => Brushes.Yellow,
                        _ => Brushes.Gray
                    };
                });
            }
            else
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    ServiceStatus = "Not Installed";
                    ServiceStatusColor = Brushes.Gray;
                });
            }

            // Also check gRPC health
            var health = await _grpcClient.GetServiceHealthAsync();
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                if (health != null)
                {
                    IsConnected = true;
                    ConnectionStatus = $"Connected (v{health.Version})";
                }
                else
                {
                    IsConnected = false;
                    ConnectionStatus = "Disconnected";
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error refreshing service status");
        }
    }

    [RelayCommand]
    private async Task StartServiceAsync()
    {
        try
        {
            await _serviceController.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service");
            await ShowErrorDialogAsync($"Failed to start service: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StopServiceAsync()
    {
        try
        {
            await _serviceController.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service");
            await ShowErrorDialogAsync($"Failed to stop service: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RestartServiceAsync()
    {
        try
        {
            await _serviceController.RestartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart service");
            await ShowErrorDialogAsync($"Failed to restart service: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        await RefreshServiceStatusAsync();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var dialog = new Views.SettingsDialog
        {
            DataContext = new SettingsViewModel(_grpcClient, ColorConfiguration)
        };
        
        await DialogHost.Show(dialog, "RootDialog");
    }

    private async Task ShowErrorDialogAsync(string message)
    {
        var dialog = new Views.ErrorDialog { Message = message };
        await DialogHost.Show(dialog, "RootDialog");
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _grpcClient?.Dispose();
        _serviceController?.Dispose();
    }
}

public class StatusUpdateViewModel
{
    public string ApplicationName { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public StatusLevel StatusLevel { get; set; }
    public string ColorHex { get; set; } = "#808080";
    public DateTime Timestamp { get; set; }
    public string Details { get; set; } = string.Empty;
    public Brush StatusBrush => (SolidColorBrush)new BrushConverter().ConvertFromString(ColorHex)! ?? Brushes.Gray;
}
