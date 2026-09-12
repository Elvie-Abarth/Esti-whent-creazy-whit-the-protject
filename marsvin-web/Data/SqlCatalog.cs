using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

/// <summary>
/// Reads and writes the catalog in SQL Server (LocalDB in development) via
/// plain ADO.NET - no ORM, every query parameterised. Queries fresh on every
/// access rather than caching, since ICatalogAdmin lets the catalog change
/// mid-run (admin/employee edits) - registered per-request in DI, not as a
/// singleton, for the same reason.
/// </summary>
public sealed class SqlCatalog(string connectionString) : ICatalog, ICatalogAdmin
{
    public IReadOnlyList<Animal> Animals
    {
        get
        {
            using var connection = Open();
            return LoadAnimals(connection);
        }
    }

    public IReadOnlyList<StockProduct> Accessories
    {
        get
        {
            using var connection = Open();
            return LoadAccessories(connection);
        }
    }

    public Animal? FindAnimal(int id) => Animals.FirstOrDefault(a => a.ProductId == id);

    public IEnumerable<Animal> AvailableAnimals() =>
        Animals.Where(a => a.Status == AnimalStatus.Available);

    public IEnumerable<IReadOnlyList<Animal>> AnimalGroups()
    {
        var animals = Animals;
        var byId = animals.ToDictionary(a => a.ProductId);
        var seen = new HashSet<int>();
        foreach (var animal in animals)
        {
            if (!seen.Add(animal.ProductId)) continue;

            var partner = animal.BondedWithId is int id && byId.TryGetValue(id, out var found) ? found : null;
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

    public void CreateAnimal(Animal animal)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        var productId = NextProductId(connection, transaction);
        InsertProduct(connection, transaction, productId, productType: 1,
            animal.Name, nameEn: null, animal.Description, animal.DescriptionEn, animal.Price);

        using var command = new SqlCommand(
            """
            INSERT INTO dbo.Animals
                (ProductId, Breed, BreedEn, Sex, DateOfBirth, Colour, ColourEn,
                 CoatPrimary, CoatSecondary, Status, BondedWithId, Personality, PersonalityEn, PhotoUrl)
            VALUES
                (@ProductId, @Breed, @BreedEn, @Sex, @DateOfBirth, @Colour, @ColourEn,
                 @CoatPrimary, @CoatSecondary, @Status, @BondedWithId, @Personality, @PersonalityEn, @PhotoUrl);
            """, connection, transaction);
        command.Parameters.AddWithValue("@ProductId", productId);
        BindAnimal(command, animal);
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    public void UpdateAnimal(Animal animal)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        UpdateProduct(connection, transaction, animal.ProductId,
            animal.Name, nameEn: null, animal.Description, animal.DescriptionEn, animal.Price);

        using var command = new SqlCommand(
            """
            UPDATE dbo.Animals SET
                Breed = @Breed, BreedEn = @BreedEn, Sex = @Sex, DateOfBirth = @DateOfBirth,
                Colour = @Colour, ColourEn = @ColourEn, CoatPrimary = @CoatPrimary,
                CoatSecondary = @CoatSecondary, Status = @Status, BondedWithId = @BondedWithId,
                Personality = @Personality, PersonalityEn = @PersonalityEn, PhotoUrl = @PhotoUrl
            WHERE ProductId = @ProductId;
            """, connection, transaction);
        command.Parameters.AddWithValue("@ProductId", animal.ProductId);
        BindAnimal(command, animal);
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    public void DeleteAnimal(int productId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var command = new SqlCommand(
            "DELETE FROM dbo.Animals WHERE ProductId = @ProductId;", connection, transaction))
        {
            command.Parameters.AddWithValue("@ProductId", productId);
            command.ExecuteNonQuery();
        }
        DeleteProduct(connection, transaction, productId);

        transaction.Commit();
    }

    public void CreateStockProduct(StockProduct product)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        var productId = NextProductId(connection, transaction);
        InsertProduct(connection, transaction, productId, productType: 2,
            product.Name, product.NameEn, product.Description, product.DescriptionEn, product.Price);

        using var command = new SqlCommand(
            """
            INSERT INTO dbo.StockProducts (ProductId, Sku, Category, StockQuantity, Unit)
            VALUES (@ProductId, @Sku, @Category, @StockQuantity, @Unit);
            """, connection, transaction);
        command.Parameters.AddWithValue("@ProductId", productId);
        BindStockProduct(command, product);
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    public void UpdateStockProduct(StockProduct product)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        UpdateProduct(connection, transaction, product.ProductId,
            product.Name, product.NameEn, product.Description, product.DescriptionEn, product.Price);

        using var command = new SqlCommand(
            """
            UPDATE dbo.StockProducts SET
                Sku = @Sku, Category = @Category, StockQuantity = @StockQuantity, Unit = @Unit
            WHERE ProductId = @ProductId;
            """, connection, transaction);
        command.Parameters.AddWithValue("@ProductId", product.ProductId);
        BindStockProduct(command, product);
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    public void DeleteStockProduct(int productId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var command = new SqlCommand(
            "DELETE FROM dbo.StockProducts WHERE ProductId = @ProductId;", connection, transaction))
        {
            command.Parameters.AddWithValue("@ProductId", productId);
            command.ExecuteNonQuery();
        }
        DeleteProduct(connection, transaction, productId);

        transaction.Commit();
    }

