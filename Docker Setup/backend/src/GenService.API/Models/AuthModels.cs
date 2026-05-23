namespace GenService.API.Models;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, UserInfo User);

public record UserInfo(
    string Email,
    string FullName,
    string Role,
    string Department,
    DateTime ExpiresAt
);
