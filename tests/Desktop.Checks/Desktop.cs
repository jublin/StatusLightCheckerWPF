using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StatusLightChecker.Desktop;

AppBuilder.Configure<CompanionApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var window = new MainWindow();
window.Show();
Dispatcher.UIThread.RunJobs();
var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
if (tabs.ItemCount != 3) throw new Exception("Missing tabs");
tabs.SelectedIndex = 1;
window.UpdateLayout();
var fields = window.GetVisualDescendants().OfType<TextBox>().Where(x => x.MaxLength == 7).ToArray();
if (fields.Length != 5) throw new Exception("Missing preset color fields");
fields[0].Text = "#123456";
var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
if (!buttons.Any(x => x.Content?.ToString() == "Save to light •")) throw new Exception("Edits not marked unsaved");
buttons.Single(x => x.Content?.ToString() == "Restore default fields").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
if (fields[0].Text != "#00CC6A") throw new Exception("Defaults not restored");
buttons.First(x => x.Content?.ToString() == "Preview").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
Dispatcher.UIThread.RunJobs();
if (!window.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "Connect a light first.")) throw new Exception("Disconnected preview lacks error");
if (args.Length == 1)
{
    Directory.CreateDirectory(args[0]);
    for (int i = 0; i < 3; i++)
    {
        tabs.SelectedIndex = i; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        using var frame = window.CaptureRenderedFrame();
        if (frame == null) throw new Exception("No rendered frame");
        frame.Save(Path.Combine(args[0], $"companion-{i}.png"));
    }
}
window.Close();
Dispatcher.UIThread.RunJobs();
Console.WriteLine("Desktop control checks passed");
