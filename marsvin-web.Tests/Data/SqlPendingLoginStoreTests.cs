using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlPendingLoginStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);

    private ApplicationUser NewCustomer([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public void Create_ThenConsume_ReturnsTheUserIdAndReturnUrl()
    {
        var user = NewCustomer();

        var token = _pendingLogins.Create(user.UserId, "/Marsvin", TimeSpan.FromMinutes(15));
        var ticket = _pendingLogins.Consume(token);

        Assert.NotNull(ticket);
        Assert.Equal(user.UserId, ticket!.UserId);
        Assert.Equal("/Marsvin", ticket.ReturnUrl);
    }

    [Fact]
    public void Create_WithNoReturnUrl_ConsumesWithNullReturnUrl()
    {
        var user = NewCustomer();

        var token = _pendingLogins.Create(user.UserId, null, TimeSpan.FromMinutes(15));
        var ticket = _pendingLogins.Consume(token);

        Assert.NotNull(ticket);
        Assert.Null(ticket!.ReturnUrl);
    }

    [Fact]
    public void Consume_IsSingleUse_SecondAttemptReturnsNull()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, null, TimeSpan.FromMinutes(15));

        var first = _pendingLogins.Consume(token);
        var second = _pendingLogins.Consume(token);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task Consume_ExpiredToken_ReturnsNull()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, null, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        var ticket = _pendingLogins.Consume(token);

        Assert.Null(ticket);
    }

    [Fact]
    public void Consume_UnknownToken_ReturnsNull()
    {
        Assert.Null(_pendingLogins.Consume("not-a-real-token"));
    }

    [Fact]
    public void Create_ReplacesAnyExistingPendingLoginForTheSameUser()
    {
        var user = NewCustomer();
        var firstToken = _pendingLogins.Create(user.UserId, null, TimeSpan.FromMinutes(15));

        _pendingLogins.Create(user.UserId, null, TimeSpan.FromMinutes(15));

        Assert.Null(_pendingLogins.Consume(firstToken));
    }
}
