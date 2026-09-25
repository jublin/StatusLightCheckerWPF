using System.IO.Ports;

namespace StatusLightChecker.Companion;

/// <summary>One request/reply at a time, using the firmware's explicit command mode.</summary>
public sealed class LightConnection : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private SerialPort? port;
    private bool disposed;
    public bool IsConnected => port?.IsOpen == true;

    public async Task<LightSettings> ConnectAsync(
        string portName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Disconnect();
            port = new SerialPort(portName, 57600, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = false,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "\n",
            };
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    port.Open();
                    // Clear a previous connection's abandoned line and its optional error reply.
                    port.WriteLine("");
                    Thread.Sleep(200);
                    port.DiscardInBuffer();
                    return LightSettings.Parse(Request("GET", cancellationToken));
                },
                cancellationToken
            );
        }
        catch
        {
            Disconnect();
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Execute a batch in order. SAVE is sent only when the caller explicitly includes it.</summary>
    public async Task<string[]> ExecuteAsync(
        IEnumerable<string> commands,
        CancellationToken cancellationToken = default
    )
    {
        string[] reply;
        var batch = commands.ToArray();
        if (
            batch.Any(command =>
                string.IsNullOrWhiteSpace(command)
                || command.Length > 95
                || command.Any(c => c < 32 || c > 126)
            )
        )
        {
            throw new ArgumentException("Invalid serial command.");
        }
        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (port?.IsOpen != true)
            {
                throw new InvalidOperationException("Connect a light first.");
            }

            return await Task.Run(
                () =>
                {
                    reply = [];
                    foreach (var command in batch)
                        reply = Request(command, cancellationToken);
                    return reply;
                },
                cancellationToken
            );
        }
        catch
        {
            Disconnect();
            return [];
        }
        finally
        {
            gate.Release();
        }
    }

    private string[] Request(string command, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            port!.WriteLine(command);
            return ReadReply(port.ReadByte, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            Disconnect();
            return [];
        }
    }

    public static string[] ReadReply(
        Func<int> readByte,
        CancellationToken cancellationToken = default
    )
    {
        var response = new List<string>();
        // Bounded lines and total bytes: a wrong device cannot make us allocate forever.
        var line = new System.Text.StringBuilder();
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        for (var bytes = 0; bytes < 2048; ++bytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.ElapsedMilliseconds >= 3000)
                throw new TimeoutException("The light did not finish its reply.");
            var value = readByte();
            if (value == '\r')
                continue;
            if (value == '\n')
            {
                var text = line.ToString();
                line.Clear();
                if (text == "OK")
                    return response.ToArray();
                if (text.StartsWith("ERR ", StringComparison.Ordinal))
                    throw new IOException($"Light reported: {text}");
                if (text.Length > 0)
                    response.Add(text);
                if (response.Count > 20)
                    break;
            }
            else
            {
                if (value is < 32 or > 126 || line.Length >= 255)
                    break;
                line.Append((char)value);
            }
        }
        throw new InvalidDataException("The light returned a malformed reply.");
    }

    private void Disconnect()
    {
        var oldPort = port;
        port = null;
        oldPort?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            disposed = true;
            Disconnect();
        }
        finally
        {
            gate.Release();
        }
    }
}
