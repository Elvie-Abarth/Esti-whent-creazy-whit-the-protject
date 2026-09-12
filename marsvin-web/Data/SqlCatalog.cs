using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

/// <summary>
/// Reads the catalog from SQL Server (LocalDB in development) via plain ADO.NET -
/// no ORM, every query parameterised. Loads once at startup; this is a small
/// demo catalog, not a paged repository.
/// </summary>
public sealed class SqlCatalog : ICatalog
{
    public IReadOnlyList<Animal> Animals { get; }
    public IReadOnlyList<StockProduct> Accessories { get; }

    public SqlCatalog(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        Animals = LoadAnimals(connection);
        Accessories = LoadAccessories(connection);
    }

    public Animal? FindAnimal(int id) => Animals.FirstOrDefault(a => a.ProductId == id);

    public IEnumerable<Animal> AvailableAnimals() =>
        Animals.Where(a => a.Status == AnimalStatus.Available);

    public IEnumerable<IReadOnlyList<Animal>> AnimalGroups()
    {
        var seen = new HashSet<int>();
        foreach (var animal in Animals)
        {
            if (!seen.Add(animal.ProductId)) continue;

            var partner = animal.BondedWithId is int id ? FindAnimal(id) : null;
            if (partner is not null && seen.Add(partner.ProductId))
                yield return [animal, partner];
            else
                yield return [animal];
        }
    }

    private static List<Animal> LoadAnimals(SqlConnection connection)
    {
        const string sql = """
            SELECT p.ProductId, p.Name, p.Description, p.DescriptionEn, p.Price,
                   a.Breed, a.BreedEn, a.Sex, a.DateOfBirth, a.Colour, a.ColourEn,
                   a.CoatPrimary, a.CoatSecondary, a.Status, a.BondedWithId,
                   a.Personality, a.PersonalityEn, a.PhotoUrl
            FROM dbo.Products p
            JOIN dbo.Animals a ON a.ProductId = p.ProductId
            ORDER BY p.ProductId;
            """;

        var animals = new List<Animal>();
        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            animals.Add(new Animal
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                DescriptionEn = reader.GetNullableString("DescriptionEn"),
                Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                Breed = reader.GetString(reader.GetOrdinal("Breed")),
                BreedEn = reader.GetNullableString("BreedEn"),
                Sex = (Sex)reader.GetByte(reader.GetOrdinal("Sex")),
                DateOfBirth = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DateOfBirth"))),
                Colour = reader.GetString(reader.GetOrdinal("Colour")),
                ColourEn = reader.GetNullableString("ColourEn"),
                CoatPrimary = reader.GetString(reader.GetOrdinal("CoatPrimary")),
                CoatSecondary = reader.GetString(reader.GetOrdinal("CoatSecondary")),
                Status = (AnimalStatus)reader.GetByte(reader.GetOrdinal("Status")),
                BondedWithId = reader.GetNullableInt32("BondedWithId"),
                Personality = reader.GetString(reader.GetOrdinal("Personality")),
                PersonalityEn = reader.GetNullableString("PersonalityEn"),
                PhotoUrl = reader.GetNullableString("PhotoUrl")
            });
        }
        return animals;
    }

    private static List<StockProduct> LoadAccessories(SqlConnection connection)
    {
        const string sql = """
            SELECT p.ProductId, p.Name, p.NameEn, p.Description, p.DescriptionEn, p.Price,
                   s.Sku, s.Category, s.StockQuantity, s.Unit
            FROM dbo.Products p
            JOIN dbo.StockProducts s ON s.ProductId = p.ProductId
            ORDER BY p.ProductId;
            """;

        var accessories = new List<StockProduct>();
        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            accessories.Add(new StockProduct
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                NameEn = reader.GetNullableString("NameEn"),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                DescriptionEn = reader.GetNullableString("DescriptionEn"),
                Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                Sku = reader.GetString(reader.GetOrdinal("Sku")),
                Category = (AccessoryCategory)reader.GetByte(reader.GetOrdinal("Category")),
                StockQuantity = reader.GetInt32(reader.GetOrdinal("StockQuantity")),
                Unit = reader.GetNullableString("Unit")
            });
        }
        return accessories;
    }
}

internal static class SqlDataReaderExtensions
{
    public static string? GetNullableString(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int? GetNullableInt32(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
