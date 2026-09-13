using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatProject;

public static class ChatServer
{
    public static async Task Run(int port)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        var users = new Dictionary<string, StreamWriter>();

        while (true)
        {
            var tcp = await listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(tcp, users);
        }
    }

    private static async Task HandleClientAsync(TcpClient tcp, Dictionary<string, StreamWriter> users)
    {
        var reader = new StreamReader(tcp.GetStream());
        var writer = new StreamWriter(tcp.GetStream()) { AutoFlush = true };

        try
        {
            var authLine = await reader.ReadLineAsync();
            if (authLine is null) return;

            using var auth = JsonDocument.Parse(authLine);
            var name = auth.RootElement.GetProperty("name").GetString()!;
            users[name] = writer;

            while (await reader.ReadLineAsync() is { } line)
            {
                using var msg = JsonDocument.Parse(line);
                if (!msg.RootElement.TryGetProperty("to", out var toProp)) continue;
                var to = toProp.GetString();
                if (to is not null && users.TryGetValue(to, out var dst))
                    await dst.WriteLineAsync(line);
            }
        }
        catch
        {
           
        }
        finally
        {
            tcp.Close();
        }
    }
}