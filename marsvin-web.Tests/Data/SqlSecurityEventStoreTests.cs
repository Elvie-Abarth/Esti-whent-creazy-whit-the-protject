using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Tests.Data;

/// <summary>
/// The security-event store against the real test database. Each test tags
/// its rows with a unique fake "IP" so it sees only its own, independent of
/// anything other tests in the shared collection recorded.
/// </summary>
[Collection("SqlCatalog collection")]
public class SqlSecurityEventStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlSecurityEventStore _store = new(fixture.ConnectionString);

    private static string UniqueIp() => "ip-" + Guid.NewGuid().ToString("N");
    private static DateTime LastHour => DateTime.UtcNow.AddHours(-1);

    private void Record(SecurityEventType type, string ip, string? email = null) =>
        _store.Record(new SecurityEvent { Type = type, IpAddress = ip, Email = email, Path = "/Account/Login", Detail = "x" });

    [Fact]
    public void Record_ThenGetRecent_RoundTripsTheFields_NewestFirst()
    {
        var ip = UniqueIp();
        _store.Record(new SecurityEvent
        {
            Type = SecurityEventType.AccessDenied, IpAddress = ip, Email = "a@b.dk",
            Path = "/Admin/Users", Detail = "Role not allowed"
        });

        var mine = _store.GetRecent(500).First(e => e.IpAddress == ip);
        Assert.Equal(SecurityEventType.AccessDenied, mine.Type);
        Assert.Equal("a@b.dk", mine.Email);
        Assert.Equal("/Admin/Users", mine.Path);
        Assert.Equal("Role not allowed", mine.Detail);
        Assert.True(mine.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public void CountFromIpSince_FiltersByTypeWhenTypesAreGiven_AndCountsAllOtherwise()
    {
        var ip = UniqueIp();
        Record(SecurityEventType.LoginFailed, ip);
        Record(SecurityEventType.LoginFailed, ip);
        Record(SecurityEventType.RateLimited, ip);

        Assert.Equal(2, _store.CountFromIpSince(ip, LastHour, SecurityEventType.LoginFailed));
        Assert.Equal(1, _store.CountFromIpSince(ip, LastHour, SecurityEventType.RateLimited));
        Assert.Equal(3, _store.CountFromIpSince(ip, LastHour));
    }

    [Fact]
    public void DistinctEmailsFromIpSince_CountsEachAccountOnce()
    {
        var ip = UniqueIp();
        Record(SecurityEventType.LoginFailed, ip, "a@x.dk");
        Record(SecurityEventType.LoginFailed, ip, "a@x.dk");
        Record(SecurityEventType.LoginFailed, ip, "b@x.dk");
        Record(SecurityEventType.LoginFailed, ip, "c@x.dk");

        Assert.Equal(3, _store.DistinctEmailsFromIpSince(ip, LastHour, SecurityEventType.LoginFailed));
    }

    [Fact]
    public void CountSince_IgnoresEventsBeforeTheWindow()
    {
        var ip = UniqueIp();
        Record(SecurityEventType.AccountLocked, ip);
        Backdate(ip, DateTime.UtcNow.AddHours(-2));

        Assert.Equal(0, _store.CountFromIpSince(ip, DateTime.UtcNow.AddMinutes(-10), SecurityEventType.AccountLocked));
        Assert.Equal(1, _store.CountFromIpSince(ip, DateTime.UtcNow.AddHours(-3), SecurityEventType.AccountLocked));
    }

    [Fact]
    public void DeleteOlderThan_RemovesOnlyTheRowsPastTheCutoff()
    {
        var ip = UniqueIp();
        Record(SecurityEventType.LoginFailed, ip);
        Backdate(ip, DateTime.UtcNow.AddDays(-100));

        var deleted = _store.DeleteOlderThan(DateTime.UtcNow.AddDays(-90));

        Assert.True(deleted >= 1);
        Assert.Equal(0, _store.CountFromIpSince(ip, DateTime.UtcNow.AddDays(-200), SecurityEventType.LoginFailed));
    }

    // Moves this IP's rows back in time, so the retention/window tests have
    // something that is genuinely "old" without waiting.
    private void Backdate(string ip, DateTime when)
    {
        using var connection = new SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var command = new SqlCommand(
            "UPDATE dbo.SecurityEvents SET CreatedAt = @When WHERE IpAddress = @Ip;", connection);
        command.Parameters.AddWithValue("@When", when);
        command.Parameters.AddWithValue("@Ip", ip);
        command.ExecuteNonQuery();
    }
}
