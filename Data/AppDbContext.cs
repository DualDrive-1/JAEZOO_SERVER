using JaeZoo.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace JaeZoo.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<DirectDialog> DirectDialogs => Set<DirectDialog>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>().HasIndex(u => u.UserName).IsUnique();
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        b.Entity<Friendship>()
            .HasIndex(f => new { f.RequesterId, f.AddresseeId })
            .IsUnique();

        b.Entity<DirectDialog>()
            .HasIndex(d => new { d.User1Id, d.User2Id })
            .IsUnique();

        b.Entity<DirectMessage>()
            .HasIndex(m => new { m.DialogId, m.SentAt });
    }
}
