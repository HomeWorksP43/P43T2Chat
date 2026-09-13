using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatProject;

public class ChatClient : IDisposable
{
    private readonly TcpClient _tcp = new();

    private StreamReader? _reader;
    private StreamWriter? _writer;

    public string Username { get; private set; } = "";

    public event Action<string, string>? MessageReceived;
    public event Action<string, string, string>? GroupMessageReceived;

    public async Task ConnectAsync(string host, int port, string username)
    {
        Username = username;

        await _tcp.ConnectAsync(host, port);
        var stream = _tcp.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };

        await _writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "Auth",
            name = username
        }));

        _ = ReceiveLoopAsync();
    }

    public async Task SendMessageAsync(string to, string text) =>
        await _writer!.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "Message",
            from = Username,
            to,
            text
        }));

    public async Task SendGroupMessageAsync(int groupId, string text) =>
        await _writer!.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "GroupMessage",
            from = Username,
            groupId,
            text
        }));

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (await _reader!.ReadLineAsync() is { } line)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var type)) continue;

                var messageType = type.GetString();
                if (messageType == "Message" &&
                    root.TryGetProperty("from", out var from) &&
                    root.TryGetProperty("text", out var text))
                {
                    MessageReceived?.Invoke(from.GetString()!, text.GetString()!);
                }
                else if (messageType == "GroupMessage" &&
                         root.TryGetProperty("from", out from) &&
                         root.TryGetProperty("groupName", out var groupName) &&
                         root.TryGetProperty("text", out text))
                {
                    GroupMessageReceived?.Invoke(from.GetString()!, groupName.GetString()!, text.GetString()!);
                }
            }
        }
        catch
        {
          
        }
    }

    public void Dispose() => _tcp.Close();
}