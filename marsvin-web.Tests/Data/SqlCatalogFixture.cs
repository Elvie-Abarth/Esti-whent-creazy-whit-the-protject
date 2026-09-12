using MarsvinWebExample.Data;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Tests.Data;

/// <summary>
/// Creates a dedicated MarsvinDb_Test database on LocalDB, seeds it via
/// DbInitializer (the exact same code path the real app uses), and drops it
/// again once the test class is done - so these tests never touch the real
/// MarsvinDb used by "dotnet run". Requires SQL Server LocalDB
/// ((localdb)\MSSQLLocalDB) to be installed and running.
/// </summary>
public sealed class SqlCatalogFixture : IDisposable
{
    public string ConnectionString { get; } =
        "Server=(localdb)\\MSSQLLocalDB;Database=MarsvinDb_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    public SqlCatalogFixture()
    {
        DbInitializer.EnsureCreatedAndSeeded(ConnectionString);
    }

    public void Dispose()
    {
        SqlConnection.ClearAllPools();

        var builder = new SqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = new SqlCommand(
            """
            IF DB_ID(@name) IS NOT NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) =
                    N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; ' +
                    N'DROP DATABASE ' + QUOTENAME(@name) + N';';
                EXEC (@sql);
            END
            """, connection);
        command.Parameters.AddWithValue("@name", databaseName);
        command.ExecuteNonQuery();
    }
}
