using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using TeamsStatusChecker.Enumerations;

namespace TeamsStatusChecker.StatusCheckers;

public abstract class StatusCheckerBase<T>(
    ApplicationCheck appToCheck,
    ILogger logger,
    string windowTitle,
    StatusChangedEventHandler statusChangedEventHandler,
    T initialLastStatus)
    : IDisposable, IStatusChecker
    where T : Enum
{
    public int PoolingIntervalSeconds { get; set; } = 5;
    protected AutomationElement? StoredWindow;
    public abstract Task GetCurrentStatus();
    
    private DispatcherTimer timer = new(TimeSpan.FromSeconds(5), DispatcherPriority.Background, StartTimerCallback , Dispatcher.CurrentDispatcher);

    public StatusChangedEventHandler StatusChanged { get; set; } = statusChangedEventHandler;
    
    internal readonly ILogger Logger = logger;

    private string WindowTitle {get; set;} = windowTitle;

    internal ApplicationCheck ApplicationToCheck { get; set; } = appToCheck;

    internal string? StatusElementSubString {get; set;}

    protected T LastStatus { get; set; } = initialLastStatus;

    internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
    
    internal readonly SemaphoreSlim StatusCheckSemaphore = new(1, 1);

    private readonly SemaphoreSlim windowFinderSemaphore = new(1, 1);
    
    private static async void StartTimerCallback(object? sender, EventArgs e)
    {
        await ((IStatusChecker)sender)?.GetCurrentStatus()!;
    }

    public bool NamePropertyContains(AutomationElement element, string value)
    {
        try
        {
            var propertyValue = element.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;
            Debug.WriteLine(propertyValue);
            return propertyValue != null && propertyValue.ToLowerInvariant().Contains(value.ToLowerInvariant());
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return false;
        }
    }

    public async void StartChecking()
    {
        await GetCurrentStatus();
        timer.Start();
    }
    
    public async void StopChecking()
    {
        await CancellationTokenSource.CancelAsync();
        timer.Stop();
    }

    public virtual async Task<AutomationElement?> FindWindowAsync()
    {
        var canEnter = await windowFinderSemaphore.WaitAsync(1000);
        if(!canEnter)
        {
            return StoredWindow;
        }
        AutomationElement? rootElement;
        AutomationElement? foundWindow = null;

        // Retry logic to handle timing issues
        for (var i = 0; i < 5; i++)
        {
            try
            {
                rootElement = AutomationElement.RootElement;

                var windows = await Task.Run(() => rootElement.FindAll(TreeScope.Children, Condition.TrueCondition),
                    CancellationTokenSource.Token);

                foreach (AutomationElement window in windows)
                {
                    if(window.GetCurrentPropertyValue(AutomationElement.NameProperty) == null)
                        continue;
                    
                    if (!NamePropertyContains(window, WindowTitle))
                        continue;
                    foundWindow = window;
                    break;
                }

                if (foundWindow != null)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error finding {Enum.GetName(typeof(ApplicationCheck), ApplicationToCheck)} window");
            }
            finally
            {
                windowFinderSemaphore.Release();
            }

            await Task.Delay(1000); // Wait for 1 second before retrying
        }

        return foundWindow;
    }

    internal abstract T GetStatusFromElementStatus(string statusString);

    public abstract SolidColorBrush GetColorFromStatus();

    public void Dispose()
    {
        StatusCheckSemaphore.Dispose();
        windowFinderSemaphore.Dispose();
    }
}