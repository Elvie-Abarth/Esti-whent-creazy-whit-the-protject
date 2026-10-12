namespace MarsvinWebExample.Data;

/// <summary>Sends transactional email - login confirmation links and inactivity warnings, not marketing.</summary>
public interface IEmailSender
{
    /// <param name="english">
    /// The language <paramref name="subject"/> and <paramref name="body"/>
    /// are already written in - the sender only uses it for the part it adds
    /// itself (the footer with address and opening hours).
    /// </param>
    Task SendAsync(string toEmail, string subject, string body, bool english = false);
}
