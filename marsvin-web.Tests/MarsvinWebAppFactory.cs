using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Tests;

/// <summary>
/// Boots the real app in-process (Program.cs, full middleware pipeline -
/// routing, authentication, authorization, antiforgery) against a disposable
/// MarsvinDb_WebTest database, so tests exercise the actual HTTP behavior
/// rather than calling PageModel methods directly.
///
/// Program.cs reads ConnectionStrings:MarsvinDb and runs DbInitializer
/// *before* WebApplicationBuilder.Build() - earlier than the point
/// WebApplicationFactory's own ConfigureWebHost/ConfigureAppConfiguration
/// hooks can reach, since those only take effect at Build() time. An
/// environment variable is the one override that's already loaded into
/// configuration the moment WebApplication.CreateBuilder(args) runs, so
/// that's what's used here instead.
///
/// Email:Username gets the same treatment, forced to empty here regardless
/// of what's in the developer's own user-secrets (set there to send real
/// mail when running the app by hand) - otherwise every test that goes
/// through Login/Register/checkout would send a real email through those
/// same credentials on every test run, dozens of times over. Empty makes
/// Program.cs's own IsNullOrWhiteSpace check pick LoggingEmailSender, the
/// same as a machine with no SMTP configured at all.
/// </summary>
public sealed class MarsvinWebAppFactory : WebApplicationFactory<Program>
{
    public const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=MarsvinDb_WebTest;Trusted_Connection=True;TrustServerCertificate=True;";

    public MarsvinWebAppFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__MarsvinDb", ConnectionString);
        Environment.SetEnvironmentVariable("Email__Username", "");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        Environment.SetEnvironmentVariable("ConnectionStrings__MarsvinDb", null);
        Environment.SetEnvironmentVariable("Email__Username", null);

        SqlConnection.ClearAllPools();
        using var connection = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;");
        connection.Open();
        using var command = new SqlCommand(
            """
            IF DB_ID('MarsvinDb_WebTest') IS NOT NULL
            BEGIN
                ALTER DATABASE MarsvinDb_WebTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE MarsvinDb_WebTest;
            END
            """, connection);
        command.ExecuteNonQuery();
    }
}
