using StatusLightChecker.Companion;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Reject(Action action)
{
    try { action(); }
    catch (ArgumentException) { return; }
    catch (InvalidDataException) { return; }
    throw new Exception("Invalid input was accepted");
}

string[] reply = ["INFO 1", "BRIGHTNESS 64", "TIMEOUT 30", "ENABLED 1", "STATUS busy", "STALE 0",
    "PRESET available 0 255 0 steady 1000", "PRESET busy 255 40 0 blink 500",
    "PRESET away 255 160 0 steady 1000", "PRESET dnd 160 0 255 pulse 2000",
    "PRESET offline 0 0 0 steady 1000"];
var settings = LightSettings.Parse(reply);
Check(settings.Brightness == 64 && settings.TimeoutSeconds == 30, "Read device settings");
Check(settings.Presets[1].Hex == "#FF2800", "Decode device RGB");
Check(settings.Presets[1].Command == "PRESET busy 255 40 0 blink 500", "Encode preset command");
Check(settings.Commands().Last() == "TIMEOUT 30", "Configuration contains timeout");
Check(!settings.Commands().Contains("SAVE"), "Editing must not write flash automatically");
Check(new LightPreset("busy", "#1234ab", "pulse", 2000).PreviewCommand ==
    "NOTIFY 18 52 171 pulse 2000 3000", "Preview must expire after three seconds");
Reject(() => new LightPreset("busy\nSAVE", "#FFFFFF", "steady", 1000).Validate());
Reject(() => new LightPreset("busy", "#FFFFFG", "steady", 1000).Validate());
Reject(() => new LightPreset("busy", "#FFFFFF", "other", 1000).Validate());
Reject(() => new LightPreset("busy", "#FFFFFF", "pulse", 0).Validate());
Reject(() => LightSettings.Parse(reply.Where(x => !x.StartsWith("PRESET busy")).ToArray()));
Reject(() => LightSettings.Parse(reply.Append("BRIGHTNESS 128").ToArray()));
Reject(() => LightSettings.Parse(reply.Select(x => x == "INFO 1" ? "INFO 2" : x).ToArray()));
Reject(() => LightSettings.Parse(reply.Select(x => x == "TIMEOUT 30" ? "TIMEOUT -1" : x).ToArray()));
Console.WriteLine("Companion protocol checks passed");

using (var reader = new StringReader(string.Join("\r\n", reply) + "\r\nOK\n"))
    Check(LightConnection.ReadReply(reader.Read).SequenceEqual(reply), "Read bytewise reply");
using (var reader = new StringReader(new string('x', 256) + "\nOK\n"))
    Reject(() => LightConnection.ReadReply(reader.Read));
using (var reader = new StringReader("ERR command\n"))
{
    try { LightConnection.ReadReply(reader.Read); throw new Exception("Ignored device error"); }
    catch (IOException) { }
}
Console.WriteLine("Reply framing checks passed");

Check(Presence.Parse("Your profile, status Do not disturb for 1 hour") == "dnd", "DND label");
Check(Presence.Parse("Your profile, status In a call") == "busy", "Call label");
Check(Presence.Parse("Available Smith, status Unknown") == null, "Do not parse a person's name as presence");
Check(Presence.Parse("Your profile, status Away") == "away", "Away label");
Check(Presence.Parse("Your profile, status Inconnu") == null, "Unknown labels remain unknown");
Console.WriteLine("Presence label checks passed");
