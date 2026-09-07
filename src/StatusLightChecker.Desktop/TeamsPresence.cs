using StatusLightChecker.Companion;

namespace StatusLightChecker.Desktop;

internal static class TeamsPresence
{
    public static string? Read()
    {
#if WINDOWS
        using var automation = new FlaUI.UIA3.UIA3Automation();
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("ms-teams"))
        {
            using (process)
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                var window = automation.FromHandle(process.MainWindowHandle);
                var button = window.FindFirstDescendant(cf => cf.ByAutomationId("idna-me-control-avatar-trigger"));
                if (button != null) return Presence.Parse(button.Name);
            }
        }
#endif
        return null;
    }
}
