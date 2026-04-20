using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using StatusLightChecker.Core.Models;
using StatusLightChecker.Services;
using System.Windows.Media;

namespace StatusLightChecker.ViewModels;

public partial class ColorConfigurationViewModel : ObservableObject
{
    [ObservableProperty]
    private StatusColorViewModel _available = new();

    [ObservableProperty]
    private StatusColorViewModel _busy = new();

    [ObservableProperty]
    private StatusColorViewModel _doNotDisturb = new();

    [ObservableProperty]
    private StatusColorViewModel _away = new();

    [ObservableProperty]
    private StatusColorViewModel _offline = new();

    [ObservableProperty]
    private StatusColorViewModel _unknown = new();

    public ColorConfigurationViewModel()
    {
        _available = new StatusColorViewModel { Name = "Available", HexColor = "#00CC6A", StatusLevel = StatusLevel.Available };
        _busy = new StatusColorViewModel { Name = "Busy", HexColor = "#FF0000", StatusLevel = StatusLevel.Busy };
        _doNotDisturb = new StatusColorViewModel { Name = "Do Not Disturb", HexColor = "#B30000", StatusLevel = StatusLevel.DoNotDisturb, IsBlinking = true };
        _away = new StatusColorViewModel { Name = "Away", HexColor = "#FFCC00", StatusLevel = StatusLevel.Away };
        _offline = new StatusColorViewModel { Name = "Offline", HexColor = "#808080", StatusLevel = StatusLevel.Offline };
        _unknown = new StatusColorViewModel { Name = "Unknown", HexColor = "#CCCCCC", StatusLevel = StatusLevel.Unknown };
    }

    public ColorConfigurationViewModel(Contracts.ColorConfiguration config)
    {
        _available = new StatusColorViewModel(config.Available);
        _busy = new StatusColorViewModel(config.Busy);
        _doNotDisturb = new StatusColorViewModel(config.DoNotDisturb);
        _away = new StatusColorViewModel(config.Away);
        _offline = new StatusColorViewModel(config.Offline);
        _unknown = new StatusColorViewModel(config.Unknown);
    }

    public ColorConfiguration ToConfiguration()
    {
        return new ColorConfiguration
        {
            Available = Available.ToStatusColor(),
            Busy = Busy.ToStatusColor(),
            DoNotDisturb = DoNotDisturb.ToStatusColor(),
            Away = Away.ToStatusColor(),
            Offline = Offline.ToStatusColor(),
            Unknown = Unknown.ToStatusColor()
        };
    }
}

public partial class StatusColorViewModel : ObservableObject
{
    [ObservableProperty]
    private StatusLevel _statusLevel;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _hexColor = "#808080";

    [ObservableProperty]
    private bool _isBlinking;

    public Brush ColorBrush => (SolidColorBrush)new BrushConverter().ConvertFromString(HexColor)! ?? Brushes.Gray;

    public StatusColorViewModel() { }

    public StatusColorViewModel(Contracts.StatusColor color)
    {
        StatusLevel = (StatusLevel)color.StatusLevel;
        Name = color.Name;
        HexColor = color.HexColor;
        IsBlinking = color.IsBlinking;
    }

    public StatusColor ToStatusColor()
    {
        return new StatusColor
        {
            StatusLevel = StatusLevel,
            Name = Name,
            HexColor = HexColor,
            IsBlinking = IsBlinking
        };
    }
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly GrpcClientService _grpcClient;

    [ObservableProperty]
    private ColorConfigurationViewModel _configuration;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    public SettingsViewModel(GrpcClientService grpcClient, ColorConfigurationViewModel initialConfig)
    {
        _grpcClient = grpcClient;
        Configuration = initialConfig;
    }

    [RelayCommand]
    private async Task SaveConfigurationAsync()
    {
        try
        {
            IsSaving = true;
            StatusMessage = "Saving...";

            var config = Configuration.ToConfiguration();
            var success = await _grpcClient.UpdateColorConfigurationAsync(config);

            if (success)
            {
                StatusMessage = "Settings saved successfully!";
                // Apply the colors dynamically
                await App.Current.Dispatcher.InvokeAsync(() => ApplyColorsToTheme(config));
            }
            else
            {
                StatusMessage = "Failed to save settings.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        Configuration = new ColorConfigurationViewModel();
        StatusMessage = "Reset to defaults. Click Save to apply.";
    }

    [RelayCommand]
    private void CloseDialog()
    {
        // Close dialog via command
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    private static void ApplyColorsToTheme(ColorConfiguration config)
    {
        // Apply colors to application resources for dynamic theming
        var resources = App.Current.Resources;
        
        resources["StatusAvailableBrush"] = (SolidColorBrush)new BrushConverter().ConvertFromString(config.Available.HexColor)!;
        resources["StatusBusyBrush"] = (SolidColorBrush)new BrushConverter().ConvertFromString(config.Busy.HexColor)!;
        resources["StatusDoNotDisturbBrush"] = (SolidColorBrush)new BrushConverter().ConvertFromString(config.DoNotDisturb.HexColor)!;
        resources["StatusAwayBrush"] = (SolidColorBrush)new BrushConverter().ConvertFromString(config.Away.HexColor)!;
        resources["StatusOfflineBrush"] = (SolidColorBrush)new BrushConverter().ConvertFromString(config.Offline.HexColor)!;
        resources["StatusUnknownBrush"] = (SolidColorBrush)new BrushConverter().ConvertFromString(config.Unknown.HexColor)!;
    }
}
