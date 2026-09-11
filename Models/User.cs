using System.Collections.Generic;

namespace ChatProject.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    
    public IList<Contact> OwnContacts { get; set; }
    public IList<Message> Messages { get; set; }
}