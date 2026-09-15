using MarsvinWebExample.Data;
using Microsoft.Extensions.DependencyInjection;

namespace MarsvinWebExample.Tests;

/// <summary>
/// Guards against the whole test suite silently sending real email: if a
/// developer has real SMTP credentials in their own user-secrets (to send
/// real mail when running the app by hand), MarsvinWebAppFactory forces
/// Email:Username empty for the test host regardless - see its constructor.
/// Without that override, every test exercising Login/Register/checkout
/// would fire a real email through those credentials on every test run.
/// </summary>
[Collection("WebApp collection")]
public class EmailSenderTests(MarsvinWebAppFactory factory)
{
    [Fact]
    public void TestHost_AlwaysUsesLoggingEmailSender_NeverRealSmtp()
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        Assert.IsType<LoggingEmailSender>(sender);
    }
}
