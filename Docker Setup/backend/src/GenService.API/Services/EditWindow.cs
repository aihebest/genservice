namespace GenService.API.Services;

/// <summary>
/// Self-correction window for records the General Service Manager otherwise
/// reserves to himself.
///
/// The rule the department agreed: the Department Manager can edit any record;
/// anyone else can correct only an entry they entered themselves, and only for
/// a short period after entering it — long enough to fix a typo, short enough
/// that a record everyone has relied on can't be quietly changed later.
/// Every edit is still stamped with LastEditedByName / LastEditedAt.
///
/// Kept in one place so the backend and the message the user sees agree.
/// The frontend mirrors <see cref="Hours"/> to decide whether to show Edit.
/// </summary>
public static class EditWindow
{
    public const int Hours = 48;

    public const string DeniedMessage =
        "You can edit your own entries for 48 hours after entering them. " +
        "For older entries or entries made by someone else, ask the Department Manager.";

    public static bool IsOwnRecent(string? loggedByEmail, DateTime createdAtUtc, string callerEmail)
        => !string.IsNullOrWhiteSpace(loggedByEmail)
        && !string.IsNullOrWhiteSpace(callerEmail)
        && string.Equals(loggedByEmail.Trim(), callerEmail.Trim(), StringComparison.OrdinalIgnoreCase)
        && DateTime.UtcNow - createdAtUtc <= TimeSpan.FromHours(Hours);
}
