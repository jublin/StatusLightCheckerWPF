using System.Windows.Automation;
using System.Windows.Media;
using Serilog;
using TeamsStatusChecker.Enumerations;

namespace TeamsStatusChecker.StatusCheckers;

public class TeamsApplicationStatusChecker(ILogger logger, StatusChangedEventHandler statusChangedEvent)
    : StatusCheckerBase<MicrosoftTeamsStatus>(ApplicationCheck.MicrosoftTeams, logger, "Microsoft Teams",
        statusChangedEvent,
        MicrosoftTeamsStatus.Unknown)
{
    public override async Task GetCurrentStatus()
    {
        var canEnter = await StatusCheckSemaphore.WaitAsync(1000);
        if(!canEnter)
        {
            return;
        }
        var presenceStatus = "Unknown";

        try
        {
            // Check if we already have a valid storedTeamsWindow
            if (StoredWindow != null)
            {
                try
                {
                    // Try to access a property to check if it's still valid
                    var cachedWindowName = StoredWindow.Current.Name;
                }
                catch
                {
                    // If accessing the property fails, the stored window is no longer valid
                    StoredWindow = null;
                }
            }

            if (StoredWindow == null)
            {
                StoredWindow = await FindWindowAsync();
                    
                if (StoredWindow == null)
                {
                    LastStatus = MicrosoftTeamsStatus.Unknown;
                    return;
                }
            }

            // Look for the presence status element within the Teams window
            var presenceElements = await Task.Run(() =>
            {
                var presenceCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
                return StoredWindow?.FindAll(TreeScope.Descendants, presenceCondition);
            }, CancellationTokenSource.Token);

            if (presenceElements != null)
                foreach (AutomationElement element in presenceElements)
                {
                    if(CancellationTokenSource.Token.IsCancellationRequested) return;
                    
                    Logger.Verbose($"presence element name found: {element.Current.Name}");
                    if (StatusElementSubString != null && (string.IsNullOrEmpty(element.Current.Name) ||
                                                           !element.Current.Name.Contains(StatusElementSubString)))
                    {
                        continue;
                    }

                    var statusWords = element.Current.Name.Split(' ');
                    var status = statusWords.Skip(3).ToList();
                    var forIndex = status.IndexOf("for");
                    status = status.Take(forIndex).ToList();
                    presenceStatus = string.Join(" ", status);
                }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "WindowsAutomation - Error reading status");
        }
        Logger.Information($"WindowsAutomation status found: {presenceStatus}");

        var newStatus = GetStatusFromElementStatus(presenceStatus);
        if (newStatus != LastStatus)
        {
            LastStatus = newStatus;
            StatusChanged?.Invoke();
            Logger.Information($"WindowsAutomation status set to {LastStatus}");
        }
        StatusCheckSemaphore.Release();
    }

    internal override MicrosoftTeamsStatus GetStatusFromElementStatus(string statusString)
    {
        switch (statusString)
        {
            case "Available":
                return MicrosoftTeamsStatus.Available;
            case "Busy":
            case "In a call":
                return MicrosoftTeamsStatus.Busy;
            case "Presenting":
            case "Do not disturb":
                return MicrosoftTeamsStatus.DoNotDisturb;
            case "Away":
            case "Be right back":
                return MicrosoftTeamsStatus.Away;
            default:
                Logger.Warning($"WindowsAutomation availability unknown: {statusString}");
                return MicrosoftTeamsStatus.Unknown;
        }
    }

    public override SolidColorBrush GetColorFromStatus()
    {
        switch (LastStatus)
        {
            case MicrosoftTeamsStatus.Available:
                return Brushes.Green;
            case MicrosoftTeamsStatus.Busy:
                return Brushes.Red;
            case MicrosoftTeamsStatus.DoNotDisturb:
                return Brushes.DarkRed;
            case MicrosoftTeamsStatus.InAMeeting:
                return Brushes.Red;
            case MicrosoftTeamsStatus.Away:
                return Brushes.Yellow;
            case MicrosoftTeamsStatus.Offline:
                return Brushes.Black;
            case MicrosoftTeamsStatus.OutOfOffice:
                return Brushes.Purple;
            case MicrosoftTeamsStatus.Unknown:
            default:
                return Brushes.Gray;
        }
    }
}