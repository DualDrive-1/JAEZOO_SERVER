namespace JaeZoo.Server.Dtos;

public record RegisterRequest(string UserName, string Email, string Password, string ConfirmPassword);
public record LoginRequest(string LoginOrEmail, string Password);

public record UserDto(string Id, string UserName, string Email, DateTime CreatedAt);
public record TokenResponse(string Token, UserDto User);

public record UserSearchDto(string Id, string UserName, string Email);
public record FriendDto(string Id, string UserName, string Email);

public record MessageDto(string SenderId, string Text, DateTime SentAt);

// заявки в друзья
public record FriendRequestDto(string FromUserId, string FromUserName, string FromEmail, DateTime CreatedAt);
