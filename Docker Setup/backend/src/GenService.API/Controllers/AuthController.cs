using GenService.API.Data;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    GenServiceDbContext db,
    IConfiguration config,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Authenticate and receive a JWT.
    /// In DevJwt mode users are validated against the AppUsers table (BCrypt passwords).
    /// In production this endpoint is replaced by Microsoft Entra ID SSO.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var authMode = config["Auth:Mode"];

        if (authMode != "DevJwt")
        {
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { message = "Local login only available in DevJwt mode. Use Microsoft Entra ID in production." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { message = "Email and password are required." });

        var email = request.Email.Trim().ToLowerInvariant();

        // Primary path — look up user in DB
        var user = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null)
        {
            logger.LogWarning("Login attempt for unknown user: {Email}", email);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Login attempt for deactivated user: {Email}", email);
            return Unauthorized(new { message = "This account has been deactivated. Please contact your administrator." });
        }

        bool passwordOk;
        try
        {
            passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            passwordOk = false;
        }

        if (!passwordOk)
        {
            logger.LogWarning("Invalid password for user: {Email}", email);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Update last-login timestamp
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var token = GenerateJwt(user.Email, user.FullName, user.Role, expiresAt);

        logger.LogInformation("User {Email} ({Role}) logged in", user.Email, user.Role);

        return Ok(new LoginResponse(
            Token: token,
            User: new UserInfo(
                Email:      user.Email,
                FullName:   user.FullName,
                Role:       user.Role,
                Department: user.Department,
                ExpiresAt:  expiresAt
            )
        ));
    }

    /// <summary>Returns the currently authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var email    = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var fullName = User.FindFirstValue(ClaimTypes.Name)  ?? "";
        var role     = User.FindFirstValue(ClaimTypes.Role)  ?? "";

        var expiryClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        var expiresAt   = expiryClaim != null
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiryClaim)).UtcDateTime
            : DateTime.UtcNow.AddHours(8);

        // Pull live department from DB (in case it was updated since token was issued)
        var user = await db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

        return Ok(new UserInfo(
            Email:      email,
            FullName:   fullName,
            Role:       role,
            Department: user?.Department ?? "General Service",
            ExpiresAt:  expiresAt
        ));
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private string GenerateJwt(string email, string fullName, string role, DateTime expiresAt)
    {
        var secret      = config["Auth:DevJwt:Secret"]   ?? "GenServiceDevJwtSecret2026LocalKey32chars!";
        var issuer      = config["Auth:DevJwt:Issuer"]   ?? "genservice-local";
        var audience    = config["Auth:DevJwt:Audience"] ?? "genservice-web";
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email,  email),
            new Claim(ClaimTypes.Name,   fullName),
            new Claim(ClaimTypes.Role,   role),
        };

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
