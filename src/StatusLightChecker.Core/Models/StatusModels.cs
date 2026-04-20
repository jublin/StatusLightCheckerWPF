namespace StatusLightChecker.Core.Models;

public enum StatusLevel
{
    Unknown = 0,
    Available = 1,
    Busy = 2,
    DoNotDisturb = 3,
    Away = 4,
    Offline = 5
}

public record ApplicationStatus
{
    public string ApplicationName { get; init; } = string.Empty;
    public string StatusValue { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public StatusLevel StatusLevel { get; init; } = StatusLevel.Unknown;
    public string? Details { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public record StatusColor
{
    public StatusLevel StatusLevel { get; init; }
    public string Name { get; init; } = string.Empty;
    public string HexColor { get; init; } = "#808080";
    public bool IsBlinking { get; init; }
}

public record ColorConfiguration
{
    public StatusColor Available { get; init; } = new() { StatusLevel = StatusLevel.Available, Name = "Available", HexColor = "#00CC6A" };
    public StatusColor Busy { get; init; } = new() { StatusLevel = StatusLevel.Busy, Name = "Busy", HexColor = "#FF0000" };
    public StatusColor DoNotDisturb { get; init; } = new() { StatusLevel = StatusLevel.DoNotDisturb, Name = "Do Not Disturb", HexColor = "#B30000", IsBlinking = true };
    public StatusColor Away { get; init; } = new() { StatusLevel = StatusLevel.Away, Name = "Away", HexColor = "#FFCC00" };
    public StatusColor Offline { get; init; } = new() { StatusLevel = StatusLevel.Offline, Name = "Offline", HexColor = "#808080" };
    public StatusColor Unknown { get; init; } = new() { StatusLevel = StatusLevel.Unknown, Name = "Unknown", HexColor = "#CCCCCC" };
}

public class StatusChangedEventArgs : EventArgs
{
    public ApplicationStatus Status { get; }
    
    public StatusChangedEventArgs(ApplicationStatus status)
    {
        Status = status;
    }
}

public class ColorConfigurationChangedEventArgs : EventArgs
{
    public ColorConfiguration Configuration { get; }
    
    public ColorConfigurationChangedEventArgs(ColorConfiguration configuration)
    {
        Configuration = configuration;
    }
}