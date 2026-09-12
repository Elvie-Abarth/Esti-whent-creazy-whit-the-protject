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
/// </summary>
public sealed class MarsvinWebAppFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=MarsvinDb_WebTest;Trusted_Connection=True;TrustServerCertificate=True;";

    public MarsvinWebAppFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__MarsvinDb", ConnectionString);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        Environment.SetEnvironmentVariable("ConnectionStrings__MarsvinDb", null);

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
