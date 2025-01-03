﻿using System.Windows.Media;
using System.Windows.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Serilog;
using StatusLightChecker.Enumerations;

namespace StatusLightChecker.StatusCheckers;

public abstract class StatusCheckerBase<T>(
    ApplicationCheck appToCheck,
    ILogger logger,
    string windowTitle,
    StatusChangedEventHandler statusChangedEventHandler,
    T initialLastStatus)
    : IDisposable, IStatusChecker
    where T : Enum
{
    public virtual string StatusButtonId { get; set; } = "status_button";
    public int PoolingIntervalSeconds { get; set; } = 3;
    protected AutomationElement? StoredWindow;
    public abstract Task GetCurrentStatus();

    private DispatcherTimer statusTimer;

    public StatusChangedEventHandler StatusChanged { get; set; } = statusChangedEventHandler;
    
    internal readonly ILogger Logger = logger;

    private string WindowTitle {get; set;} = windowTitle;

    internal ApplicationCheck ApplicationToCheck { get; set; } = appToCheck;

    internal string? StatusElementSubString {get; set;}

    protected T LastStatus { get; set; } = initialLastStatus;

    internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
    
    internal readonly SemaphoreSlim StatusCheckSemaphore = new(1, 1);

    private readonly SemaphoreSlim windowFinderSemaphore = new(1, 1);


    public async void StartChecking()
    {
        InitializeTimer();
        statusTimer.Start();
    }
    
    public async void StopChecking()
    {
        statusTimer.Stop();
        await CancellationTokenSource.CancelAsync();
    }

    public void InitializeTimer()
    {
        statusTimer = new DispatcherTimer(TimeSpan.FromSeconds(PoolingIntervalSeconds), DispatcherPriority.Normal, StatusTimerCallback, Dispatcher.CurrentDispatcher);
    }

    private async void StatusTimerCallback(object? sender, EventArgs e)
    {
        await GetCurrentStatus();
        statusTimer.Start();
    }

    private List<AutomationElement>? FindWindows()
    {
        using var automation = new UIA3Automation();
        var desktop = automation.GetDesktop();
        var windows = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
        if (windows.Any(window => window.Name.Contains(WindowTitle, StringComparison.OrdinalIgnoreCase)))
        {
            return windows.Where(window => window.Name.Contains(WindowTitle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return null;
    }

    public AutomationElement? FindWindow()
    {
        var foundWindows = FindWindows();
        if (foundWindows == null || foundWindows.Count == 0)
        {
            return null;
        }

        if (foundWindows.Count > 1)
        {
            return (from window in foundWindows
                let buttons =
                    window.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                let accountbutton = buttons.FirstOrDefault(b =>
                    b.AutomationId.Equals(StatusButtonId, StringComparison.OrdinalIgnoreCase))
                select accountbutton).OfType<AutomationElement>().FirstOrDefault();
        }

        return foundWindows[0];
    }

    

    internal abstract T GetStatusFromElementStatus(string statusString);

    public abstract SolidColorBrush GetColorFromStatus();

    public void Dispose()
    {
        StatusCheckSemaphore.Dispose();
        windowFinderSemaphore.Dispose();
    }
}