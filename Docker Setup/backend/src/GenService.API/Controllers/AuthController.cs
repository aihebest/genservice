using GenService.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IConfiguration config, ILogger<AuthController> logger) : ControllerBase
{
    // ── Dev users (active only when Auth:Mode = DevJwt) ──────────────────────
    // Format: email → (password, fullName, role, department)
    private static readonly Dictionary<string, (string Password, string FullName, string Role, string Department)> DevUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["manager@demo.local"]     = ("DemoManager2026!", "Bobby Tholath",      "DepartmentManager", "General Service"),
        ["supervisor@demo.local"]  = ("DemoSuper2026!",   "Emeka Okonkwo",      "Supervisor",        "General Service"),
        ["technician@demo.local"]  = ("DemoTech2026!",    "Chukwudi Nwosu",     "Technician",        "General Service"),
        ["driver@demo.local"]      = ("DemoDriver2026!",  "Bola Adeyemi",       "Driver",            "General Service"),
        ["requester1@demo.local"]  = ("DemoReq2026!",     "Fatima Al-Hassan",   "Requester",         "Finance"),
        ["requester2@demo.local"]  = ("DemoReq2026!",     "Tunde Babatunde",    "Requester",         "HR"),
        ["tech2@demo.local"]       = ("DemoTech2026!",    "Grace Obi",          "Technician",        "General Service"),
        ["driver2@demo.local"]     = ("DemoDriver2026!",  "Kwame Asante",       "Driver",            "General Service"),
        // Development shorthand — any @dev.local email with password Dev2026!
        ["admin@dev.local"]        = ("Dev2026!",         "Dev Administrator",  "SystemAdmin",       "IT"),
    };

    /// <summary>
    /// Authenticate and receive a JWT token.
    /// DevJwt mode only — in production this is replaced by Microsoft Entra ID SSO.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var authMode = config["Auth:Mode"];

        if (authMode != "DevJwt")
        {
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { message = "Local login only available in DevJwt mode. Use Microsoft Entra ID in production." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { message = "Email and password are required." });

        if (!DevUsers.TryGetValue(request.Email.Trim(), out var user))
        {
            logger.LogWarning("Login attempt for unknown user: {Email}", request.Email);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (user.Password != request.Password)
        {
            logger.LogWarning("Invalid password for user: {Email}", request.Email);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var token = GenerateJwt(request.Email.Trim(), user.FullName, user.Role, expiresAt);

        logger.LogInformation("User {Email} ({Role}) logged in via DevJwt", request.Email, user.Role);

        return Ok(new LoginResponse(
            Token: token,
            User: new UserInfo(
                Email:      request.Email.Trim(),
                FullName:   user.FullName,
                Role:       user.Role,
                Department: user.Department,
                ExpiresAt:  expiresAt
            )
        ));
    }

    /// <summary>Returns the currently authenticated user's profile.</summary>
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var email      = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var fullName   = User.FindFirstValue(ClaimTypes.Name)  ?? "";
        var role       = User.FindFirstValue(ClaimTypes.Role)  ?? "";
        var expiryClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        var expiresAt  = expiryClaim != null
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiryClaim)).UtcDateTime
            : DateTime.UtcNow.AddHours(8);

        DevUsers.TryGetValue(email, out var devUser);

        return Ok(new UserInfo(
            Email:      email,
            FullName:   fullName,
            Role:       role,
            Department: devUser.Department ?? "General Service",
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
