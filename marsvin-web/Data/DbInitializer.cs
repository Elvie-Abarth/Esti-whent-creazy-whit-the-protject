using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

/// <summary>
/// Creates the local database and its schema if they don't exist yet, and seeds
/// it from <see cref="DemoCatalog"/> so there is exactly one place the demo data
/// is written down. Runs once at startup - this is a demo catalog, not a
/// migration framework.
/// </summary>
public static class DbInitializer
{
    public static void EnsureCreatedAndSeeded(string connectionString)
    {
        EnsureDatabaseExists(connectionString);

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        RunSchemaScript(connection);
        SeedIfEmpty(connection);
    }

    private static void EnsureDatabaseExists(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = new SqlCommand(
            """
            IF DB_ID(@name) IS NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@name);
                EXEC (@sql);
            END
            """, connection);
        command.Parameters.AddWithValue("@name", databaseName);
        command.ExecuteNonQuery();
    }

    private static void RunSchemaScript(SqlConnection connection)
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "Sql", "schema.sql");
        var schemaSql = File.ReadAllText(schemaPath);

        using var command = new SqlCommand(schemaSql, connection);
        command.ExecuteNonQuery();
    }

    private static void SeedIfEmpty(SqlConnection connection)
    {
        using (var check = new SqlCommand("SELECT COUNT(*) FROM dbo.Products;", connection))
        {
            var count = (int)check.ExecuteScalar()!;
            if (count > 0) return;
        }

        var catalog = new DemoCatalog();

        foreach (var animal in catalog.Animals)
            InsertProduct(connection, animal.ProductId, productType: 1,
                animal.Name, nameEn: null, animal.Description, animal.DescriptionEn, animal.Price);

        foreach (var item in catalog.Accessories)
            InsertProduct(connection, item.ProductId, productType: 2,
                item.Name, item.NameEn, item.Description, item.DescriptionEn, item.Price);

        foreach (var animal in catalog.Animals)
            InsertAnimal(connection, animal);

        foreach (var item in catalog.Accessories)
            InsertStockProduct(connection, item);
    }

    private static void InsertProduct(
        SqlConnection connection, int productId, byte productType,
        string name, string? nameEn, string description, string? descriptionEn, decimal price)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.Products (ProductId, ProductType, Name, NameEn, Description, DescriptionEn, Price)
            VALUES (@ProductId, @ProductType, @Name, @NameEn, @Description, @DescriptionEn, @Price);
            """, connection);

        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@ProductType", productType);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@NameEn", (object?)nameEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@DescriptionEn", (object?)descriptionEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@Price", price);
        command.ExecuteNonQuery();
    }

    private static void InsertAnimal(SqlConnection connection, Animal animal)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.Animals
                (ProductId, Breed, BreedEn, Sex, DateOfBirth, Colour, ColourEn,
                 CoatPrimary, CoatSecondary, Status, BondedWithId, Personality, PersonalityEn, PhotoUrl)
            VALUES
                (@ProductId, @Breed, @BreedEn, @Sex, @DateOfBirth, @Colour, @ColourEn,
                 @CoatPrimary, @CoatSecondary, @Status, @BondedWithId, @Personality, @PersonalityEn, @PhotoUrl);
            """, connection);

        command.Parameters.AddWithValue("@ProductId", animal.ProductId);
        command.Parameters.AddWithValue("@Breed", animal.Breed);
        command.Parameters.AddWithValue("@BreedEn", (object?)animal.BreedEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@Sex", (byte)animal.Sex);
        command.Parameters.AddWithValue("@DateOfBirth", animal.DateOfBirth.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@Colour", animal.Colour);
        command.Parameters.AddWithValue("@ColourEn", (object?)animal.ColourEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@CoatPrimary", animal.CoatPrimary);
        command.Parameters.AddWithValue("@CoatSecondary", animal.CoatSecondary);
        command.Parameters.AddWithValue("@Status", (byte)animal.Status);
        command.Parameters.AddWithValue("@BondedWithId", (object?)animal.BondedWithId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Personality", animal.Personality);
        command.Parameters.AddWithValue("@PersonalityEn", (object?)animal.PersonalityEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@PhotoUrl", (object?)animal.PhotoUrl ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void InsertStockProduct(SqlConnection connection, StockProduct item)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.StockProducts (ProductId, Sku, Category, StockQuantity, Unit)
            VALUES (@ProductId, @Sku, @Category, @StockQuantity, @Unit);
            """, connection);

        command.Parameters.AddWithValue("@ProductId", item.ProductId);
        command.Parameters.AddWithValue("@Sku", item.Sku);
        command.Parameters.AddWithValue("@Category", (byte)item.Category);
        command.Parameters.AddWithValue("@StockQuantity", item.StockQuantity);
        command.Parameters.AddWithValue("@Unit", (object?)item.Unit ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
