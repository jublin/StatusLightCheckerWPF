# Legacy tokens
WPF MaterialDesign Dark theme, Indigo primary, DeepPurple secondary. Statuses #00CC6A, #FF0000, #B30000, #FFCC00, #808080, #CCCCCC. User dislikes legacy UI; new companion uses the explicitly separate design-system.md. No CSS/Tailwind exists.

```xml
<Application x:Class="StatusLightChecker.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:converters="clr-namespace:StatusLightChecker.Converters"
             xmlns:vm="clr-namespace:StatusLightChecker.ViewModels">
    
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- Material Design Themes -->
                <materialDesign:BundledTheme BaseTheme="Dark" PrimaryColor="Indigo" SecondaryColor="DeepPurple" />
                <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/themes/materialdesign3.defaults.xaml"/>
                
                <!-- Custom Status Colors (Dynamic Resources) -->
                <ResourceDictionary>
                    <!-- Status Indicator Brushes -->
                    <SolidColorBrush x:Key="StatusAvailableBrush" Color="#00CC6A" />
                    <SolidColorBrush x:Key="StatusBusyBrush" Color="#FF0000" />
                    <SolidColorBrush x:Key="StatusDoNotDisturbBrush" Color="#B30000" />
                    <SolidColorBrush x:Key="StatusAwayBrush" Color="#FFCC00" />
                    <SolidColorBrush x:Key="StatusOfflineBrush" Color="#808080" />
                    <SolidColorBrush x:Key="StatusUnknownBrush" Color="#CCCCCC" />
                    
                    <!-- Converters -->
                    <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
                    <converters:StatusLevelToBrushConverter x:Key="StatusLevelToBrushConverter" />
                    <converters:StatusLevelToIconConverter x:Key="StatusLevelToIconConverter" />
                    <converters:HexToColorConverter x:Key="HexToColorConverter" />
                    <converters:InverseBooleanConverter x:Key="InverseBooleanConverter" />
                    <converters:ServiceStatusToVisibilityConverter x:Key="ServiceStatusToVisibilityConverter" />
                    
                </ResourceDictionary>
                
            </ResourceDictionary.MergedDictionaries>
            
        </ResourceDictionary>
    </Application.Resources>
    
</Application>
```
