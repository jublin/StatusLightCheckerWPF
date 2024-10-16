using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Expression.Interactivity.Core;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Threading;
using TeamsStatusChecker.Configuration;
using TeamsStatusChecker.Services;
using Serilog;
using System.Windows.Controls;
using System.Windows;
using System.Data;

namespace TeamsStatusChecker.ViewModels
{
    internal class MainViewModel : ObservableObject
    {
        public static MainViewModel Instance { get; set; } = new();

        private SerialPort? serialPort;

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
                StatusPattern = "Your profile, status @status"
            };
            Log.Logger = new LoggerConfiguration()
                .WriteTo.RichTextBox(Application.Current.MainWindow.FindName("LogBox") as RichTextBox)
                .CreateLogger();

            Log.Information("Hello, world!");

            timer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Normal, TimerTick, App.Current.Dispatcher);
            teamsStatusAutomationService = new TeamsStatusAutomationService(cfg, Log.Logger);
        }

        private string comPort = string.Empty;
        public string ComPort
        {
            get => comPort;
            set => SetProperty(ref comPort, value);
        }

        private int baudRate;
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
            set => SetProperty(ref connected, value);
        }

        public ActionCommand ConnectCommand => new ActionCommand(() => ConnectSerialPort());
        public ActionCommand DisconnectCommand => new ActionCommand(() => DisconnectSerialPort());

        private void DisconnectSerialPort()
        {
            serialPort?.Close();
            teamsStatusAutomationService.StatusChanged -= StatusChangedEvent;
        }

        private void ConnectSerialPort()
        {
            serialPort = new SerialPort(ComPort, BaudRate);
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
            teamsStatusAutomationService.GetCurrentStatus();
            timer.Start();
        }

        private void StatusChangedEvent(object? sender, MicrosoftTeamsStatus e)
        {
            switch (e)
            {
                case MicrosoftTeamsStatus.Available:
                    serialPort?.Write(TeamsStatusColors.Available.ToArray(), 0, TeamsStatusColors.Available.Count);
                    break;
                case MicrosoftTeamsStatus.Busy:
                    serialPort?.Write(TeamsStatusColors.Busy.ToArray(), 0, TeamsStatusColors.Busy.Count);
                    break;
                case MicrosoftTeamsStatus.DoNotDisturb:
                    serialPort?.Write(TeamsStatusColors.Donotdisturb.ToArray(), 0, TeamsStatusColors.Donotdisturb.Count);
                    break;
                case MicrosoftTeamsStatus.InAMeeting:
                    serialPort?.Write(TeamsStatusColors.Inameeting.ToArray(), 0, TeamsStatusColors.Inameeting.Count);
                    break;
                case MicrosoftTeamsStatus.Away:
                    serialPort?.Write(TeamsStatusColors.Away.ToArray(), 0, TeamsStatusColors.Away.Count);
                    break;
                case MicrosoftTeamsStatus.Offline:
                    serialPort?.Write(TeamsStatusColors.Offline.ToArray(), 0, TeamsStatusColors.Offline.Count);
                    break;
                case MicrosoftTeamsStatus.OutOfOffice:
                    serialPort?.Write(TeamsStatusColors.OutOfOffice.ToArray(), 0, TeamsStatusColors.OutOfOffice.Count);
                    break;
                default:
                    break;
            }
        }
    }
    public static class TeamsStatusColors
    {
        public static List<byte> Available => [0, 255, 0];
        public static List<byte> Busy => [255, 0, 0];
        public static List<byte> Donotdisturb => [255, 2, 2];
        public static List<byte> Inameeting => [255, 0, 0];
        public static List<byte> Away => [194, 194, 4];
        public static List<byte> Offline => [0, 0, 0];
        public static List<byte> OutOfOffice => [174, 55, 255];

    }
}

