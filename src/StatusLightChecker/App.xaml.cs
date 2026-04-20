using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using StatusLightChecker.Services;
using StatusLightChecker.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace StatusLightChecker;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = ConfigureServices();
        ServiceProvider = services.BuildServiceProvider();

        // Log to AppData\Local so the client never tries to write into ProgramFiles.
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Jublin", "StatusLightChecker", "Client", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(ServiceProvider.GetRequiredService<IConfiguration>())
            .WriteTo.File(
                Path.Combine(logDir, "client-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // Load appsettings.json from the executable's directory, not the working
        // directory. These diverge when launched via a Start Menu shortcut.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<App>(optional: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddLogging(builder =>
        {
            builder.AddSerilog();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<GrpcClientService>();
        services.AddSingleton<ServiceControllerWrapper>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception occurred");

        MessageBox.Show(
            $"An unhandled exception occurred: {e.Exception.Message}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
