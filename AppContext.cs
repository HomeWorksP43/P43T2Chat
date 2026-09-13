using System.IO;
using ChatProject.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChatProject;

public class AppContext:DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        optionsBuilder.UseNpgsql(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(u => u.Messages) 
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Group)
            .WithMany(g => g.Messages)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Contact>()
            .HasOne(c => c.OwnerUser)
            .WithMany()
            .HasForeignKey(c => c.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contact>()
            .HasOne(c => c.ContactUser)
            .WithMany()
            .HasForeignKey(c => c.ContactUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Group>()
            .HasOne(g => g.Owner)
            .WithMany()
            .HasForeignKey(g => g.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupMember>()
            .HasKey(gm => new { gm.GroupId, gm.UserId });

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.User)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}