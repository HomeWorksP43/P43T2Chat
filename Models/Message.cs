using System;

namespace ChatProject.Models;

public class Message
{
    public int Id { get; set; } 
    public int SenderId { get; set; }
    public User Sender { get; set; }
    public string Text {get;set;}
    public DateTime SendAt { get; set; }
    public int ReceiverId { get; set; }
    public User Receiver { get; set; }
}