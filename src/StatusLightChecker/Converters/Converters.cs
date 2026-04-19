using StatusLightChecker.Core.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StatusLightChecker.Converters;

public class StatusLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StatusLevel level)
        {
            var resources = App.Current.Resources;
            return level switch
            {
                StatusLevel.Available => resources["StatusAvailableBrush"] ?? Brushes.Green,
                StatusLevel.Busy => resources["StatusBusyBrush"] ?? Brushes.Red,
                StatusLevel.DoNotDisturb => resources["StatusDoNotDisturbBrush"] ?? Brushes.DarkRed,
                StatusLevel.Away => resources["StatusAwayBrush"] ?? Brushes.Orange,
                StatusLevel.Offline => resources["StatusOfflineBrush"] ?? Brushes.Gray,
                _ => resources["StatusUnknownBrush"] ?? Brushes.LightGray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusLevelToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StatusLevel level)
        {
            return level switch
            {
                StatusLevel.Available => "CheckCircle",
                StatusLevel.Busy => "Circle",
                StatusLevel.DoNotDisturb => "MinusCircle",
                StatusLevel.Away => "ClockOutline",
                StatusLevel.Offline => "CloseCircle",
                _ => "HelpCircle"
            };
        }
        return "HelpCircle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class HexToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                return (Color)ColorConverter.ConvertFromString(hex)!;
            }
        }
        catch { }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class ServiceStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status && parameter is string targetStatus)
        {
            var visibility = status.Equals(targetStatus, StringComparison.OrdinalIgnoreCase) 
                ? System.Windows.Visibility.Visible 
                : System.Windows.Visibility.Collapsed;
            return visibility;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}