using Serilog;
using StatusLightChecker.Core.Services;
using StatusLightChecker.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using StatusLightChecker.Core;

var builder = WebApplication.CreateBuilder(args);

// Resolve writable data directory. When running as a Windows Service, the process
// working directory is System32 (LocalSystem account) so relative paths fail.
// Environment.UserInteractive is false when hosted by the SCM.
var isService = !Environment.UserInteractive;
var dataDir = isService
    ? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Jublin", "StatusLightChecker", "Service")
    : AppContext.BaseDirectory;

Directory.CreateDirectory(Path.Combine(dataDir, "logs"));

// File sink path is set programmatically so it always resolves to a writable
// location. Console sink remains configured via appsettings.json.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.File(
        Path.Combine(dataDir, "logs", "service-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Configure as Windows Service
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = builder.Configuration["ServiceConfiguration:ServiceName"] ?? "StatusLightCheckerService";
});

// DB path resolved to ProgramData when running as a service; relative otherwise (dev).
var dbConnectionString = isService
    ? $"Data Source={Path.Combine(dataDir, "StatusLightChecker.db")}"
    : builder.Configuration.GetConnectionString("DefaultConnection")
      ?? builder.Configuration["Database:ConnectionString"]
      ?? "Data Source=StatusLightChecker.db";

// Register Core Services
builder.Services.AddSingleton<IColorConfigurationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ColorConfigurationService>>();
    return new ColorConfigurationService(logger, dbConnectionString);
});

builder.Services.AddSingleton<ServiceHealthTracker>();
builder.Services.AddSingleton<IServiceHealthTracker>(sp => sp.GetRequiredService<ServiceHealthTracker>());

// Register gRPC services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.MaxReceiveMessageSize = 1024 * 1024;
    options.MaxSendMessageSize = 1024 * 1024;
});

// Register Status Detectors
builder.Services.AddSingleton<IStatusDetector, TeamsStatusDetector>();

// Register Background Services
builder.Services.AddHostedService<StatusMonitoringService>();
builder.Services.AddHostedService<HealthHeartbeatService>();

var app = builder.Build();

// Set initial service state
var healthTracker = app.Services.GetRequiredService<ServiceHealthTracker>();
healthTracker.SetState(StatusLightChecker.Core.Services.ServiceState.Starting);

// Configure gRPC endpoints
app.MapGrpcService<GrpcStatusService>();
app.MapGrpcService<GrpcSettingsService>();

// Health check endpoint
app.MapGet("/health", () =>
{
    var health = healthTracker.GetCurrentHealth();
    return Results.Ok(new { health.State, health.Version, health.UptimeSeconds });
});

// Configure shutdown
app.Lifetime.ApplicationStarted.Register(() =>
{
    healthTracker.SetState(StatusLightChecker.Core.Services.ServiceState.Running);
    Log.Information("Status Light Checker Service started successfully");
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    healthTracker.SetState(StatusLightChecker.Core.Services.ServiceState.Stopping);
    Log.Information("Status Light Checker Service is stopping...");
});

app.Lifetime.ApplicationStopped.Register(() =>
{
    healthTracker.SetState(StatusLightChecker.Core.Services.ServiceState.Stopped);
    Log.Information("Status Light Checker Service stopped");
    Log.CloseAndFlush();
});

await app.RunAsync();
