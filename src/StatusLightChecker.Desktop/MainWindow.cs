using System.Globalization;
using System.IO.Ports;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using StatusLightChecker.Companion;

namespace StatusLightChecker.Desktop;

public sealed class MainWindow : Window
{
    private LightConnection connection = new();
    private readonly TextBlock detected = Text("Looking for Teams…", 24);
    private readonly TextBlock output = Text("Light disconnected", 16);
    private readonly TextBlock message = Text("Connect your light in Settings.");
    private readonly TextBlock brightnessLabel = Text("Brightness · 40%");
    private readonly Ellipse orb = new()
    {
        Width = 88,
        Height = 88,
        Fill = Brushes.Gray,
        Margin = new Thickness(0, 12),
    };
    private readonly ComboBox mode = Choice(["Automatic", "Manual"], 0);
    private readonly ComboBox manual = Choice(LightPreset.Statuses.ToArray(), 0);
    private readonly ComboBox ports = new() { MinWidth = 240 };
    private readonly Slider brightness = new()
    {
        Minimum = 0,
        Maximum = 100,
        Value = 40,
        TickFrequency = 1,
        IsSnapToTickEnabled = true,
    };
    private readonly CheckBox enabled = new() { Content = "Light on", IsChecked = true };
    private readonly NumericUpDown timeout = new()
    {
        Minimum = 0,
        Maximum = 86400,
        Value = 30,
        Increment = 10,
        FormatString = "0",
    };
    private readonly CheckBox minimize = new() { Content = "Minimize to tray" };
    private readonly CheckBox startup = new() { Content = "Launch when I sign in (Windows)" };
    private readonly Button connect = new() { Content = "Connect" };
    private readonly Button save = new() { Content = "Save to light" };
    private readonly TabControl tabs = new();
    private readonly Dictionary<
        string,
        (TextBox Hex, ComboBox Effect, NumericUpDown Period)
    > editors = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly string preferencesPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StatusLightCompanion",
        "settings.json"
    );
    private TrayIcon? tray;
    private bool connected,
        loading,
        exiting;
    private readonly SemaphoreSlim operations = new(1);
    private string? presence,
        lastSentStatus;
    private Task<string?>? presenceRead;
    private DateTime presenceReadStarted;
    private readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private int ticks;
    private string selectedStatus = "offline";
    private LightSettings applied = Defaults();
    private DateTime previewUntil;
    private LightPreset? previewPreset;
    private Preferences preferences = new(null, false);

    private sealed record Preferences(string? Port, bool Minimize);

    public MainWindow()
    {
        Title = "Status Light";
        Width = 560;
        Height = 720;
        MinWidth = 520;
        MinHeight = 580;
        Background = Brush.Parse("#111519");
        FontSize = 14;
        var light = Stack(
            Text("TEAMS STATUS", 11),
            detected,
            Text("Detected presence stays separate from your light output."),
            Card(Stack(Text("Light output", 18), Row(mode, manual), orb, output)),
            brightnessLabel,
            brightness,
            Button(
                "Apply brightness",
                async () =>
                {
                    await Send($"BRIGHTNESS {BrightnessValue()}");
                    applied = applied with { Brightness = BrightnessValue() };
                    MarkDirty();
                }
            ),
            enabled
        );
        manual.IsVisible = false;
        var appearance = Stack(
            Row(Text("Status presets", 20), save),
            Text("Colors use #RRGGBB. Effect period is in milliseconds.")
        );
        foreach (var status in LightPreset.Statuses)
        {
            var hex = new TextBox { Width = 112, MaxLength = 7 };
            var effect = Choice(["steady", "blink", "pulse"], 0);
            effect.Width = 106;
            var period = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 60000,
                Increment = 100,
                Value = 1000,
                Width = 160,
                FormatString = "0",
            };
            AutomationProperties.SetName(hex, status + " color");
            AutomationProperties.SetName(effect, status + " effect");
            AutomationProperties.SetName(period, status + " period in milliseconds");
            editors.Add(status, (hex, effect, period));
            var swatch = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.Gray,
            };
            var preview = Button(
                "Preview",
                async () =>
                {
                    var preset = ReadPreset(status);
                    await Send(preset.PreviewCommand);
                    previewPreset = preset;
                    previewUntil = DateTime.UtcNow.AddSeconds(3);
                    tabs.SelectedIndex = 0;
                }
            );
            appearance.Children.Add(
                Card(
                    Stack(
                        Row(
                            swatch,
                            Text(
                                status == "dnd"
                                    ? "Do not disturb"
                                    : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(status),
                                15
                            ),
                            preview
                        ),
                        Row(hex, effect, period)
                    )
                )
            );
            hex.TextChanged += (_, _) =>
            {
                MarkDirty();
                if (Color.TryParse(hex.Text, out var color))
                    swatch.Fill = new SolidColorBrush(color);
            };
            effect.SelectionChanged += (_, _) => MarkDirty();
            period.ValueChanged += (_, _) => MarkDirty();
        }
        appearance.Children.Add(
            Button(
                "Restore default fields",
                () =>
                {
                    Load(Defaults());
                    MarkDirty();
                    return Task.CompletedTask;
                }
            )
        );
        var settings = Stack(
            Text("Device", 20),
            Text("USB port"),
            ports,
            Row(
                connect,
                Button(
                    "Refresh ports",
                    () =>
                    {
                        RefreshPorts();
                        return Task.CompletedTask;
                    }
                )
            ),
            Text("If updates stop, show Offline after (seconds)", 15),
            timeout,
            Text("0 disables the timeout. Save to light applies this setting."),
            Text("Application", 20),
            minimize,
            startup,
            Text(
                "Automatic presence uses the local Windows Teams app.\nOn macOS and Linux, choose Manual. No tenant app registration."
            )
        );
        tabs.ItemsSource = new[]
        {
            Tab("Light", light),
            Tab("Appearance", appearance),
            Tab("Settings", settings),
        };
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(20),
        };
        layout.Children.Add(Text("Status Light", 24));
        Grid.SetRow(tabs, 1);
        layout.Children.Add(tabs);
        message.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(message, 2);
        layout.Children.Add(message);
        Content = layout;
        AutomationProperties.SetName(mode, "Light mode");
        AutomationProperties.SetName(manual, "Manual status");
        AutomationProperties.SetName(ports, "USB port");
        AutomationProperties.SetName(brightness, "Brightness percent");
        AutomationProperties.SetName(timeout, "Stale timeout in seconds");
        Load(applied);
        brightness.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                brightnessLabel.Text = $"Brightness · {brightness.Value:0}%";
                MarkDirty();
            }
        };
        mode.SelectionChanged += async (_, _) =>
        {
            manual.IsVisible = mode.SelectedIndex == 1;
            await Run(UpdateOutput);
        };
        manual.SelectionChanged += async (_, _) => await Run(UpdateOutput);
        enabled.IsCheckedChanged += async (_, _) =>
        {
            if (!loading)
                await Run(async () => await Send(enabled.IsChecked == true ? "ON" : "OFF"));
        };
        timeout.ValueChanged += (_, _) => MarkDirty();
        connect.Click += async (_, _) => await Run(Connect);
        save.Click += async (_, _) =>
            await Run(async () =>
            {
                var value = ReadSettings();
                await connection.ExecuteAsync([.. value.Commands(), "SAVE"]);
                applied = value;
                save.Content = "Save to light";
                message.Text = "Settings saved to the light.";
            });
        minimize.IsCheckedChanged += (_, _) =>
        {
            if (!loading)
                SavePreferences();
        };
        startup.IsEnabled = OperatingSystem.IsWindows();
        startup.IsCheckedChanged += async (_, _) =>
        {
            if (!loading)
                await Run(() =>
                {
                    try
                    {
                        SetStartup(startup.IsChecked == true);
                    }
                    finally
                    {
                        loading = true;
                        try
                        {
                            startup.IsChecked = HasStartup();
                        }
                        finally
                        {
                            loading = false;
                        }
                    }
                    return Task.CompletedTask;
                });
        };
        timer.Tick += async (_, _) =>
            await Run(
                async () =>
                {
                    if (presenceRead?.IsCompleted == true)
                    {
                        try
                        {
                            presence = presenceRead.GetAwaiter().GetResult();
                        }
                        catch
                        {
                            presence = null;
                        }
                        presenceRead = null;
                    }
                    if (
                        presenceRead != null
                        && DateTime.UtcNow - presenceReadStarted > TimeSpan.FromSeconds(5)
                    )
                        presence = null;
                    if (ticks++ % 12 == 0 && presenceRead == null && OperatingSystem.IsWindows())
                    {
                        presenceReadStarted = DateTime.UtcNow;
                        // One outstanding native read at most; a stuck provider cannot block light commands.
                        presenceRead = Task.Run(TeamsPresence.Read);
                    }
                    detected.Text =
                        !OperatingSystem.IsWindows() ? "Automatic presence unavailable"
                        : presence is null ? "Status unavailable"
                        : presence == "dnd" ? "Do not disturb"
                        : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(presence);
                    if (connected)
                        await UpdateOutput();
                    DrawOutput();
                },
                silent: true
            );
        Opened += (_, _) =>
        {
            try
            {
                if (File.Exists(preferencesPath))
                    preferences =
                        JsonSerializer.Deserialize<Preferences>(File.ReadAllText(preferencesPath))
                        ?? preferences;
                loading = true;
                minimize.IsChecked = preferences.Minimize;
                startup.IsChecked = HasStartup();
                loading = false;
                CreateTray();
                RefreshPorts();
                if (!OperatingSystem.IsWindows())
                {
                    mode.SelectedIndex = 1;
                    detected.Text = "Automatic presence unavailable";
                }
                if (preferences.Port != null && ports.Items.Contains(preferences.Port))
                    ports.SelectedItem = preferences.Port;
            }
            catch (Exception ex)
            {
                loading = false;
                message.Text = ex.Message;
            }
            timer.Start();
            animation.Start();
        };
        PropertyChanged += (_, e) =>
        {
            if (
                e.Property == WindowStateProperty
                && WindowState == WindowState.Minimized
                && minimize.IsChecked == true
                && tray != null
            )
                Hide();
        };
        animation.Tick += (_, _) => DrawOutput();
        Closing += async (_, e) =>
        {
            if (exiting)
                return;
            e.Cancel = true;
            timer.Stop();
            animation.Stop();
            await connection.DisposeAsync();
            tray?.Dispose();
            exiting = true;
            Close();
        };
    }

    private static TextBlock Text(string text, double size = 13) =>
        new()
        {
            Text = text,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };

    private static StackPanel Stack(params Control[] items)
    {
        var p = new StackPanel { Spacing = 12 };
        foreach (var item in items)
            p.Children.Add(item);
        return p;
    }

    private static StackPanel Row(params Control[] items)
    {
        var p = Stack(items);
        p.Orientation = Orientation.Horizontal;
        p.Spacing = 8;
        return p;
    }

    private static Border Card(Control content) =>
        new()
        {
            Background = Brush.Parse("#191F24"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = content,
        };

    private static ComboBox Choice(string[] values, int selected) =>
        new()
        {
            ItemsSource = values,
            SelectedIndex = selected,
            MinWidth = 100,
        };

    private static TabItem Tab(string title, Control content) =>
        new()
        {
            Header = title,
            Content = new ScrollViewer { Content = content, Margin = new Thickness(0, 16, 0, 0) },
        };

    private Button Button(string title, Func<Task> action)
    {
        var button = new Button { Content = title };
        button.Click += async (_, _) => await Run(action);
        return button;
    }

    private async Task Run(Func<Task> action, bool silent = false)
    {
        if (exiting)
            return;
        if (silent)
        {
            if (!operations.Wait(0))
                return;
        }
        else
            await operations.WaitAsync();
        if (!silent)
            tabs.IsEnabled = false;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            message.Text = ex.Message;
            if (!connection.IsConnected)
            {
                connected = false;
                connect.Content = "Connect";
                output.Text = "Light disconnected";
            }
        }
        finally
        {
            operations.Release();
            if (!silent)
                tabs.IsEnabled = true;
        }
    }

    private async Task Send(params string[] commands)
    {
        await connection.ExecuteAsync(commands);
    }

    private async Task Connect()
    {
        if (connected)
        {
            await connection.DisposeAsync();
            connection = new();
            connected = false;
            connect.Content = "Connect";
            output.Text = "Light disconnected";
            message.Text = "Disconnected.";
            return;
        }
        if (ports.SelectedItem is not string name)
            throw new ArgumentException("Select a USB port first.");
        applied = await connection.ConnectAsync(name);
        connected = true;
        lastSentStatus = null;
        Load(applied);
        await UpdateOutput();
        await Send(enabled.IsChecked == true ? "ON" : "OFF");
        connect.Content = "Disconnect";
        preferences = preferences with { Port = name };
        SavePreferences();
        message.Text = "Connected · " + name;
    }

    private async Task UpdateOutput()
    {
        if (!connected)
            return;
        selectedStatus =
            mode.SelectedIndex == 1
                ? manual.SelectedItem as string ?? "offline"
                : presence ?? "offline";
        await Send(lastSentStatus == selectedStatus ? "PING" : "STATUS " + selectedStatus);
        lastSentStatus = selectedStatus;
        DrawOutput();
    }

    private void DrawOutput()
    {
        var preset =
            previewUntil > DateTime.UtcNow
                ? previewPreset!
                : applied.Presets.First(p => p.Status == selectedStatus);
        orb.Fill = Brush.Parse(connected ? preset.Hex : "#72808B");
        var phase =
            (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond % preset.PeriodMs)
            / (double)preset.PeriodMs;
        var effect = preset.Effect switch
        {
            "blink" => phase < .5 ? 1 : 0,
            "pulse" => .5 - .5 * Math.Cos(phase * Math.Tau),
            _ => 1,
        };
        orb.Opacity =
            enabled.IsChecked == true && connected ? applied.Brightness / 255d * effect : .1;
        output.Text =
            !connected ? "Light disconnected"
            : enabled.IsChecked != true ? "Light off"
            : previewUntil > DateTime.UtcNow ? "Preview · 3 seconds"
            : "Output · " + selectedStatus;
    }

    private int BrightnessValue() => (int)Math.Round(brightness.Value * 255 / 100);

    private LightPreset ReadPreset(string status)
    {
        var row = editors[status];
        var p = new LightPreset(
            status,
            row.Hex.Text ?? "",
            row.Effect.SelectedItem as string ?? "",
            (int)(row.Period.Value ?? 0)
        );
        p.Validate();
        return p;
    }

    private LightSettings ReadSettings() =>
        new(
            BrightnessValue(),
            (int)(timeout.Value ?? throw new ArgumentException("Enter a timeout in seconds.")),
            LightPreset.Statuses.Select(ReadPreset).ToArray()
        );

    private void Load(LightSettings settings)
    {
        loading = true;
        brightness.Value = settings.Brightness * 100d / 255;
        timeout.Value = settings.TimeoutSeconds;
        foreach (var p in settings.Presets)
        {
            var row = editors[p.Status];
            row.Hex.Text = p.Hex;
            row.Effect.SelectedItem = p.Effect;
            row.Period.Value = p.PeriodMs;
        }
        save.Content = "Save to light";
        loading = false;
    }

    private void MarkDirty()
    {
        if (loading)
            return;
        save.Content = "Save to light •";
    }

    private void RefreshPorts()
    {
        var selected = ports.SelectedItem;
        ports.ItemsSource = SerialPort.GetPortNames().Order().ToArray();
        ports.SelectedItem = selected;
        if (ports.SelectedIndex < 0 && ports.ItemCount > 0)
            ports.SelectedIndex = 0;
    }

    private void SavePreferences()
    {
        try
        {
            preferences = preferences with { Minimize = minimize.IsChecked == true };
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(preferencesPath)!);
            File.WriteAllText(preferencesPath + ".tmp", JsonSerializer.Serialize(preferences));
            File.Move(preferencesPath + ".tmp", preferencesPath, true);
        }
        catch (Exception ex)
        {
            message.Text = "Could not save application settings: " + ex.Message;
        }
    }

    private static LightSettings Defaults() =>
        new(
            102,
            30,
            [
                new("available", "#00CC6A", "steady", 1000),
                new("busy", "#F06969", "steady", 1000),
                new("away", "#E5B84D", "steady", 1000),
                new("dnd", "#B997EA", "steady", 1000),
                new("offline", "#72808B", "steady", 1000),
            ]
        );

    private void CreateTray()
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(24, 24),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Premul
        );
        using (var buffer = bitmap.Lock())
        {
            var pixels = new byte[buffer.RowBytes * 24];
            for (var y = 0; y < 24; y++)
            for (var x = 0; x < 24; x++)
                if ((x - 12) * (x - 12) + (y - 12) * (y - 12) < 100)
                {
                    var i = y * buffer.RowBytes + x * 4;
                    pixels[i] = 173;
                    pixels[i + 1] = 214;
                    pixels[i + 2] = 120;
                    pixels[i + 3] = 255;
                }
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        }
        var show = new NativeMenuItem("Show Status Light");
        show.Click += (_, _) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => Close();
        tray = new TrayIcon
        {
            Icon = new WindowIcon(bitmap),
            ToolTipText = "Status Light",
            Menu = new NativeMenu { Items = { show, quit } },
        };
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
    }

    private static bool HasStartup()
    {
#if WINDOWS
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run"
        );
        return key?.GetValue("StatusLightCompanion") != null;
#else
        return false;
#endif
    }

    private static void SetStartup(bool value)
    {
#if WINDOWS
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run"
        );
        if (value)
        {
            string executable =
                Environment.ProcessPath ?? throw new IOException("Cannot locate the app.");
            if (
                System
                    .IO.Path.GetFileNameWithoutExtension(executable)
                    .Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            )
                throw new IOException(
                    "Publish and run the app executable before enabling startup."
                );
            key.SetValue("StatusLightCompanion", "\"" + executable + "\"");
        }
        else
            key.DeleteValue("StatusLightCompanion", false);
#endif
    }
}
