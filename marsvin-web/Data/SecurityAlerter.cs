using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// Turns a stream of security events into the occasional admin email - but
/// only when a *pattern* crosses a threshold (a burst of failed logins from
/// one IP across several accounts, a spike of wrong-role clicks, sustained
/// rate-limiting or form rejections). A single failed login never emails
/// anyone; an automated attack does.
///
/// Honesty matters here: this alerts on *attempts* seen in the logs, it does
/// NOT claim a breach or a data leak - no app can truthfully detect that, and
/// the email says so in as many words.
///
/// A singleton, because the only state it keeps is the per-category cooldown
/// that stops one attacker from flooding the admins' inboxes; the counts
/// themselves come from the (scoped) store it's handed on each call.
/// </summary>
public sealed class SecurityAlerter(string appBaseUrl, ILogger<SecurityAlerter> logger)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(30);

    private readonly object _gate = new();
    private readonly Dictionary<string, DateTime> _lastAlertByCategory = new();

    public async Task ConsiderAsync(
        SecurityEvent e, ISecurityEventStore store, IUserAccountStore users, IEmailSender emailSender)
    {
        var alert = Evaluate(e, store);
        if (alert is null) return;

        // Cooldown gate: one alert per category per 30 minutes, so a scanner
        // that keeps tripping the same threshold can't bury the admins.
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (_lastAlertByCategory.TryGetValue(alert.Value.Category, out var last) && now - last < Cooldown)
                return;
            _lastAlertByCategory[alert.Value.Category] = now;
        }

        foreach (var admin in users.GetAll().Where(u => u is { Role: UserRole.Admin, IsActive: true }))
        {
            var english = admin.PrefersEnglish;
            var subject = english ? "Suspicious activity on Marsvin" : "Mistænkelig aktivitet på Marsvin";
            var greeting = english ? $"Hello {admin.DisplayName}," : $"Hej {admin.DisplayName},";
            var footer = english
                ? $"\n\nThis is an automatic alert about suspicious activity seen in the logs - NOT confirmation that anything was accessed or leaked. See {appBaseUrl}/Admin/SecurityLog for the full list."
                : $"\n\nDette er en automatisk advarsel om mistænkelig aktivitet i logfilerne - IKKE en bekræftelse på at noget er blevet tilgået eller lækket. Se {appBaseUrl}/Admin/SecurityLog for hele listen.";
            var body = $"{greeting}\n\n{(english ? alert.Value.En : alert.Value.Da)}{footer}";

            try
            {
                await emailSender.SendAsync(admin.Email, subject, body, english);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send a security alert to an admin.");
            }
        }
    }

    // Returns the alert to send (a cooldown category and the two language
    // texts), or null when nothing crosses a threshold. Counts come straight
    // from the store, over the last 10 minutes.
    private static (string Category, string Da, string En)? Evaluate(SecurityEvent e, ISecurityEventStore store)
    {
        var since = DateTime.UtcNow - Window;
        var ip = e.IpAddress;

        switch (e.Type)
        {
            case SecurityEventType.LoginFailed when !string.IsNullOrEmpty(ip):
                var accounts = store.DistinctEmailsFromIpSince(ip, since, SecurityEventType.LoginFailed);
                var attempts = store.CountFromIpSince(ip, since, SecurityEventType.LoginFailed);
                if (accounts >= 3 && attempts >= 10)
                    return ($"stuffing:{ip}",
                        $"Der er set {attempts} mislykkede loginforsøg fra IP {ip} mod {accounts} forskellige konti inden for de sidste 10 minutter. Det ligner automatiseret gætning af adgangskoder (credential stuffing). Berørte konti låser sig selv efter gentagne forsøg.",
                        $"{attempts} failed login attempts have come from IP {ip} against {accounts} different accounts in the last 10 minutes. This looks like automated password guessing (credential stuffing). The accounts involved lock themselves after repeated failures.");
                break;

            case SecurityEventType.AccountLocked:
                var locks = store.CountSince(since, SecurityEventType.AccountLocked);
                if (locks >= 3)
                    return ("lockouts",
                        $"{locks} konti er blevet låst efter gentagne mislykkede loginforsøg inden for de sidste 10 minutter - et tegn på et bredt angreb mod flere konti.",
                        $"{locks} accounts have been locked after repeated failed logins in the last 10 minutes - a sign of a broad attack across several accounts.");
                break;

            case SecurityEventType.AccessDenied when !string.IsNullOrEmpty(ip):
                var denied = store.CountFromIpSince(ip, since, SecurityEventType.AccessDenied);
                if (denied >= 10)
                    return ($"probing:{ip}",
                        $"IP {ip} er blevet nægtet adgang {denied} gange inden for de sidste 10 minutter - nogen prøver gentagne gange at nå sider, deres rolle ikke har adgang til (forced browsing / rettighedsafprøvning).",
                        $"IP {ip} has been denied access {denied} times in the last 10 minutes - someone is repeatedly trying to reach pages their role isn't allowed on (forced browsing / privilege probing).");
                break;

            case SecurityEventType.RateLimited when !string.IsNullOrEmpty(ip):
                var limited = store.CountFromIpSince(ip, since, SecurityEventType.RateLimited);
                if (limited >= 30)
                    return ($"hammering:{ip}",
                        $"IP {ip} er blevet begrænset {limited} gange inden for de sidste 10 minutter - en vedholdende strøm af forsøg mod login/opret/nulstil eller chatten.",
                        $"IP {ip} has been rate-limited {limited} times in the last 10 minutes - a sustained flood of attempts against login/register/reset or the chat.");
                break;

            case SecurityEventType.AntiforgeryRejected when !string.IsNullOrEmpty(ip):
                var rejected = store.CountFromIpSince(ip, since, SecurityEventType.AntiforgeryRejected);
                if (rejected >= 10)
                    return ($"forgery:{ip}",
                        $"IP {ip} har fået afvist {rejected} formularer (manglende eller forkert anti-forgery-token) inden for de sidste 10 minutter - et tegn på forfalskede eller scriptede anmodninger.",
                        $"IP {ip} has had {rejected} forms rejected (missing or wrong anti-forgery token) in the last 10 minutes - a sign of forged or scripted requests.");
                break;
        }

        return null;
    }
}
