using System.Windows.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Serilog;
using StatusLightChecker.Core.Colors;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StatusLightChecker.Core.StatusCheckers;

public enum TeamsStatus
{
    Available,
    Busy,
    DoNotDisturb,
    Away,
    Offline,
    Unknown,
    OutOfOffice,
    InAMeeting
}

public class TeamsApplicationStatusChecker : IStatusChecker
{
    public event StatusChangedEventHandler? StatusChanged;
    public string StatusButtonId { get; set; } = "idna-me-control-avatar-trigger";

    private readonly ILogger _logger;
    private AutomationElement? _storedWindow;
    private TeamsStatus _lastStatus = TeamsStatus.Unknown;
    private DispatcherTimer? _statusTimer;
    private readonly int _poolingIntervalSeconds = 3;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _statusCheckSemaphore = new(1, 1);
    private readonly SemaphoreSlim _windowFinderSemaphore = new(1, 1);

    private const string WindowTitle = "Microsoft Teams";

    public TeamsApplicationStatusChecker()
    {
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }

    public void StartChecking()
    {
        _cts = new CancellationTokenSource();
        InitializeTimer();
        _statusTimer?.Start();
        _logger.Information("Started checking Teams status");
    }

    public void StopChecking()
    {
        _statusTimer?.Stop();
        _cts?.Cancel();
        _logger.Information("Stopped checking Teams status");
    }

    private void InitializeTimer()
    {
        _statusTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(_poolingIntervalSeconds),
            DispatcherPriority.Normal,
            StatusTimerCallback,
            Dispatcher.CurrentDispatcher);
    }

    private async void StatusTimerCallback(object? sender, EventArgs e)
    {
        await GetCurrentStatus();
        _statusTimer?.Start();
    }

    public async Task GetCurrentStatus()
    {
        var canEnter = await _statusCheckSemaphore.WaitAsync(1000);
        if (!canEnter)
            return;

        try
        {
            var presenceStatus = "Unknown";

            if (_storedWindow != null)
            {
                try
                {
                    _ = _storedWindow.Name;
                }
                catch
                {
                    _storedWindow = null;
                }
            }

            if (_storedWindow == null)
            {
                _storedWindow = FindWindow();
                if (_storedWindow == null)
                {
                    _logger.Warning("Could not find Teams window");
                    _lastStatus = TeamsStatus.Unknown;
                    return;
                }
            }

            var buttons = _storedWindow.FindAllDescendants(
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));

            var accountButton = buttons.FirstOrDefault(b =>
                b.AutomationId.Equals(StatusButtonId, StringComparison.OrdinalIgnoreCase));

            if (accountButton == null)
            {
                _logger.Error("Couldn't find button holding status info.");
                _lastStatus = TeamsStatus.Unknown;
                StatusChanged?.Invoke();
                return;
            }

            var statusWords = accountButton.Name.Split(' ');
            var status = statusWords.Skip(3).ToList();
            var forIndex = status.IndexOf("for");
            status = forIndex > 0 ? status.Take(forIndex).ToList() : status;
            presenceStatus = string.Join(" ", status);
            _logger.Information($"Status found: {presenceStatus}");

            var newStatus = GetStatusFromElementStatus(presenceStatus);
            if (newStatus != _lastStatus)
            {
                _lastStatus = newStatus;
                _logger.Information($"Status set to {_lastStatus}");
                StatusChanged?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading status");
        }
        finally
        {
            _statusCheckSemaphore.Release();
        }
    }

    private TeamsStatus GetStatusFromElementStatus(string statusString)
    {
        var status = statusString switch
        {
            "Available" => TeamsStatus.Available,
            "Busy" or "In a call" or "In a meeting" => TeamsStatus.Busy,
            "Presenting" or "Do not disturb" => TeamsStatus.DoNotDisturb,
            "Away" or "Be right back" => TeamsStatus.Away,
            _ => TeamsStatus.Unknown
        };

        if (status == TeamsStatus.Unknown)
        {
            _logger.Warning($"Unknown status: {statusString}");
        }

        return status;
    }

    public StatusColors GetColorFromStatus(StatusColors? colors)
    {
        var defaultColors = new StatusColors();
        colors ??= defaultColors;

        var statusColors = _lastStatus switch
        {
            TeamsStatus.Available => colors.Available,
            TeamsStatus.Busy => colors.Busy,
            TeamsStatus.DoNotDisturb => colors.DoNotDisturb,
            TeamsStatus.Away => colors.Away,
            TeamsStatus.Offline => colors.Offline,
            TeamsStatus.OutOfOffice => colors.OutOfOffice,
            _ => colors.Unknown
        };

        return new StatusColors
        {
            Available = statusColors,
            Busy = statusColors,
            DoNotDisturb = statusColors,
            Away = statusColors,
            Offline = statusColors,
            OutOfOffice = statusColors,
            Unknown = statusColors
        };
    }

    private AutomationElement? FindWindow()
    {
        using var automation = new UIA3Automation();
        var desktop = automation.GetDesktop();
        var windows = desktop.FindAllChildren(
            cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

        var foundWindows = windows.Where(w =>
            w.Name.Contains(WindowTitle, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!foundWindows.Any())
            return null;

        if (foundWindows.Count == 1)
            return foundWindows[0];

        foreach (var window in foundWindows)
        {
            var buttons = window.FindAllDescendants(
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            var accountButton = buttons.FirstOrDefault(b =>
                b.AutomationId.Equals(StatusButtonId, StringComparison.OrdinalIgnoreCase));
            if (accountButton != null)
                return window;
        }

        return foundWindows[0];
    }

    public void Dispose()
    {
        _statusTimer?.Stop();
        _statusCheckSemaphore.Dispose();
        _windowFinderSemaphore.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    ~TeamsApplicationStatusChecker() => Dispose();
}
