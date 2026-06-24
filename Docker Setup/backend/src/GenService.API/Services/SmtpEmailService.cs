using System.Net;
using System.Net.Mail;

namespace GenService.API.Services;

/// <summary>
/// Sends emails via SMTP.
/// In development, points to MailHog (localhost:1025) which captures all emails
/// in a local web inbox at http://localhost:8025 — no real emails are sent.
/// </summary>
public class SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> log) : IEmailService
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host     = config["Email:Host"]     ?? "";
        var port     = int.Parse(config["Email:Port"] ?? "587");
        var fromAddr = config["Email:From"]     ?? "";
        var fromName = config["Email:FromName"] ?? "GenService Platform";
        var user     = config["Email:User"]     ?? "";
        var password = config["Email:Password"] ?? "";
        var enableSsl= bool.Parse(config["Email:EnableSsl"] ?? "true");

        // If no SMTP host is configured, log and skip (non-fatal)
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddr))
        {
            log.LogWarning("📧 Email NOT sent (no SMTP configured). To: {Email} | Subject: {Subject}", toEmail, subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl            = enableSsl,
                DeliveryMethod       = SmtpDeliveryMethod.Network,
                UseDefaultCredentials= false,
                Credentials          = string.IsNullOrWhiteSpace(user)
                    ? null
                    : new NetworkCredential(user, password),
            };

            using var msg = new MailMessage
            {
                From       = new MailAddress(fromAddr, fromName),
                Subject    = subject,
                Body       = htmlBody,
                IsBodyHtml = true,
            };
            msg.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(msg);
            log.LogInformation("📧 Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Never crash the app over an email failure
            log.LogWarning(ex, "⚠️ Email failed (to: {Email}, subject: {Subject}). Continuing.", toEmail, subject);
        }
    }
}
