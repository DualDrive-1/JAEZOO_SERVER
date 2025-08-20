using System.ComponentModel.DataAnnotations;

namespace JaeZoo.Server.Dtos;

// ===== Auth =====
public record RegisterRequest(
    [Required, MinLength(3)] string UserName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    [Required, MinLength(6)] string ConfirmPassword
);

public record LoginRequest(
    [Required] string LoginOrEmail,
    [Required] string Password
);

public record UserDto(Guid Id, string UserName, string Email, DateTime CreatedAt);
public record TokenResponse(string Token, UserDto User);

// ===== Users =====
public record UserSearchDto(Guid Id, string Name, string Email);

// ===== Friends =====
public enum FriendshipStatusDto { Pending = 0, Accepted = 1, Blocked = 2 }

public record FriendDto(
    Guid Id,
    string Name,
    string Email,
    FriendshipStatusDto Status
);

// ===== Chat =====
public record SendMessageRequest([Required] Guid RecipientId, [Required, MaxLength(4000)] string Text);
public record MessageDto(Guid SenderId, string Text, DateTime SentAt);
