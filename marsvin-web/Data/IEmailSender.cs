namespace MarsvinWebExample.Data;

/// <summary>Sends transactional email - login confirmation links and inactivity warnings, not marketing.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body);
}
