namespace GenService.API.Domain;

/// <summary>
/// A DStv subscription for a company location. The system auto-calculates the
/// expiry date from the start date + duration and drives 7/3/1-day renewal reminders.
/// </summary>
public class DstvSubscription
{
    public Guid     Id { get; set; } = Guid.NewGuid();

    // ── Identity ──────────────────────────────────────────────────────────────
    public string   DecoderNumber   { get; set; } = "";   // decoder / smart card number
    public string   Location        { get; set; } = "";   // assigned location
    public string   Package         { get; set; } = "";   // Premium, Compact Plus, Compact, etc.

    // ── Subscription period ───────────────────────────────────────────────────
    public DateTime StartDate       { get; set; } = DateTime.UtcNow.Date;
    public int      DurationMonths  { get; set; } = 1;     // subscription duration
    public DateTime ExpiryDate      { get; set; }          // auto = StartDate + DurationMonths

    // ── Payment ───────────────────────────────────────────────────────────────
    public decimal  AmountNaira     { get; set; }
    public string?  PaymentMethod   { get; set; }          // Transfer, Cash, POS, Online
    public string?  Vendor          { get; set; }          // vendor / agent
    public string?  ReceiptAttachment { get; set; }

    // ── Status & reminder tracking ────────────────────────────────────────────
    public string   Status          { get; set; } = DstvStatus.Active;
    /// <summary>Last reminder milestone already sent (7, 3, 1) — prevents duplicate reminders.</summary>
    public int?     LastReminderDaysOut { get; set; }
    public bool     ExpiredNotified { get; set; } = false;

    public string?  Notes           { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
}

public static class DstvStatus
{
    public const string Active       = "Active";        // more than 7 days to expiry
    public const string ExpiringSoon = "ExpiringSoon";  // within 7 days
    public const string Expired      = "Expired";       // past expiry
}
