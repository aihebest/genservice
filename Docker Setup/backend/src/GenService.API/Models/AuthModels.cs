namespace GenService.API.Models;

public record LoginRequest(string Email, string Password);

/// <summary>Payload sent by the frontend after MSAL loginPopup completes.</summary>
public record EntraLoginRequest(string IdToken);

public record LoginResponse(string Token, UserInfo User);

public record UserInfo(
    string Email,
    string FullName,
    string Role,
    string Department,
    DateTime ExpiresAt
);
