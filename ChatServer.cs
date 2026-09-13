using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using ChatProject.Models;
using Microsoft.EntityFrameworkCore;

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

    private static async Task HandleGroupMessageAsync(JsonElement root, Dictionary<string, StreamWriter> users)
    {
        if (!root.TryGetProperty("groupId", out var groupIdProp) ||
            !root.TryGetProperty("from", out var fromProp) ||
            !root.TryGetProperty("text", out var textProp))
            return;

        var groupId = groupIdProp.GetInt32();
        var from = fromProp.GetString();
        var text = textProp.GetString();
        if (from is null || text is null) return;

        using var context = new AppContext();
        var group = await context.Groups
            .Include(g => g.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null) return;

        var forward = JsonSerializer.Serialize(new
        {
            type = "GroupMessage",
            from,
            groupId,
            groupName = group.Name,
            text
        });

        foreach (var member in group.Members)
        {
            if (member.User.Username == from) continue;
            if (users.TryGetValue(member.User.Username, out var dst))
                await dst.WriteLineAsync(forward);
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
                var root = msg.RootElement;

                string messageType = root.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString() ?? ""
                    : "";

                if (messageType == "GroupMessage")
                {
                    await HandleGroupMessageAsync(root, users);
                    continue;
                }

                if (!root.TryGetProperty("to", out var toProp)) continue;
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