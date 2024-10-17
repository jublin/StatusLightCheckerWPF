using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Expression.Interactivity.Core;
using System.Windows.Threading;
using TeamsStatusChecker.Configuration;
using TeamsStatusChecker.Services;
using Serilog;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using RJCP.IO.Ports;

namespace TeamsStatusChecker.ViewModels
{
    internal class MainViewModel : ObservableObject
    {
        public static MainViewModel Instance { get; set; } = new();

        private SerialPortStream? serialPort;

        private TeamsStatusAutomationService teamsStatusAutomationService;

        private DispatcherTimer timer;

        private void TimerTick(object? sender, EventArgs e)
        {
            teamsStatusAutomationService?.GetCurrentStatus();
        }

        public MainViewModel()
        {
            var cfg = new SourceWindowsAutomationConfiguration
            {
                Interval = 5,
                WindowName = "Microsoft Teams",
                StatusPattern = "Your profile, status @status for"
            };
            Log.Logger = new LoggerConfiguration()
                .WriteTo.RichTextBox(Application.Current.MainWindow.FindName("LogBox") as RichTextBox)
                .CreateLogger();

            Log.Information("Hello, world!");

            timer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Normal, TimerTick, App.Current.Dispatcher);
            teamsStatusAutomationService = new TeamsStatusAutomationService(cfg, Log.Logger);
            teamsStatusAutomationService.GetCurrentStatus();
        }

        private string comPort = string.Empty;
        public string ComPort
        {
            get => comPort;
            set => SetProperty(ref comPort, value);
        }

        private int baudRate = 115200;
        public int BaudRate
        {
            get => baudRate;
            set => SetProperty(ref baudRate, value);
        }

        private int checkInterval = 5;
        public int CheckInterval
        {
            get => checkInterval;
            set
            {               
                
                SetProperty(ref checkInterval, value);
                timer.Stop();
                timer.Interval = TimeSpan.FromSeconds(value);
                timer.Start();
            }
        }

        private bool connected;
        public bool Connected
        {
            get => connected;
            set
            {
                SetProperty(ref connected, value); 
                OnPropertyChanged(nameof(ComportButtonText));
                OnPropertyChanged(nameof(ComPortButtonCommand));
            }
        }

        public ActionCommand ComPortButtonCommand => Connected ? DisconnectCommand : ConnectCommand;

        private ActionCommand ConnectCommand => new ActionCommand(ConnectSerialPort);
        private ActionCommand DisconnectCommand => new ActionCommand(DisconnectSerialPort);

        private void DisconnectSerialPort()
        {
            serialPort?.Close();
            teamsStatusAutomationService.StatusChanged -= StatusChangedEvent;
        }

        private async void ConnectSerialPort()
        {
            serialPort = new SerialPortStream(ComPort, BaudRate);
            try
            {
                serialPort.Open();
            }
            catch (Exception ex)
            {
                Connected = false;
                return;
            }
            Connected = true;
            
            teamsStatusAutomationService.StatusChanged += StatusChangedEvent;
            await teamsStatusAutomationService.GetCurrentStatus();
            timer.Start();
        }

        private MicrosoftTeamsStatus lastStatus;

        public MicrosoftTeamsStatus LastStatus
        {
            get => lastStatus;
            set => SetProperty(ref lastStatus, value);
        }

        public string ComportButtonText => Connected ? "Disconnect" : "Connect";

        public SolidColorBrush StatusColor
        {
            get
            {
                switch (LastStatus)
                {
                    case MicrosoftTeamsStatus.Available:
                        return Brushes.Green;
                    case MicrosoftTeamsStatus.Busy:
                        return Brushes.Red;
                    case MicrosoftTeamsStatus.DoNotDisturb:
                        return Brushes.DarkRed;
                    case MicrosoftTeamsStatus.InAMeeting:
                        return Brushes.Red;
                    case MicrosoftTeamsStatus.Away:
                        return Brushes.Yellow;
                    case MicrosoftTeamsStatus.Offline:
                        return Brushes.Black;
                    case MicrosoftTeamsStatus.OutOfOffice:
                        return Brushes.Purple;
                    case MicrosoftTeamsStatus.Unknown:
                    default:
                        return Brushes.Gray;
                    
                }
            }
        }

        private async void StatusChangedEvent(object? sender, MicrosoftTeamsStatus e)
        {
            serialPort?.DiscardOutBuffer();
            if(e == LastStatus) return;
            LastStatus = e;
            switch (e)
            {
                case MicrosoftTeamsStatus.Available:
                    await serialPort?.WriteAsync(TeamsStatusColors.Available.ToArray(), 0, TeamsStatusColors.Available.Count);
                    break;
                case MicrosoftTeamsStatus.Busy:
                    var bytes = TeamsStatusColors.Busy.ToArray();
                    
                    await serialPort?.WriteAsync(bytes, 0, TeamsStatusColors.Busy.Count);
                    break;
                case MicrosoftTeamsStatus.DoNotDisturb:
                    await serialPort?.WriteAsync(TeamsStatusColors.Donotdisturb.ToArray(), 0, TeamsStatusColors.Donotdisturb.Count);
                    break;
                case MicrosoftTeamsStatus.InAMeeting:
                    await serialPort?.WriteAsync(TeamsStatusColors.Inameeting.ToArray(), 0, TeamsStatusColors.Inameeting.Count);
                    break;
                case MicrosoftTeamsStatus.Away:
                    await serialPort?.WriteAsync(TeamsStatusColors.Away.ToArray(), 0, TeamsStatusColors.Away.Count);
                    break;
                case MicrosoftTeamsStatus.Offline:
                    await serialPort?.WriteAsync(TeamsStatusColors.Offline.ToArray(), 0, TeamsStatusColors.Offline.Count);
                    break;
                case MicrosoftTeamsStatus.OutOfOffice:
                    await serialPort?.WriteAsync(TeamsStatusColors.OutOfOffice.ToArray(), 0, TeamsStatusColors.OutOfOffice.Count);
                    break;
                case MicrosoftTeamsStatus.Unknown:
                        await serialPort?.WriteAsync(TeamsStatusColors.Offline.ToArray(), 0, TeamsStatusColors.Offline.Count);
                        break;
                default:
                    break;
            }

            await serialPort?.FlushAsync();
        }
    }
    public static class TeamsStatusColors
    {
        public static List<byte> Available => [0, 255, 0];
        public static List<byte> Busy => [255, 0, 0];
        public static List<byte> Donotdisturb => [234, 26, 2];
        public static List<byte> Inameeting => [255, 0, 0];
        public static List<byte> Away => [194, 194, 4];
        public static List<byte> Offline => [0, 0, 0];
        public static List<byte> OutOfOffice => [174, 55, 255];

    }
}

