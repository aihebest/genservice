namespace GenService.API.Services;

/// <summary>Minimal HTML email templates for workflow notifications.</summary>
public static class EmailTemplates
{
    private static string Wrap(string body) => $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:24px;background:#f9f9f9;">
          <div style="background:#fff;border-radius:8px;padding:32px;border:1px solid #e8e8e8;">
            <div style="margin-bottom:24px;">
              <span style="font-size:20px;font-weight:700;color:#1a1a2e;">Desicon Group</span>
              <span style="font-size:13px;color:#888;margin-left:8px;">General Service Management</span>
            </div>
            {body}
            <hr style="border:none;border-top:1px solid #f0f0f0;margin:24px 0;"/>
            <p style="color:#aaa;font-size:11px;margin:0;">
              This is an automated notification from the General Service Management Platform.
              Please do not reply to this email.
            </p>
          </div>
        </div>
        """;

    public static string Approved(string name, string refNumber, string approvedBy, string nextStep) =>
        Wrap($"""
            <h2 style="color:#52c41a;margin:0 0 16px;">✅ Request Approved</h2>
            <p style="color:#333;">Hi {name},</p>
            <p style="color:#333;">Your request <strong>{refNumber}</strong> has been <strong>{approvedBy}</strong>.</p>
            <div style="background:#f6ffed;border:1px solid #b7eb8f;border-radius:6px;padding:16px;margin:16px 0;">
              <strong>Next Step:</strong> {nextStep}
            </div>
            <p style="color:#333;">You can track the progress of your request by logging into the platform.</p>
            """);

    public static string Rejected(string name, string refNumber, string rejectedBy, string reason) =>
        Wrap($"""
            <h2 style="color:#ff4d4f;margin:0 0 16px;">❌ Request Rejected</h2>
            <p style="color:#333;">Hi {name},</p>
            <p style="color:#333;">Your request <strong>{refNumber}</strong> has been rejected by <strong>{rejectedBy}</strong>.</p>
            <div style="background:#fff2f0;border:1px solid #ffccc7;border-radius:6px;padding:16px;margin:16px 0;">
              <strong>Reason:</strong> {reason}
            </div>
            <p style="color:#333;">If you believe this is incorrect, please speak with your line manager or raise a new request with additional details.</p>
            """);

    // ── Maintenance Reminder ──────────────────────────────────────────────────

    public static string MaintenanceDueSoon(string recipientName, string taskName,
        string location, string category, string dueDate, string daysLeft) =>
        Wrap($"""
            <h2 style="color:#fa8c16;margin:0 0 16px;">⏰ Maintenance Due Soon</h2>
            <p style="color:#333;">Hi {recipientName},</p>
            <p style="color:#333;">The following scheduled maintenance task is due in <strong>{daysLeft}</strong>:</p>
            <table style="width:100%;border-collapse:collapse;margin:16px 0;">
              <tr><td style="padding:8px;background:#fafafa;width:140px;font-weight:600;color:#555;">Task</td><td style="padding:8px;">{taskName}</td></tr>
              <tr><td style="padding:8px;background:#fafafa;font-weight:600;color:#555;">Category</td><td style="padding:8px;">{category}</td></tr>
              <tr><td style="padding:8px;background:#fafafa;font-weight:600;color:#555;">Location</td><td style="padding:8px;">{location}</td></tr>
              <tr><td style="padding:8px;background:#fafafa;font-weight:600;color:#555;">Due Date</td><td style="padding:8px;"><strong>{dueDate}</strong></td></tr>
            </table>
            <p style="color:#333;">Please ensure this task is completed on time. Log into the platform to update progress.</p>
            """);

    public static string MaintenanceOverdue(string recipientName, string taskName,
        string location, string category, string dueDate, string daysOverdue) =>
        Wrap($"""
            <h2 style="color:#ff4d4f;margin:0 0 16px;">🚨 Maintenance Overdue — Action Required</h2>
            <p style="color:#333;">Hi {recipientName},</p>
            <p style="color:#333;">The following maintenance task is <strong style="color:#ff4d4f;">{daysOverdue} overdue</strong> and requires immediate attention:</p>
            <table style="width:100%;border-collapse:collapse;margin:16px 0;">
              <tr><td style="padding:8px;background:#fff2f0;width:140px;font-weight:600;color:#555;">Task</td><td style="padding:8px;">{taskName}</td></tr>
              <tr><td style="padding:8px;background:#fff2f0;font-weight:600;color:#555;">Category</td><td style="padding:8px;">{category}</td></tr>
              <tr><td style="padding:8px;background:#fff2f0;font-weight:600;color:#555;">Location</td><td style="padding:8px;">{location}</td></tr>
              <tr><td style="padding:8px;background:#fff2f0;font-weight:600;color:#555;">Was Due</td><td style="padding:8px;"><strong style="color:#ff4d4f;">{dueDate}</strong></td></tr>
            </table>
            <p style="color:#333;">Please log into the platform immediately to address this overdue task or reassign it to the appropriate team member.</p>
            """);

    public static string MaintenanceEscalation(string recipientName, string taskName,
        string location, string category, string dueDate, string daysOverdue, string escalationLevel) =>
        Wrap($"""
            <h2 style="color:#ff4d4f;margin:0 0 16px;">⚠️ Maintenance Escalation — {escalationLevel}</h2>
            <p style="color:#333;">Hi {recipientName},</p>
            <p style="color:#333;">This is an <strong>escalation notice</strong>. The following scheduled maintenance task has not been completed and is <strong style="color:#ff4d4f;">{daysOverdue} overdue</strong>:</p>
            <table style="width:100%;border-collapse:collapse;margin:16px 0;">
              <tr><td style="padding:8px;background:#fff2f0;width:140px;font-weight:600;color:#555;">Task</td><td style="padding:8px;">{taskName}</td></tr>
              <tr><td style="padding:8px;background:#fff2f0;font-weight:600;color:#555;">Category</td><td style="padding:8px;">{category}</td></tr>
              <tr><td style="padding:8px;background:#fff2f0;font-weight:600;color:#555;">Location</td><td style="padding:8px;">{location}</td></tr>
              <tr><td style="padding:8px;background:#fff2f0;font-weight:600;color:#555;">Was Due</td><td style="padding:8px;"><strong style="color:#ff4d4f;">{dueDate}</strong></td></tr>
            </table>
            <p style="color:#333;">This issue has been escalated to you because it remains unresolved. Please ensure this is actioned immediately and mark it as complete in the platform once done.</p>
            """);

    public static string Submitted(string refNumber, string category, string location, string submittedBy) =>
        Wrap($"""
            <h2 style="color:#1677ff;margin:0 0 16px;">📋 New Request Submitted</h2>
            <p style="color:#333;">A new <strong>{category}</strong> request has been submitted and requires your review.</p>
            <table style="width:100%;border-collapse:collapse;margin:16px 0;">
              <tr><td style="padding:8px;background:#fafafa;width:140px;font-weight:600;color:#555;">Reference</td><td style="padding:8px;">{refNumber}</td></tr>
              <tr><td style="padding:8px;background:#fafafa;font-weight:600;color:#555;">Category</td><td style="padding:8px;">{category}</td></tr>
              <tr><td style="padding:8px;background:#fafafa;font-weight:600;color:#555;">Location</td><td style="padding:8px;">{location}</td></tr>
              <tr><td style="padding:8px;background:#fafafa;font-weight:600;color:#555;">Raised By</td><td style="padding:8px;">{submittedBy}</td></tr>
            </table>
            <p style="color:#333;">Please log into the platform to review and approve or reject this request.</p>
            """);
}
