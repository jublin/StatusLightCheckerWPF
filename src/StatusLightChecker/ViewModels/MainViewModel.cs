﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Xaml.Behaviors.Core;
using RJCP.IO.Ports;
using Serilog;
using StatusLightChecker.Enumerations;
using StatusLightChecker.StatusCheckers;

namespace StatusLightChecker.ViewModels;

internal class MainViewModel : ObservableObject
{
    public static MainViewModel Instance { get; } = new();

    public SerialPortStream? SerialPort { get; private set; }

    private TeamsApplicationStatusChecker? teamsApplicationStatusChecker;
    
    private readonly StatusChangedEventHandler statusChangedEventHandler;

    private MainViewModel()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.RichTextBox(Application.Current.MainWindow?.FindName("LogBox") as RichTextBox)
            .CreateLogger();
        Log.Information("Hello, world!");
        statusChangedEventHandler = StatusChangedEvent;
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
        
    private ApplicationCheck selectedApp = ApplicationCheck.MicrosoftTeams;

    public ApplicationCheck SelectedApp
    {
        get => selectedApp;
        set
        {
            SetProperty(ref selectedApp, value);
            SetupChecker();
            GetChecker()?.GetCurrentStatus();
        }
    }
    
    private IStatusChecker? GetChecker()
    {
        return SelectedApp switch
        {
            ApplicationCheck.MicrosoftTeams => teamsApplicationStatusChecker,
            // ApplicationCheck.Slack => slackStatusChecker,
            _ => null
        };
    }

    private void SetupChecker()
    {
        var currChecker = GetChecker();
        currChecker?.Dispose();
        switch (SelectedApp)
        {
            case ApplicationCheck.MicrosoftTeams:
                teamsApplicationStatusChecker = new TeamsApplicationStatusChecker(Log.Logger, statusChangedEventHandler);
                break;
            // case ApplicationCheck.Slack:
            //     slackStatusChecker = new SlackStatusChecker(Log.Logger, statusChangedEventHandler);
                // break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private bool connected;

    private bool Connected
    {
        get => connected;
        set
        {
            SetProperty(ref connected, value); 
            OnPropertyChanged(nameof(ComportButtonText));
            OnPropertyChanged(nameof(ComPortButtonCommand));
        }
    }

    private bool showLog;

    public bool ShowLog
    {
        get => showLog;
        set
        {
            SetProperty(ref showLog, value);
            OnPropertyChanged(nameof(ShowLogText));
        }
    }

    public string ShowLogText => ShowLog ? "Hide Log" : "Show Log";
 
    
    public ActionCommand ComPortButtonCommand => Connected ? DisconnectCommand : ConnectCommand;

    private ActionCommand ConnectCommand => new(ConnectSerialPort);
    private ActionCommand DisconnectCommand => new(DisconnectSerialPort);

    private void DisconnectSerialPort()
    {
        GetChecker()?.StopChecking();
        SerialPort?.Close();
    }

    private void ConnectSerialPort()
    {
        if (GetChecker() == null)
        {
            SetupChecker();
        }
        SerialPort = new SerialPortStream(ComPort, BaudRate);
        try
        {
            SerialPort.Open();
        }
        catch (Exception)
        {
            Connected = false;
            return;
        }
        Connected = true;
        GetChecker()?.StartChecking();
    }

    public string ComportButtonText => Connected ? "Disconnect" : "Connect";

    public SolidColorBrush? StatusColor => GetColorFromSelectedChecker();

    private SolidColorBrush? GetColorFromSelectedChecker()
    {
        var checker = GetChecker();
        return checker == null ? new SolidColorBrush(Colors.Black) : checker?.GetColorFromStatus();
    }
    
    private async void StatusChangedEvent()
    {
        OnPropertyChanged(nameof(StatusColor));
        SerialPort?.DiscardOutBuffer();
        byte[] colorBytes = StatusColor == null ? [0, 0, 0] : [StatusColor.Color.R, StatusColor.Color.G, StatusColor.Color.B];
        await SerialPort?.WriteAsync(colorBytes, 0, colorBytes.Length)!;
        await SerialPort?.FlushAsync()!;
    }
}