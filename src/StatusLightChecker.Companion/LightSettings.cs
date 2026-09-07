using System.Globalization;

namespace StatusLightChecker.Companion;

public sealed record LightPreset(string Status, string Hex, string Effect, int PeriodMs)
{
    public static readonly IReadOnlyList<string> Statuses = Array.AsReadOnly(
        new[] { "available", "busy", "away", "dnd", "offline" });

    public void Validate()
    {
        if (!Statuses.Contains(Status) || Effect is not ("steady" or "blink" or "pulse") ||
            PeriodMs is < 100 or > 60000 || Hex.Length != 7 || Hex[0] != '#' ||
            !Hex.AsSpan(1).ToString().All(char.IsAsciiHexDigit))
            throw new ArgumentException("Choose a status, #RRGGBB color, effect, and period from 100 to 60000 ms.");
    }

    private string Arguments
    {
        get
        {
            Validate();
            byte[] rgb = Convert.FromHexString(Hex[1..]);
            return FormattableString.Invariant($"{rgb[0]} {rgb[1]} {rgb[2]} {Effect} {PeriodMs}");
        }
    }
    public string Command => $"PRESET {Status} {Arguments}";
    public string PreviewCommand => $"NOTIFY {Arguments} 3000";
}

public sealed record LightSettings(int Brightness, int TimeoutSeconds, IReadOnlyList<LightPreset> Presets)
{
    public string[] Commands()
    {
        if (Brightness is < 0 or > 255 || TimeoutSeconds is < 0 or > 86400 ||
            Presets.Count != 5 || !Presets.Select(p => p.Status).Order().SequenceEqual(LightPreset.Statuses.Order()))
            throw new ArgumentException("Invalid light configuration.");
        return [.. Presets.Select(p => p.Command),
            FormattableString.Invariant($"BRIGHTNESS {Brightness}"),
            FormattableString.Invariant($"TIMEOUT {TimeoutSeconds}")];
    }

    public static LightSettings Parse(IReadOnlyList<string> response)
    {
        var fields = new Dictionary<string, string[]>();
        foreach (string line in response)
        {
            string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) throw new InvalidDataException("Incomplete device reply.");
            string key = words[0] == "PRESET" ? $"PRESET {words[1]}" : words[0];
            if (!fields.TryAdd(key, words)) throw new InvalidDataException("Duplicate device field.");
        }
        string[] Field(string key, int count)
        {
            if (!fields.TryGetValue(key, out var words) || words.Length != count)
                throw new InvalidDataException($"Missing or malformed device field: {key}");
            return words;
        }
        static int Number(string text, int max)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value > max)
                throw new InvalidDataException("Invalid device number.");
            return value;
        }
        if (Field("INFO", 2)[1] != "1") throw new InvalidDataException("This firmware protocol version is not supported.");
        var presets = new List<LightPreset>();
        foreach (string status in LightPreset.Statuses)
        {
            string[] words = Field($"PRESET {status}", 7);
            string hex = $"#{Number(words[2], 255):X2}{Number(words[3], 255):X2}{Number(words[4], 255):X2}";
            presets.Add(new(status, hex, words[5], Number(words[6], 60000)));
        }
        var result = new LightSettings(Number(Field("BRIGHTNESS", 2)[1], 255),
            Number(Field("TIMEOUT", 2)[1], 86400), presets.AsReadOnly());
        try { result.Commands(); }
        catch (ArgumentException ex) { throw new InvalidDataException("Invalid device settings.", ex); }
        return result;
    }
}
