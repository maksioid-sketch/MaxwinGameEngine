using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Core.Editor;

public static class EditorIpc
{
    public const string ArgName = "--editor-ipc";
    public const string SelectionMessageType = "selection";
    private const string GameToEditorSuffix = ".g2e";
    private const string EditorToGameSuffix = ".e2g";

    public static string CreateSessionToken()
        => Guid.NewGuid().ToString("N");

    public static string GetPipeName(string token, EditorIpcChannel channel)
        => $"MaxwinEditor.{token}{(channel == EditorIpcChannel.GameToEditor ? GameToEditorSuffix : EditorToGameSuffix)}";

    public static bool TryGetTokenFromArgs(string[] args, out string token)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], ArgName, StringComparison.OrdinalIgnoreCase))
            {
                token = args[i + 1];
                return !string.IsNullOrWhiteSpace(token);
            }
        }

        token = string.Empty;
        return false;
    }
}

public enum EditorIpcChannel
{
    GameToEditor,
    EditorToGame
}

public sealed class EditorIpcMessage
{
    public string Type { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
}

public sealed class EditorIpcClient : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;
    private int _backoffMs;
    private DateTime _nextAttemptUtc = DateTime.MinValue;

    public EditorIpcClient(string token, EditorIpcChannel channel)
    {
        _pipeName = EditorIpc.GetPipeName(token, channel);
    }

    public void SendSelection(Guid? entityId)
    {
        if (!EnsureConnected())
            return;

        var msg = new EditorIpcMessage
        {
            Type = EditorIpc.SelectionMessageType,
            EntityId = entityId
        };

        try
        {
            var json = JsonSerializer.Serialize(msg);
            _writer!.WriteLine(json);
        }
        catch
        {
            Dispose();
            ScheduleReconnect();
        }
    }

    private bool EnsureConnected()
    {
        if (DateTime.UtcNow < _nextAttemptUtc)
            return false;

        if (_pipe is { IsConnected: true })
            return true;

        try
        {
            _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            _pipe.Connect(120);
            _writer = new StreamWriter(_pipe) { AutoFlush = true };
            _backoffMs = 0;
            _nextAttemptUtc = DateTime.MinValue;
            return true;
        }
        catch
        {
            Dispose();
            ScheduleReconnect();
            return false;
        }
    }

    private void ScheduleReconnect()
    {
        _backoffMs = _backoffMs == 0 ? 100 : System.Math.Min(_backoffMs * 2, 2000);
        _nextAttemptUtc = DateTime.UtcNow.AddMilliseconds(_backoffMs);
    }

    public void Dispose()
    {
        try { _writer?.Dispose(); } catch { }
        try { _pipe?.Dispose(); } catch { }
        _writer = null;
        _pipe = null;
    }
}

public sealed class EditorIpcServer : IDisposable
{
    private readonly string _pipeName;
    private readonly Action<EditorIpcMessage> _onMessage;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    public EditorIpcServer(string token, EditorIpcChannel channel, Action<EditorIpcMessage> onMessage)
    {
        _pipeName = EditorIpc.GetPipeName(token, channel);
        _onMessage = onMessage;
    }

    public void Start()
    {
        _loopTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                continue;
            }

            using var reader = new StreamReader(server);
            while (!ct.IsCancellationRequested && server.IsConnected)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch
                {
                    break;
                }

                if (line is null)
                    break;

                try
                {
                    var msg = JsonSerializer.Deserialize<EditorIpcMessage>(line);
                    if (msg is not null)
                        _onMessage(msg);
                }
                catch
                {
                    // ignore bad payloads
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loopTask?.Wait(200); } catch { }
        _cts.Dispose();
    }
}
