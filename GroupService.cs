using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatProject.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatProject;

public static class GroupService
{
    public static async Task<Group?> GetGroupAsync(int groupId)
    {
        using var context = new AppContext();
        return await context.Groups
            .Include(g => g.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
    }

    public static async Task<Group> CreateGroupAsync(string ownerUsername, string name, params string[] memberUsernames)
    {
        using var context = new AppContext();
        var owner = await context.Users.FirstOrDefaultAsync(u => u.Username == ownerUsername);
        if (owner is null) throw new InvalidOperationException($"User '{ownerUsername}' not found");

        var members = new List<User> { owner };
        if (memberUsernames.Length > 0)
        {
            members.AddRange(await context.Users
                .Where(u => memberUsernames.Contains(u.Username) && u.Id != owner.Id)
                .ToListAsync());
        }

        var group = new Group
        {
            Name = name,
            OwnerId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            Members = members
                .Select(m => new GroupMember { UserId = m.Id })
                .ToList(),
            Messages = new List<Message>()
        };

        context.Groups.Add(group);
        await context.SaveChangesAsync();
        return group;
    }

    public static async Task AddMembersAsync(int groupId, params string[] memberUsernames)
    {
        using var context = new AppContext();
        var group = await context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null) throw new InvalidOperationException($"Group '{groupId}' not found");

        var foundIds = await context.Users
            .Where(u => memberUsernames.Contains(u.Username))
            .Select(u => u.Id)
            .ToListAsync();

        var existingIds = group.Members.Select(m => m.UserId);
        foreach (var userId in foundIds.Where(id => !existingIds.Contains(id)))
            context.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId });

        await context.SaveChangesAsync();
    }

    public static async Task<IList<Group>> GetUserGroupsAsync(int userId)
    {
        using var context = new AppContext();
        return await context.Groups
            .Include(g => g.Members)
            .ThenInclude(m => m.User)
            .Where(g => g.OwnerId == userId || g.Members.Any(m => m.UserId == userId))
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public static async Task<IList<Message>> GetGroupHistoryAsync(int groupId)
    {
        using var context = new AppContext();
        return await context.Messages
            .Include(m => m.Sender)
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.SendAt)
            .ToListAsync();
    }

    public static async Task<Message> SendMessageAsync(ChatClient client, int groupId, int senderUserId, string text)
    {
        using var context = new AppContext();
        bool isMember = await context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == senderUserId);
        if (!isMember) throw new InvalidOperationException("User is not a member of this group");

        var message = new Message
        {
            SenderId = senderUserId,
            GroupId = groupId,
            Text = text,
            SendAt = DateTime.UtcNow
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();

        await client.SendGroupMessageAsync(groupId, text);
        return message;
    }
}