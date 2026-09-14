namespace MarsvinWebExample.Data;

/// <summary>
/// Stands in for SmtpEmailSender when no mailbox credentials are configured
/// (see Program.cs) - logs the message instead of sending it, so the app -
/// including the email-link-only login flow - stays usable out of the box.
/// Never selected once Email:Username is set via `dotnet user-secrets`.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body)
    {
        logger.LogWarning(
            "No SMTP credentials configured (see DATABASE-NOTES.txt) - " +
            "logging this email instead of sending it.\nTo: {ToEmail}\nSubject: {Subject}\n{Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}
