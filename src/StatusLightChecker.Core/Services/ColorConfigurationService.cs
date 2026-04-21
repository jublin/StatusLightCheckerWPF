using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StatusLightChecker.Core.Models;
using System.Text.Json;

namespace StatusLightChecker.Core.Services;

public class ColorConfigurationService : IColorConfigurationService
{
    private readonly ILogger<ColorConfigurationService> _logger;
    private readonly string _connectionString;
    private ColorConfiguration _currentConfig;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public event EventHandler<ColorConfigurationChangedEventArgs>? ConfigurationChanged;

    public ColorConfigurationService(ILogger<ColorConfigurationService> logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
        _currentConfig = new ColorConfiguration();
        
        InitializeDatabaseAsync().Wait();
        LoadConfigurationAsync().Wait();
    }

    private async Task InitializeDatabaseAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS ColorConfiguration (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                ConfigurationJson TEXT NOT NULL,
                LastUpdated INTEGER NOT NULL
            );
            
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                LastUpdated INTEGER NOT NULL
            );
        ";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
        _logger.LogDebug("Database initialized successfully");
    }

    private async Task LoadConfigurationAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqliteCommand(
                "SELECT ConfigurationJson FROM ColorConfiguration WHERE Id = 1", connection);
            
            var result = await command.ExecuteScalarAsync();
            if (result is string json)
            {
                var config = JsonSerializer.Deserialize<ColorConfiguration>(json);
                if (config != null)
                {
                    _currentConfig = config;
                    _logger.LogInformation("Loaded color configuration from database");
                }
            }
            else
            {
                await SaveConfigurationInternalAsync(_currentConfig);
                _logger.LogInformation("Saved default color configuration to database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading color configuration");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public ColorConfiguration GetCurrentConfiguration()
    {
        return _currentConfig;
    }

    public async Task UpdateConfigurationAsync(ColorConfiguration configuration)
    {
        await _semaphore.WaitAsync();
        try
        {
            await SaveConfigurationInternalAsync(configuration);
            _currentConfig = configuration;
            
            ConfigurationChanged?.Invoke(this, new ColorConfigurationChangedEventArgs(configuration));
            _logger.LogInformation("Color configuration updated");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveConfigurationInternalAsync(ColorConfiguration configuration)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var json = JsonSerializer.Serialize(configuration);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        using var command = new SqliteCommand(@"
            INSERT OR REPLACE INTO ColorConfiguration (Id, ConfigurationJson, LastUpdated)
            VALUES (1, @json, @timestamp)
        ", connection);

        command.Parameters.AddWithValue("@json", json);
        command.Parameters.AddWithValue("@timestamp", timestamp);

        await command.ExecuteNonQueryAsync();
    }
}

public class SerialPortConfigurationService : ISerialPortConfigurationService
{
    private const string SettingsKey = "SerialPort";

    private readonly ILogger<SerialPortConfigurationService> _logger;
    private readonly string _connectionString;
    private SerialPortConfiguration _currentConfig;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SerialPortConfigurationService(
        ILogger<SerialPortConfigurationService> logger,
        string connectionString,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = connectionString;

        var comPort = configuration["SerialPort:ComPort"] ?? "COM3";
        var baudRate = int.TryParse(configuration["SerialPort:BaudRate"], out var br) ? br : 115200;
        _currentConfig = new SerialPortConfiguration { ComPort = comPort, BaudRate = baudRate };

        EnsureTableAsync().Wait();
        LoadConfigurationAsync().Wait();
    }

    private async Task EnsureTableAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqliteCommand(@"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                LastUpdated INTEGER NOT NULL
            );", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task LoadConfigurationAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqliteCommand(
                "SELECT Value FROM Settings WHERE Key = @key", connection);
            command.Parameters.AddWithValue("@key", SettingsKey);

            var result = await command.ExecuteScalarAsync();
            if (result is string json)
            {
                var config = JsonSerializer.Deserialize<SerialPortConfiguration>(json);
                if (config != null)
                {
                    _currentConfig = config;
                    _logger.LogInformation("Loaded serial port configuration from database");
                }
            }
            else
            {
                await SaveInternalAsync(_currentConfig);
                _logger.LogInformation("Saved default serial port configuration to database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading serial port configuration");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public SerialPortConfiguration GetCurrentConfiguration() => _currentConfig;

    public async Task UpdateConfigurationAsync(SerialPortConfiguration configuration)
    {
        await _semaphore.WaitAsync();
        try
        {
            await SaveInternalAsync(configuration);
            _currentConfig = configuration;
            _logger.LogInformation("Serial port configuration updated: {Port} @ {Baud}",
                configuration.ComPort, configuration.BaudRate);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveInternalAsync(SerialPortConfiguration configuration)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var json = JsonSerializer.Serialize(configuration);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        using var command = new SqliteCommand(@"
            INSERT OR REPLACE INTO Settings (Key, Value, LastUpdated)
            VALUES (@key, @value, @timestamp)", connection);
        command.Parameters.AddWithValue("@key", SettingsKey);
        command.Parameters.AddWithValue("@value", json);
        command.Parameters.AddWithValue("@timestamp", timestamp);

        await command.ExecuteNonQueryAsync();
    }
}
