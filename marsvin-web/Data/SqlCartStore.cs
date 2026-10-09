using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

/// <summary>
/// A signed-in customer's cart in SQL Server (dbo.CartItems): one row per
/// product, with a quantity. Every statement is scoped to the customer's own
/// UserId, so one customer can never read or change another's cart.
/// (A guest's cart is SessionCartStore instead - see ICartStore.)
/// </summary>
public sealed class SqlCartStore(string connectionString) : ICartStore
{
    // The cart only stores which product and how many. Name, price, photo
    // and weight are joined in fresh from the catalog on every read, so the
    // cart always shows today's price - never one remembered from when the
    // item was added. The LEFT JOINs find out whether the product is an
    // animal or an accessory.
    public IReadOnlyList<CartLine> GetLines(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            SELECT c.ProductId, c.Quantity, p.Name, p.NameEn, p.Price,
                   CASE WHEN a.ProductId IS NULL THEN 0 ELSE 1 END AS IsAnimal,
                   COALESCE(a.PhotoUrl, sp.PhotoUrl) AS PhotoUrl,
                   COALESCE(sp.WeightGrams, 0) AS WeightGrams
            FROM dbo.CartItems c
            JOIN dbo.Products p ON p.ProductId = c.ProductId
            LEFT JOIN dbo.Animals a ON a.ProductId = p.ProductId
            LEFT JOIN dbo.StockProducts sp ON sp.ProductId = p.ProductId
            WHERE c.UserId = @UserId
            ORDER BY c.CartItemId;
            """, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var lines = new List<CartLine>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var nameEnOrdinal = reader.GetOrdinal("NameEn");
            var photoUrlOrdinal = reader.GetOrdinal("PhotoUrl");
            lines.Add(new CartLine
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                ProductName = reader.GetString(reader.GetOrdinal("Name")),
                ProductNameEn = reader.IsDBNull(nameEnOrdinal) ? null : reader.GetString(nameEnOrdinal),
                UnitPrice = reader.GetDecimal(reader.GetOrdinal("Price")),
                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                IsAnimal = reader.GetInt32(reader.GetOrdinal("IsAnimal")) == 1,
                PhotoUrl = reader.IsDBNull(photoUrlOrdinal) ? null : reader.GetString(photoUrlOrdinal),
                WeightGrams = reader.GetInt32(reader.GetOrdinal("WeightGrams"))
            });
        }
        return lines;
    }

    // One statement that either raises the quantity of an existing line or
    // inserts a new one (MERGE = "upsert"). Doing it as "look first, then
    // insert or update" in two steps could add the same product twice if two
    // requests arrived together; the UNIQUE (UserId, ProductId) constraint
    // on the table is the backstop.
    public void AddOrIncrement(int userId, int productId, int quantity)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            MERGE dbo.CartItems AS target
            USING (SELECT @UserId AS UserId, @ProductId AS ProductId) AS source
                ON target.UserId = source.UserId AND target.ProductId = source.ProductId
            WHEN MATCHED THEN
                UPDATE SET Quantity = target.Quantity + @Quantity
            WHEN NOT MATCHED THEN
                INSERT (UserId, ProductId, Quantity) VALUES (@UserId, @ProductId, @Quantity);
            """, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.ExecuteNonQuery();
    }

    public void SetQuantity(int userId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            RemoveLine(userId, productId);
            return;
        }

        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.CartItems SET Quantity = @Quantity WHERE UserId = @UserId AND ProductId = @ProductId;", connection);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.ExecuteNonQuery();
    }

    public void RemoveLine(int userId, int productId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "DELETE FROM dbo.CartItems WHERE UserId = @UserId AND ProductId = @ProductId;", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.ExecuteNonQuery();
    }

    public void Clear(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "DELETE FROM dbo.CartItems WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}
