using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using TeamsStatusChecker.Configuration;

namespace TeamsStatusChecker.Services;

public class TeamsStatusAutomationService : IMicrosoftTeamsService
{
    public bool isChecking { get; private set; }
    private AutomationElement? storedTeamsWindow = null;
    private readonly CancellationToken token = new CancellationToken();
    private readonly Serilog.ILogger _logger;
    private readonly SourceWindowsAutomationConfiguration _windowsAutomationConfiguration;
    private MicrosoftTeamsStatus _lastStatus = MicrosoftTeamsStatus.Unknown;

    public EventHandler<MicrosoftTeamsStatus> StatusChanged;

    public TeamsStatusAutomationService(
        SourceWindowsAutomationConfiguration config, Serilog.ILogger logger)
    {
        _logger = logger;

        if (config == null)
            throw new ConfigurationException("Configuration not found for source service WindowsAutomationConfiguration.");

        _windowsAutomationConfiguration = config;
        PoolingInterval = _windowsAutomationConfiguration.Interval;
    }

    public int PoolingInterval { get; set; }

    private bool NamePropertyContains(AutomationElement element, string value)
    {
        var propertyValue = element.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;
        return propertyValue != null && propertyValue.Contains(value);
    }

    private SemaphoreSlim finderSemaphore = new SemaphoreSlim(1, 1);
    private async Task<AutomationElement?> FindTeamsWindowAsync()
    {
        var canEnter = await finderSemaphore.WaitAsync(1000);
        if(!canEnter)
        {
            return storedTeamsWindow;
        }
        AutomationElement? rootElement;
        AutomationElement? teamsWindow = null;

        // Retry logic to handle timing issues
        for (int i = 0; i < 5; i++)
        {
            try
            {
                rootElement = AutomationElement.RootElement;


                var windowCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);


                var windows = await Task.Run(() => rootElement.FindAll(TreeScope.Children, Condition.TrueCondition),
                    token);

                foreach (AutomationElement window in windows)
                {
                    if (NamePropertyContains(window, _windowsAutomationConfiguration.WindowName ?? "Microsoft Teams"))
                    {
                        teamsWindow = window;
                        break;
                    }
                }

                if (teamsWindow != null)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error finding Teams window");
            }
            finally
            {
                finderSemaphore.Release();
            }

            await Task.Delay(1000); // Wait for 1 second before retrying
        }

        return teamsWindow;
    }
    private SemaphoreSlim statusSemaphore = new SemaphoreSlim(1, 1);
    public async Task GetCurrentStatus()
    {
        var canEnter = await statusSemaphore.WaitAsync(1000);
        if(!canEnter)
        {
            return;
        }
        var presenceStatus = "Unknown";
        var windowName = _windowsAutomationConfiguration.WindowName;
        var statusPattern = _windowsAutomationConfiguration.StatusPattern.Replace("@status", "(\\w+)");

        try
        {
            // Check if we already have a valid storedTeamsWindow
            if (storedTeamsWindow != null)
            {
                try
                {
                    // Try to access a property to check if it's still valid
                    var cachedWindowName = storedTeamsWindow.Current.Name;
                }
                catch
                {
                    // If accessing the property fails, the stored window is no longer valid
                    storedTeamsWindow = null;
                }
            }

            if (storedTeamsWindow == null)
            {
                storedTeamsWindow = await FindTeamsWindowAsync();
                    
                if (storedTeamsWindow == null)
                {
                    isChecking = false;
                    StatusChanged?.Invoke(this, MicrosoftTeamsStatus.Unknown); // Return early if no Teams window is found
                }
            }

            // Look for the presence status element within the Teams window
            var presenceElements = await Task.Run(() =>
            {
                var presenceCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
                return storedTeamsWindow.FindAll(TreeScope.Descendants, presenceCondition);
            }, token);

            foreach (AutomationElement element in presenceElements)
            {
                // On my system, with the "new" Teams UI installed, I had to look for the string:
                // "Your profile picture with status displayed as"
                // and then look at the next word, which is my current status.
                _logger.Verbose($"presence element name found: {element.Current.Name}");
                if (string.IsNullOrEmpty(element.Current.Name) || !element.Current.Name.Contains("Your profile, status")) continue;

                var stat = element.Current.Name.Split(' ');
                var status = stat.Skip(3).ToList();
                var forIndex = status.IndexOf("for");
                status = status.Take(forIndex).ToList();
                presenceStatus = string.Join(" ", status);
                var y = presenceStatus;

                // Debug.WriteLine(element.Current.Name);
                // if (match.Success)
                // {
                //     // Let's grab the status by looking at everything after "displayed as ", removing the trailing ".",
                //     // and setting it to lowercase. I set it to lowercase because that is how I have my ESP32-C3
                //     // set up to read the data that this C# app sends to it.
                //     presenceStatus = match.Groups[1].Value;
                //     break;
                // }
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "WindowsAutomation - Error reading status");
        }
        _logger.Information($"WindowsAutomation status found: {presenceStatus}");

        MicrosoftTeamsStatus newStatus;

        // Return what we found
        switch (presenceStatus)
        {
            case "Available":
                newStatus = MicrosoftTeamsStatus.Available;
                break;

            case "Busy":
            case "In a call":
                newStatus = MicrosoftTeamsStatus.Busy;
                break;

            case "Presenting":
            case "Do not disturb":
                newStatus = MicrosoftTeamsStatus.DoNotDisturb;
                break;
                case "Away":
            case "Be right back":
                newStatus = MicrosoftTeamsStatus.Away;
                break;

            default:
                _logger.Warning($"WindowsAutomation availability unknown: {presenceStatus}");
                newStatus = MicrosoftTeamsStatus.Unknown;
                break;
        }

        if (newStatus != _lastStatus)
        {
            _lastStatus = newStatus;
            _logger.Information($"WindowsAutomation status set to {_lastStatus}");
        }
        statusSemaphore.Release();
        StatusChanged?.Invoke(this, _lastStatus);
    }
}