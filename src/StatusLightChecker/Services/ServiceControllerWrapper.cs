using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ServiceProcess;

namespace StatusLightChecker.Services;

public class ServiceControllerWrapper : IDisposable
{
    private readonly ILogger<ServiceControllerWrapper> _logger;
    private readonly string _serviceName;
    private ServiceController? _serviceController;

    public event EventHandler<ServiceControllerStatus>? StatusChanged;

    public ServiceControllerWrapper(ILogger<ServiceControllerWrapper> logger, IConfiguration configuration)
    {
        _logger = logger;
        _serviceName = configuration["ClientConfiguration:ServiceName"] ?? "StatusLightCheckerService";
        RefreshServiceController();
    }

    public ServiceControllerStatus? CurrentStatus => _serviceController?.Status;

    public bool IsInstalled
    {
        get
        {
            try
            {
                RefreshServiceController();
                return _serviceController != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanStart => _serviceController != null;
    public bool CanStop => _serviceController?.CanStop ?? false;
    public bool CanPause => false;

    public async Task StartAsync()
    {
        if (_serviceController == null) return;
        
        try
        {
            if (_serviceController.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("Starting service {ServiceName}", _serviceName);
                _serviceController.Start();
                await Task.Run(() => _serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));
                StatusChanged?.Invoke(this, _serviceController.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_serviceController == null) return;
        
        try
        {
            if (_serviceController.CanStop)
            {
                _logger.LogInformation("Stopping service {ServiceName}", _serviceName);
                _serviceController.Stop();
                await Task.Run(() => _serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
                StatusChanged?.Invoke(this, _serviceController.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service");
            throw;
        }
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await Task.Delay(1000);
        await StartAsync();
    }

    public void Refresh()
    {
        RefreshServiceController();
    }

    private void RefreshServiceController()
    {
        try
        {
            _serviceController?.Dispose();
            _serviceController = new ServiceController(_serviceName);
            _serviceController.Refresh();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Service {ServiceName} not found or not accessible", _serviceName);
            _serviceController = null;
        }
    }

    public void Dispose()
    {
        _serviceController?.Dispose();
    }
}