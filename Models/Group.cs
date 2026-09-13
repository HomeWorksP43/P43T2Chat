using System;
using System.Collections.Generic;

namespace ChatProject.Models;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }

    public int OwnerId { get; set; }
    public User Owner { get; set; }

    public IList<GroupMember> Members { get; set; }
    public IList<Message> Messages { get; set; }
}