using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarsvinWebExample.Tests.Data;

/// <summary>
/// The threshold alerting: a single failed login emails nobody, an automated
/// pattern emails the admins once (then the cooldown holds), and every alert
/// says plainly that it is about *attempts*, not a confirmed breach.
/// </summary>
[Collection("SqlCatalog collection")]
public class SecurityAlerterTests(SqlCatalogFixture fixture)
{
    private readonly SqlSecurityEventStore _store = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private static SecurityAlerter NewAlerter() => new("http://localhost", NullLogger<SecurityAlerter>.Instance);
    private static string UniqueIp() => "ip-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task ConsiderAsync_AFewFailedLogins_EmailsNobody()
    {
        var ip = UniqueIp();
        var email = new RecordingEmailSender();
        _store.Record(new SecurityEvent { Type = SecurityEventType.LoginFailed, IpAddress = ip, Email = "a@x.dk" });
        _store.Record(new SecurityEvent { Type = SecurityEventType.LoginFailed, IpAddress = ip, Email = "b@x.dk" });

        await NewAlerter().ConsiderAsync(
            new SecurityEvent { Type = SecurityEventType.LoginFailed, IpAddress = ip, Email = "b@x.dk" },
            _store, _users, email);

        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task ConsiderAsync_CredentialStuffingPattern_EmailsTheAdminsOnce_AndSaysItIsNotABreach()
    {
        var ip = UniqueIp();
        var email = new RecordingEmailSender();
        // 10 failed logins from one IP across 4 accounts - over the threshold.
        for (var i = 0; i < 10; i++)
            _store.Record(new SecurityEvent { Type = SecurityEventType.LoginFailed, IpAddress = ip, Email = $"victim{i % 4}@x.dk" });
        var alerter = NewAlerter();
        var trigger = new SecurityEvent { Type = SecurityEventType.LoginFailed, IpAddress = ip, Email = "victim0@x.dk" };

        await alerter.ConsiderAsync(trigger, _store, _users, email);

        Assert.NotEmpty(email.Sent);
        Assert.All(email.Sent, m =>
        {
            Assert.True(m.Subject.Contains("Mistænkelig") || m.Subject.Contains("Suspicious"));
            Assert.Contains("/Admin/SecurityLog", m.Body);
            // The honest disclaimer - never claims a confirmed breach.
            Assert.True(m.Body.Contains("tilgået eller lækket") || m.Body.Contains("accessed or leaked"));
        });

        // The cooldown holds: the same pattern a moment later sends nothing new.
        var before = email.Sent.Count;
        await alerter.ConsiderAsync(trigger, _store, _users, email);
        Assert.Equal(before, email.Sent.Count);
    }
}