    public void UpdateStockQuantity(int productId, int quantity)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.StockProducts SET StockQuantity = @Quantity WHERE ProductId = @ProductId;", connection);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.ExecuteNonQuery();
    }

    public void UpdateAnimalStatus(int productId, AnimalStatus status)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Animals SET Status = @Status WHERE ProductId = @ProductId;", connection);
        command.Parameters.AddWithValue("@Status", (byte)status);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.ExecuteNonQuery();
    }

    private static int NextProductId(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = new SqlCommand(
            "SELECT ISNULL(MAX(ProductId), 0) + 1 FROM dbo.Products WITH (TABLOCKX, HOLDLOCK);",
            connection, transaction);
        return (int)command.ExecuteScalar()!;
    }

    private static void InsertProduct(
        SqlConnection connection, SqlTransaction transaction, int productId, byte productType,
        string name, string? nameEn, string description, string? descriptionEn, decimal price)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.Products (ProductId, ProductType, Name, NameEn, Description, DescriptionEn, Price)
            VALUES (@ProductId, @ProductType, @Name, @NameEn, @Description, @DescriptionEn, @Price);
            """, connection, transaction);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@ProductType", productType);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@NameEn", (object?)nameEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@DescriptionEn", (object?)descriptionEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@Price", price);
        command.ExecuteNonQuery();
    }

    private static void UpdateProduct(
        SqlConnection connection, SqlTransaction transaction, int productId,
        string name, string? nameEn, string description, string? descriptionEn, decimal price)
    {
        using var command = new SqlCommand(
            """
            UPDATE dbo.Products SET
                Name = @Name, NameEn = @NameEn, Description = @Description,
                DescriptionEn = @DescriptionEn, Price = @Price
            WHERE ProductId = @ProductId;
            """, connection, transaction);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@NameEn", (object?)nameEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@DescriptionEn", (object?)descriptionEn ?? DBNull.Value);
        command.Parameters.AddWithValue("@Price", price);
        command.ExecuteNonQuery();
    }

    private static void DeleteProduct(SqlConnection connection, SqlTransaction transaction, int productId)
    {
        using var command = new SqlCommand(
            "DELETE FROM dbo.Products WHERE ProductId = @ProductId;", connection, transaction);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.ExecuteNonQuery();
    }

    private static void BindAnimal(SqlCommand command, Animal animal)
    {
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
    }

    private static void BindStockProduct(SqlCommand command, StockProduct product)
    {
        command.Parameters.AddWithValue("@Sku", product.Sku);
        command.Parameters.AddWithValue("@Category", (byte)product.Category);
        command.Parameters.AddWithValue("@StockQuantity", product.StockQuantity);
        command.Parameters.AddWithValue("@Unit", (object?)product.Unit ?? DBNull.Value);
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
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
