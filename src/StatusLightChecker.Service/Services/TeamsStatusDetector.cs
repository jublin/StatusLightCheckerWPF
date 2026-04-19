using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Core.Models;
using StatusLightChecker.Core.Services;
using System.Diagnostics;

namespace StatusLightChecker.Service.Services;

public class TeamsStatusDetector : IStatusDetector
{
    private readonly ILogger<TeamsStatusDetector> _logger;
    private const string TeamsProcessName = "Teams";
    private const string TeamsProcessName2 = "ms-teams";

    public string ApplicationName => "Microsoft Teams";

    public TeamsStatusDetector(ILogger<TeamsStatusDetector> logger)
    {
        _logger = logger;
    }

    public Task<ApplicationStatus?> DetectStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var process = FindTeamsProcess();
            if (process == null)
            {
                return Task.FromResult<ApplicationStatus?>(null);
            }

            var status = DetectStatusFromProcess(process);
            return Task.FromResult<ApplicationStatus?>(status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting Teams status");
            return Task.FromResult<ApplicationStatus?>(null);
        }
    }

    private Process? FindTeamsProcess()
    {
        var process = Process.GetProcessesByName(TeamsProcessName).FirstOrDefault();
        if (process != null) return process;

        process = Process.GetProcessesByName(TeamsProcessName2).FirstOrDefault();
        if (process != null) return process;

        process = Process.GetProcessesByName(TeamsProcessName + ".exe").FirstOrDefault();
        return process;
    }

    private ApplicationStatus DetectStatusFromProcess(Process process)
    {
        var statusLevel = StatusLevel.Away;
        var statusLabel = "Away";
        var details = $"Process ID: {process.Id}";

        try
        {
            using var automation = new UIA3Automation();
            var app = FlaUI.Core.Application.Attach(process);
            var window = app.GetMainWindow(automation);

            if (window != null)
            {
                var statusIndicator = FindStatusIndicator(window);
                if (statusIndicator != null)
                {
                    (statusLevel, statusLabel) = ParseStatusFromElement(statusIndicator);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not access Teams UI elements");
            statusLevel = StatusLevel.Available;
            statusLabel = "Available";
        }

        return new ApplicationStatus
        {
            ApplicationName = ApplicationName,
            StatusValue = statusLevel.ToString().ToLowerInvariant(),
            StatusLabel = statusLabel,
            StatusLevel = statusLevel,
            Details = details
        };
    }

    private AutomationElement? FindStatusIndicator(AutomationElement window)
    {
        try
        {
            var buttons = window.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            
            foreach (var button in buttons)
            {
                var name = button.Name?.ToLowerInvariant() ?? "";
                if (name.Contains("status") || name.Contains("presence") || 
                    name.Contains("available") || name.Contains("busy") ||
                    name.Contains("away") || name.Contains("do not disturb"))
                {
                    return button;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error finding status indicator");
        }
        return null;
    }

    private (StatusLevel Level, string Label) ParseStatusFromElement(AutomationElement element)
    {
        var name = element.Name?.ToLowerInvariant() ?? "";
        
        if (name.Contains("available") || name.Contains("green"))
            return (StatusLevel.Available, "Available");
        if (name.Contains("busy") || name.Contains("red"))
            return (StatusLevel.Busy, "Busy");
        if (name.Contains("do not disturb") || name.Contains("dnd") || name.Contains("presenting"))
            return (StatusLevel.DoNotDisturb, "Do Not Disturb");
        if (name.Contains("away") || name.Contains("yellow"))
            return (StatusLevel.Away, "Away");
        if (name.Contains("offline") || name.Contains("gray"))
            return (StatusLevel.Offline, "Offline");
        
        return (StatusLevel.Unknown, "Unknown");
    }
}
