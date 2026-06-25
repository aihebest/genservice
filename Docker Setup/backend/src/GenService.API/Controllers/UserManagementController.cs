using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

/// <summary>
/// User management — SystemAdmin only.
/// Provides full CRUD over AppUsers plus deactivate/activate and password reset.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "SystemAdmin,DepartmentManager")]
public class UserManagementController(
    GenServiceDbContext db,
    ILogger<UserManagementController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";

    private static AppUserDto ToDto(AppUser u) => new(
        u.Id, u.Email, u.FullName, u.Role, u.Department,
        u.IsActive, u.LastLoginAt, u.CreatedByEmail, u.CreatedAt, u.UpdatedAt
    );

    // ── GET /api/v1/users ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<UserListResponse>> List([FromQuery] UserListQuery q)
    {
        var query = db.AppUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Role))
            query = query.Where(u => u.Role == q.Role);

        if (!string.IsNullOrWhiteSpace(q.Department))
            query = query.Where(u => u.Department == q.Department);

        if (q.IsActive.HasValue)
            query = query.Where(u => u.IsActive == q.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            // SQL Server CI_AS collation handles case-insensitive search — no .ToLower() on DB columns
            query = query.Where(u =>
                u.Email.Contains(s) ||
                u.FullName.Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new UserListResponse(items.Select(ToDto), total, q.Page, q.PageSize));
    }

    // ── GET /api/v1/users/summary ─────────────────────────────────────────────
    /// <summary>Returns per-role counts and total active/inactive for the header cards.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var all = await db.AppUsers.AsNoTracking().ToListAsync();
        return Ok(new
        {
            total      = all.Count,
            active     = all.Count(u => u.IsActive),
            inactive   = all.Count(u => !u.IsActive),
            byRole     = all.GroupBy(u => u.Role)
                            .Select(g => new { role = g.Key, count = g.Count() })
                            .OrderByDescending(x => x.count)
                            .ToList(),
        });
    }

    // ── GET /api/v1/users/{id} ────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AppUserDto>> GetById(Guid id)
    {
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();
        return Ok(ToDto(u));
    }

    // ── POST /api/v1/users ────────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<AppUserDto>> Create([FromBody] CreateUserRequest req)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email is required." });

        if (!AppUserRoles.All.Contains(req.Role))
            return BadRequest(new { message = $"Invalid role '{req.Role}'." });

        if (req.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        var email = req.Email.Trim().ToLowerInvariant();
        // SQL Server collation is case-insensitive — no .ToLower() needed on DB side (EF Core 8 can't translate it)
        if (await db.AppUsers.AnyAsync(u => u.Email == email))
            return Conflict(new { message = $"A user with email '{email}' already exists." });

        try
        {
            var user = new AppUser
            {
                Email          = email,
                FullName       = req.FullName.Trim(),
                PasswordHash   = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 10),
                Role           = req.Role,
                Department     = req.Department?.Trim() ?? "",
                IsActive       = true,
                CreatedByEmail = CallerEmail,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            };

            db.AppUsers.Add(user);
            await db.SaveChangesAsync();

            logger.LogInformation("User {Email} created by {Admin}", user.Email, CallerEmail);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create user {Email}", email);
            // Diagnostic — expose error type so we can see what's failing
            return StatusCode(500, new { message = $"Create failed ({ex.GetType().Name}): {ex.Message}" });
        }
    }

    // ── PUT /api/v1/users/{id} ────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AppUserDto>> Update(Guid id, [FromBody] UpdateUserRequest req)
    {
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();

        if (req.FullName   != null) u.FullName   = req.FullName.Trim();
        if (req.Department != null) u.Department = req.Department.Trim();
        if (req.Role       != null)
        {
            if (!AppUserRoles.All.Contains(req.Role))
                return BadRequest(new { message = $"Invalid role '{req.Role}'." });
            u.Role = req.Role;
        }

        u.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("User {Email} updated by {Admin}", u.Email, CallerEmail);
        return Ok(ToDto(u));
    }

    // ── POST /api/v1/users/{id}/deactivate ───────────────────────────────────
    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<AppUserDto>> Deactivate(Guid id)
    {
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();

        // Prevent deactivating yourself
        if (u.Email.ToLower() == CallerEmail.ToLower())
            return BadRequest(new { message = "You cannot deactivate your own account." });

        u.IsActive  = false;
        u.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("User {Email} deactivated by {Admin}", u.Email, CallerEmail);
        return Ok(ToDto(u));
    }

    // ── POST /api/v1/users/{id}/activate ─────────────────────────────────────
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<AppUserDto>> Activate(Guid id)
    {
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();

        u.IsActive  = true;
        u.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("User {Email} activated by {Admin}", u.Email, CallerEmail);
        return Ok(ToDto(u));
    }

    // ── POST /api/v1/users/{id}/reset-password ────────────────────────────────
    /// <summary>
    /// Generates a secure temporary password, hashes and stores it, then
    /// returns the plain-text password to the admin to hand to the user.
    /// The user should change it on next login.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(Guid id)
    {
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();

        // Generate a readable 12-char temporary password
        var tempPassword = GenerateTemporaryPassword();

        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 11);
        u.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Password reset for {Email} by {Admin} — temp password issued",
            u.Email, CallerEmail);

        return Ok(new ResetPasswordResponse(
            Email:             u.Email,
            TemporaryPassword: tempPassword,
            Message:           $"Password for {u.FullName} has been reset. Share the temporary password securely — they should change it immediately after login."
        ));
    }

    // ── POST /api/v1/users/me/change-password ─────────────────────────────────
    /// <summary>Any authenticated user can change their own password.</summary>
    [HttpPost("me/change-password")]
    [Authorize]   // override controller-level SystemAdmin restriction
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest req)
    {
        var email = CallerEmail;
        var u     = await db.AppUsers.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, u.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        if (req.NewPassword.Length < 8)
            return BadRequest(new { message = "New password must be at least 8 characters." });

        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 11);
        u.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully." });
    }

    // ── DELETE /api/v1/users/{id} ─────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var u = await db.AppUsers.FindAsync(id);
        if (u is null) return NotFound();

        if (u.Email.ToLower() == CallerEmail.ToLower())
            return BadRequest(new { message = "You cannot delete your own account." });

        db.AppUsers.Remove(u);
        await db.SaveChangesAsync();

        logger.LogWarning("User {Email} permanently deleted by {Admin}", u.Email, CallerEmail);
        return NoContent();
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private static string GenerateTemporaryPassword()
    {
        const string upper   = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string lower   = "abcdefghjkmnpqrstuvwxyz";
        const string digits  = "23456789";
        const string special = "@#!$";

        var rng  = new Random(Guid.NewGuid().GetHashCode());
        var chars = new char[12];
        chars[0]  = upper  [rng.Next(upper.Length)];
        chars[1]  = lower  [rng.Next(lower.Length)];
        chars[2]  = digits [rng.Next(digits.Length)];
        chars[3]  = special[rng.Next(special.Length)];
        var all = upper + lower + digits;
        for (int i = 4; i < 12; i++)
            chars[i] = all[rng.Next(all.Length)];

        // Shuffle
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
