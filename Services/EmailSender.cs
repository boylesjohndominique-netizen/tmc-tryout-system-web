using System.Net;
using System.Net.Mail;

namespace TmcTryoutSystem.Services;

/// <summary>SMTP settings for outgoing mail (verification codes). Bound from the "Smtp" config section.</summary>
public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "TMC Tryouts";
    public bool EnableSsl { get; set; } = true;

    /// <summary>True once a host and sender address are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromEmail);
}

public interface IEmailSender
{
    /// <summary>False when SMTP settings are missing, so callers can fall back to a development hint.</summary>
    bool IsConfigured { get; }

    Task SendAsync(string to, string subject, string htmlBody);
}

/// <summary>
/// Sends mail through the SMTP server configured in appsettings. When no server is
/// configured the message is logged instead of throwing, so sign-up still works locally.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning(
                "SMTP is not configured (set Smtp:Host and Smtp:FromEmail). Skipped email to {To} with subject '{Subject}'.",
                to, subject);
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            // The default is 100 seconds, which would hang a sign-up request.
            Timeout = 15_000
        };
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Surface a clear log line; the caller decides how to tell the user.
            _logger.LogError(ex, "SMTP delivery to {To} failed through {Host}:{Port}.", to, _options.Host, _options.Port);
            throw;
        }
    }
}
