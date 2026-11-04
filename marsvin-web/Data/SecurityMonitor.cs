using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// The one entry point the rest of the app uses to record an attack attempt:
/// it writes the event, then lets <see cref="SecurityAlerter"/> decide whether
/// the pattern now warrants an email to the admins. Scoped, so it has a
/// per-request store/user/email to work with; the alerter it calls is the
/// shared singleton that holds the cooldown.
///
/// Monitoring must never break the thing it's watching, so every call is
/// wrapped - a logging or email hiccup here can't turn a failed login or a
/// rejected form into a 500.
/// </summary>
public sealed class SecurityMonitor(
    ISecurityEventStore store, IUserAccountStore users, IEmailSender emailSender,
    SecurityAlerter alerter, ILogger<SecurityMonitor> logger)
{
    public async Task RecordAsync(
        SecurityEventType type, string? email, string? ipAddress, string? path, string? detail = null)
    {
        try
        {
            var securityEvent = new SecurityEvent
            {
                Type = type,
                // Normalised to match how accounts are stored and looked up, so
                // the "distinct accounts from one IP" count is accurate.
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress,
                // Single-line, so a crafted path can't fake a second log row.
                Path = SafeOrNull(path),
                Detail = SafeOrNull(detail)
            };

            store.Record(securityEvent);
            await alerter.ConsiderAsync(securityEvent, store, users, emailSender);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record a {Type} security event.", type);
        }
    }

    private static string? SafeOrNull(string? value) =>
        string.IsNullOrEmpty(value) ? null : LogSafe.Line(value);
}
