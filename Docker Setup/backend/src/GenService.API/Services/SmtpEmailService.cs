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
        var host     = config["Email:Host"]     ?? "mailhog";
        var port     = int.Parse(config["Email:Port"] ?? "1025");
        var fromAddr = config["Email:From"]     ?? "noreply@genservice.local";
        var fromName = config["Email:FromName"] ?? "GenService Platform";

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl            = false,
                DeliveryMethod       = SmtpDeliveryMethod.Network,
                UseDefaultCredentials= false,
                Credentials          = new NetworkCredential("", ""),
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
