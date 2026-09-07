# Status Light companion

The new companion is `src/StatusLightChecker.Desktop`. It uses Avalonia for a portable desktop UI and shares its firmware protocol code with `StatusLightChecker.Companion`. The original WPF app and service remain available separately.

## Run on Windows

Extract the entire `StatusLightCompanion-win-x64.zip` and run `StatusLightChecker.Desktop.exe`. The self-contained package includes .NET; it does not need an administrator installer or a Teams tenant application registration. Company endpoint policies may still restrict applications or USB devices.

1. Flash the configurable notifier firmware from TeamsLightRP2040. This app requires protocol version 1 at 57600 baud; the original raw-RGB firmware is incompatible.
2. Close the old helper/service if it owns the light's serial port.
3. Open **Settings**, select the USB port, and **Connect**. Connection reads the light's current settings. The app remembers the selected port but requires an explicit connection after restarting.
4. Choose **Automatic** to follow Teams, or **Manual** to select a status yourself.
5. In **Appearance**, edit a `#RRGGBB` color, effect, and period (100–60000 milliseconds). **Preview** plays it on the light for three seconds. **Save to light** applies all fields and writes persistent device settings. Restoring default fields only edits the form.
6. **Apply brightness** and **Light on** affect live output. Use **Save to light** to retain brightness after power loss. The on/off switch is session-only.

The timeout controls when firmware displays its Offline preset after updates stop; 0 disables it. A detected status that cannot be read displays **Status unavailable** and sends Offline in Automatic mode. Manual selection does not change your Teams presence. The on-screen orb illustrates the selected output; it is not a sensor reading and animation timing is approximate.

**Minimize to tray** hides a minimized window; use the tray's **Show Status Light** menu to restore it. Closing the window or choosing **Quit** exits the app. Tray support on Linux depends on the desktop environment. Windows launch-at-sign-in uses the current user's startup registry entry and is opt-in; enable it from the published executable.

## Presence limitations

Windows presence reads the local Teams accessibility account button in the same signed-in desktop session. Teams must be running and expose that button. This retains the original helper's English-label limitation; Teams updates, localization, or missing/minimized accessibility elements may produce an unavailable result. Failed reads never become Available. A stuck accessibility call does not block serial commands.

macOS and Linux currently support manual status and all USB light configuration, but do not have automatic Teams detection. No Graph sign-in, browser extension, or tenant consent flow is implemented.

## Build and check

With .NET 10 SDK:

```sh
dotnet run --project src/StatusLightChecker.Desktop
dotnet run --project tests/Companion.Checks/Companion.Checks.csproj
dotnet run --project tests/Desktop.Checks/Desktop.Checks.csproj
dotnet publish src/StatusLightChecker.Desktop -c Release -r win-x64 --self-contained true -o build/companion-win-x64
```

On Windows, the desktop project selects its Windows target and includes FlaUI. Passing `-r win-x64` also selects it when cross-building. Non-Windows targets exclude FlaUI. Cross-build other releases with `-r linux-x64`, `-r osx-arm64`, or the appropriate runtime identifier.

Checks cover configuration parsing/validation, bytewise reply framing and error handling, English presence labels, and rendered desktop controls (tabs, preset editing/defaults, and disconnected preview errors). Desktop checks can optionally write rendered screens by appending `-- /path/to/screens`.

Verified in this development environment: portable checks, headless desktop controls, and Windows cross-publishing. Real Windows Teams detection, physical USB operation, startup, and tray behavior still require a Windows/device smoke test.
