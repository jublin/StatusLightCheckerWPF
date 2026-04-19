using System.IO.Ports;
using StatusLightChecker.Core.Colors;

namespace StatusLightChecker.Core.Serial;

public class SerialPortManager : IDisposable
{
    private SerialPort? _serialPort;
    private bool _disposed;

    public bool IsOpen => _serialPort?.IsOpen ?? false;

    public async Task<bool> Open(string portName, int baudRate, CancellationToken token)
    {
        try
        {
            if (_serialPort?.IsOpen == true)
                return true;

            _serialPort?.Dispose();

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            await Task.Run(() => _serialPort.Open(), token);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open serial port {portName}: {ex.Message}");
            return false;
        }
    }

    public async Task CloseAsync()
    {
        if (_serialPort?.IsOpen == true)
        {
            await Task.Run(() =>
            {
                try
                {
                    _serialPort?.Close();
                }
                catch { }
            });
        }
    }

    public void WriteColor(StatusColors colors)
    {
        if (_serialPort?.IsOpen != true) return;

        try
        {
            _serialPort.DiscardOutBuffer();
            var data = new[] { colors.Unknown.R, colors.Unknown.G, colors.Unknown.B };
            _serialPort.Write(data, 0, data.Length);
            // _serialPort.DrainWriter();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to write to serial port: {ex.Message}");
        }
    }

    public void WriteColorRaw(byte r, byte g, byte b)
    {
        if (_serialPort?.IsOpen != true) return;

        try
        {
            _serialPort.DiscardOutBuffer();
            var data = new[] { r, g, b };
            _serialPort.Write(data, 0, data.Length);
            // _serialPort.DrainWriter();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to write to serial port: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _serialPort?.Close();
            _serialPort?.Dispose();
        }
        catch { }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~SerialPortManager() => Dispose();
}
