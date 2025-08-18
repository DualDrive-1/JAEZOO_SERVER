using System.ComponentModel.DataAnnotations;

namespace JaeZoo.Server.Models;

public enum FriendshipStatus { Pending = 0, Accepted = 1, Blocked = 2 }

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public Guid RequesterId { get; set; }
    [Required] public Guid AddresseeId { get; set; }
    [Required] public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
