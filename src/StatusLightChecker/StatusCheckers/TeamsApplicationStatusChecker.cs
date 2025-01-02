using System.Drawing;
using System.Windows.Media;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Serilog;
using StatusLightChecker.Enumerations;
using Brushes = System.Windows.Media.Brushes;

namespace StatusLightChecker.StatusCheckers;

public class TeamsApplicationStatusChecker(ILogger logger, StatusChangedEventHandler statusChangedEvent)
    : StatusCheckerBase<MicrosoftTeamsStatus>(ApplicationCheck.MicrosoftTeams, logger, "Microsoft Teams",
        statusChangedEvent,
        MicrosoftTeamsStatus.Unknown)
{
    public override string StatusButtonId { get; set; } = "idna-me-control-avatar-trigger";
    public static AutomationElement? FindButtonById(string buttonId)
    {
        using var automation = new UIA3Automation();
        var desktop = automation.GetDesktop();
        var buttons = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));

        foreach (var button in buttons)
        {
            if (button.AutomationId.Equals(buttonId, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }
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
                    var cachedWindowName = StoredWindow.Name;
                }
                catch
                {
                    // If accessing the property fails, the stored window is no longer valid
                    StoredWindow = null;
                }
            }
            if (StoredWindow == null)
            {
                StoredWindow = FindWindow();
                if (StoredWindow == null)
                {
                    Log.Error("StatusLightChecker - TeamsApplicationStatusChecker - Could not find Teams window");
                    LastStatus = MicrosoftTeamsStatus.Unknown;
                    return;
                }
            }
            var buttons = StoredWindow.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            var accountbutton = buttons.FirstOrDefault(b =>
                b.AutomationId.Equals(StatusButtonId, StringComparison.OrdinalIgnoreCase));
            if (accountbutton == null)
            {
                Log.Error("Couldn't find button holding status info.");
                LastStatus = MicrosoftTeamsStatus.Unknown;
                StatusChanged?.Invoke();
                throw new Exception("Couldn't find button holding status info.");
            }
            var statusWords = accountbutton.Name.Split(' ');
            var status = statusWords.Skip(3).ToList();
            var forIndex = status.IndexOf("for");
            status = status.Take(forIndex).ToList();
            presenceStatus = string.Join(" ", status);
            Logger.Information($"Status found: {presenceStatus}");

            var newStatus = GetStatusFromElementStatus(presenceStatus);
            if (newStatus != LastStatus)
            {
                LastStatus = newStatus;
                StatusChanged?.Invoke();
                Logger.Information($"status set to {LastStatus}");
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "- Error reading status");
        }
        
        finally

        {
            StatusCheckSemaphore.Release();
        }
        
    }

    internal override MicrosoftTeamsStatus GetStatusFromElementStatus(string statusString)
    {
        switch (statusString)
        {
            case "Available":
                return MicrosoftTeamsStatus.Available;
            case "Busy":
            case "In a call":
            case "In a meeting":
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

    // private Icon IconFromStatus()
    // {
    //     return new Icon()
    // }
}