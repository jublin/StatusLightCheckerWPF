using System.Text.RegularExpressions;

namespace StatusLightChecker.Companion;

public static class Presence
{
    // ponytail: English accessibility labels only; add localized phrases when needed.
    public static string? Parse(string label)
    {
        var match = Regex.Match(label,
            @"\b(Do not disturb|Presenting|In a meeting|In a call|Be right back|Appear offline|Offline|Available|Busy|Away)\b(?:\s+for\b.*)?[.,\s]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "available" => "available",
            "busy" or "in a call" or "in a meeting" => "busy",
            "do not disturb" or "presenting" => "dnd",
            "away" or "be right back" => "away",
            "offline" or "appear offline" => "offline",
            _ => null
        };
    }
}
