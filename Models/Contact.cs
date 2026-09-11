namespace ChatProject.Models;

public class Contact
{
    public int Id { get; set; }
    public User ContactUser { get; set; }
    public int ContactUserId { get; set; }
    public int OwnerUserId { get; set; }
    public User OwnerUser { get; set; }
    public string DisplayName { get; set; }
    
}