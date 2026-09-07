# Legacy main window
- src/StatusLightChecker/MainWindow.xaml
  - App.xaml (MaterialDesign resources and converters)
  - ViewModels/MainViewModel.cs (status and service operations)
    - Services/GrpcClientService.cs
    - Services/ServiceControllerWrapper.cs
    - Core/Models/StatusModels.cs
  - Converters/Converters.cs
  - Views/SettingsDialog.xaml
    - ViewModels/SettingsViewModel.cs
  - Views/ErrorDialog.xaml
# New companion
No rendered page yet. Reuse status meanings and device capabilities, not old WPF layout. Device protocol lives in src/StatusLightChecker.Companion/LightSettings.cs and LightConnection.cs.
