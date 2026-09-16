using MarsvinWebExample.Data;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class DbInitializerTests
{
    private readonly SqlCatalogFixture _fixture;

    public DbInitializerTests(SqlCatalogFixture fixture) => _fixture = fixture;

    [Fact]
    public void EnsureCreatedAndSeeded_CreatesAllThreeTables()
    {
        using var connection = new SqlConnection(_fixture.ConnectionString);
        connection.Open();

        using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Products', 'Animals', 'StockProducts');",
            connection);
        var tableCount = (int)command.ExecuteScalar()!;

        Assert.Equal(3, tableCount);
    }

    [Fact]
    public void EnsureCreatedAndSeeded_SeedsExpectedRowCounts()
    {
        using var connection = new SqlConnection(_fixture.ConnectionString);
        connection.Open();

        Assert.Equal(12, CountRows(connection, "dbo.Animals"));
        Assert.Equal(12, CountRows(connection, "dbo.StockProducts"));
        Assert.Equal(24, CountRows(connection, "dbo.Products"));
    }

    [Fact]
    public void EnsureCreatedAndSeeded_ProducesTheSameRowCountsWhenRunAgain()
    {
        // The schema script drops and recreates the tables every run, then
        // reseeds from DemoCatalog - so calling it twice should land on the
        // same, deterministic counts rather than duplicating or losing rows.
        DbInitializer.EnsureCreatedAndSeeded(_fixture.ConnectionString);

        using var connection = new SqlConnection(_fixture.ConnectionString);
        connection.Open();

        Assert.Equal(24, CountRows(connection, "dbo.Products"));
    }

    private static int CountRows(SqlConnection connection, string table)
    {
        using var command = new SqlCommand($"SELECT COUNT(*) FROM {table};", connection);
        return (int)command.ExecuteScalar()!;
    }
}
