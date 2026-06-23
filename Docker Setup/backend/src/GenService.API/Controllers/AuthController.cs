using GenService.API.Data;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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

    /// <summary>
    /// Exchange a Microsoft Entra ID token for a platform JWT.
    /// The frontend calls this after the user completes MSAL loginPopup.
    /// </summary>
    [HttpPost("entra-login")]
    [AllowAnonymous]
    public async Task<IActionResult> EntraLogin([FromBody] EntraLoginRequest request)
    {
        var tenantId = config["Auth:Entra:TenantId"];
        var clientId = config["Auth:Entra:ClientId"];

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { message = "Microsoft SSO is not configured on this server." });

        // Validate the Microsoft ID token against Entra's public keys
        ClaimsPrincipal principal;
        try
        {
            var metadataUri = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
            var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataUri,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var openIdConfig = await configManager.GetConfigurationAsync(CancellationToken.None);

            var validationParams = new TokenValidationParameters
            {
                ValidIssuer       = $"https://login.microsoftonline.com/{tenantId}/v2.0",
                ValidAudience     = clientId,
                IssuerSigningKeys = openIdConfig.SigningKeys,
                ValidateLifetime  = true,
                ValidateIssuer    = true,
                ValidateAudience  = true,
                ClockSkew         = TimeSpan.FromMinutes(5)
            };

            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(request.IdToken, validationParams, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Entra ID token validation failed: {Message}", ex.Message);
            return Unauthorized(new { message = "Invalid Microsoft token. Please sign in again." });
        }

        // Extract UPN / email (Entra uses 'preferred_username' for work accounts)
        var email = principal.FindFirst("preferred_username")?.Value
                 ?? principal.FindFirst("email")?.Value
                 ?? principal.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(email))
            return Unauthorized(new { message = "Could not read email from your Microsoft account." });

        email = email.Trim().ToLowerInvariant();

        // Match to a platform user
        var user = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive);

        if (user is null)
        {
            logger.LogWarning("Entra login — no platform account for {Email}", email);
            return Unauthorized(new { message = $"No platform account found for {email}. Ask your administrator to create one." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var token     = GenerateJwt(user.Email, user.FullName, user.Role, expiresAt);

        logger.LogInformation("Entra login OK: {Email} ({Role})", email, user.Role);

        return Ok(new LoginResponse(
            Token: token,
            User:  new UserInfo(
                Email:      user.Email,
                FullName:   user.FullName,
                Role:       user.Role,
                Department: user.Department,
                ExpiresAt:  expiresAt
            )
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
