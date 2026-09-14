using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlOrderStore(string connectionString) : IOrderStore
{
    public CheckoutResult Checkout(int userId, DeliveryMethod deliveryMethod = DeliveryMethod.Pickup, string? shippingAddress = null)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var lines = LoadCartLines(connection, transaction, userId);
            if (lines.Count == 0)
                return CheckoutResult.Fail(new Bilingual("Din kurv er tom.", "Your cart is empty."));

            // A guinea pig can't go in a parcel, but an order can still mix an
            // animal with accessories - Shipping then means "ship whatever's
            // shippable, the animal still gets picked up in store regardless"
            // (see the confirmation page). Only reject Shipping outright when
            // there's nothing shippable in the order at all. Checked here
            // rather than trusted from the form, the same way stock/
            // availability is re-checked below instead of trusted from the cart.
            if (deliveryMethod == DeliveryMethod.Shipping && lines.All(l => l.IsAnimal))
            {
                transaction.Rollback();
                return CheckoutResult.Fail(new Bilingual(
                    "Der er intet at sende med fragt i denne ordre - vælg afhentning.",
                    "There's nothing to ship in this order - choose pickup."));
            }
            if (deliveryMethod == DeliveryMethod.Shipping && string.IsNullOrWhiteSpace(shippingAddress))
            {
                transaction.Rollback();
                return CheckoutResult.Fail(new Bilingual(
                    "Angiv en leveringsadresse for forsendelse.",
                    "Enter a delivery address for shipping."));
            }

            foreach (var line in lines)
            {
                var problem = ValidateLine(connection, transaction, line);
                if (problem is not null)
                {
                    transaction.Rollback();
                    return CheckoutResult.Fail(problem);
                }
            }

            var orderId = InsertOrder(connection, transaction, userId, lines, deliveryMethod,
                deliveryMethod == DeliveryMethod.Shipping ? shippingAddress!.Trim() : null);

            foreach (var line in lines)
                ApplyStockChange(connection, transaction, line);

            ClearCart(connection, transaction, userId);

            transaction.Commit();

            var order = FindForUser(orderId, userId)
                ?? throw new InvalidOperationException("Order vanished immediately after being created.");
            return CheckoutResult.Ok(order);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public Order? FindForUser(int orderId, int userId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var orderCommand = new SqlCommand(
            "SELECT OrderId, UserId, TotalPrice, CreatedAt, DeliveryMethod, ShippingAddress FROM dbo.Orders " +
            "WHERE OrderId = @OrderId AND UserId = @UserId;", connection);
        orderCommand.Parameters.AddWithValue("@OrderId", orderId);
        orderCommand.Parameters.AddWithValue("@UserId", userId);

        int? foundUserId;
        decimal totalPrice;
        DateTime createdAt;
        DeliveryMethod deliveryMethod;
        string? shippingAddress;
        using (var reader = orderCommand.ExecuteReader())
        {
            if (!reader.Read()) return null;
            var userIdOrdinal = reader.GetOrdinal("UserId");
            foundUserId = reader.IsDBNull(userIdOrdinal) ? null : reader.GetInt32(userIdOrdinal);
            totalPrice = reader.GetDecimal(reader.GetOrdinal("TotalPrice"));
            createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"));
            deliveryMethod = (DeliveryMethod)reader.GetByte(reader.GetOrdinal("DeliveryMethod"));
            var shippingAddressOrdinal = reader.GetOrdinal("ShippingAddress");
            shippingAddress = reader.IsDBNull(shippingAddressOrdinal) ? null : reader.GetString(shippingAddressOrdinal);
        }

        using var itemsCommand = new SqlCommand(
            "SELECT ProductId, ProductName, UnitPrice, Quantity, IsAnimal FROM dbo.OrderItems " +
            "WHERE OrderId = @OrderId ORDER BY OrderItemId;", connection);
        itemsCommand.Parameters.AddWithValue("@OrderId", orderId);

        var items = new List<OrderItem>();
        using (var reader = itemsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                items.Add(new OrderItem
                {
                    ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                    ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                    UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                    Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                    IsAnimal = reader.GetBoolean(reader.GetOrdinal("IsAnimal"))
                });
            }
        }

        return new Order
        {
            OrderId = orderId,
            UserId = foundUserId,
            TotalPrice = totalPrice,
            CreatedAt = createdAt,
            Items = items,
            DeliveryMethod = deliveryMethod,
            ShippingAddress = shippingAddress
        };
    }

    public IReadOnlyList<Order> GetOrdersForUser(int userId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        var orders = new List<Order>();
        using var orderCommand = new SqlCommand(
            // OrderId DESC as a tiebreaker: two checkouts in quick succession (easily
            // seconds or less apart for a real customer, let alone in automated tests)
            // can land in the same CreatedAt tick, and CreatedAt alone then sorts them
            // in whatever order the storage engine feels like, not necessarily recency.
            // OrderId is IDENTITY(1,1), so it's a reliable "definitely later" signal.
            "SELECT OrderId, TotalPrice, CreatedAt, DeliveryMethod, ShippingAddress FROM dbo.Orders " +
            "WHERE UserId = @UserId ORDER BY CreatedAt DESC, OrderId DESC;", connection);
        orderCommand.Parameters.AddWithValue("@UserId", userId);

        var headers = new List<(int OrderId, decimal TotalPrice, DateTime CreatedAt, DeliveryMethod DeliveryMethod, string? ShippingAddress)>();
        using (var reader = orderCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var shippingAddressOrdinal = reader.GetOrdinal("ShippingAddress");
                headers.Add((
                    reader.GetInt32(reader.GetOrdinal("OrderId")),
                    reader.GetDecimal(reader.GetOrdinal("TotalPrice")),
                    reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    (DeliveryMethod)reader.GetByte(reader.GetOrdinal("DeliveryMethod")),
                    reader.IsDBNull(shippingAddressOrdinal) ? null : reader.GetString(shippingAddressOrdinal)));
            }
        }

        foreach (var (orderId, totalPrice, createdAt, deliveryMethod, shippingAddress) in headers)
        {
            using var itemsCommand = new SqlCommand(
                "SELECT ProductId, ProductName, UnitPrice, Quantity, IsAnimal FROM dbo.OrderItems " +
                "WHERE OrderId = @OrderId ORDER BY OrderItemId;", connection);
            itemsCommand.Parameters.AddWithValue("@OrderId", orderId);

            var items = new List<OrderItem>();
            using (var reader = itemsCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    items.Add(new OrderItem
                    {
                        ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                        ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                        UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                        Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                        IsAnimal = reader.GetBoolean(reader.GetOrdinal("IsAnimal"))
                    });
                }
            }

            orders.Add(new Order
            {
                OrderId = orderId,
                UserId = userId,
                TotalPrice = totalPrice,
                CreatedAt = createdAt,
                Items = items,
                DeliveryMethod = deliveryMethod,
                ShippingAddress = shippingAddress
            });
        }

        return orders;
    }

    private static List<CartLine> LoadCartLines(SqlConnection connection, SqlTransaction transaction, int userId)
    {
        using var command = new SqlCommand(
            """
            SELECT c.ProductId, c.Quantity, p.Name, p.Price,
                   CASE WHEN a.ProductId IS NULL THEN 0 ELSE 1 END AS IsAnimal
            FROM dbo.CartItems c
            JOIN dbo.Products p ON p.ProductId = c.ProductId
            LEFT JOIN dbo.Animals a ON a.ProductId = p.ProductId
            WHERE c.UserId = @UserId;
            """, connection, transaction);
        command.Parameters.AddWithValue("@UserId", userId);

        var lines = new List<CartLine>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            lines.Add(new CartLine
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                ProductName = reader.GetString(reader.GetOrdinal("Name")),
                UnitPrice = reader.GetDecimal(reader.GetOrdinal("Price")),
                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                IsAnimal = reader.GetInt32(reader.GetOrdinal("IsAnimal")) == 1
            });
        }
        return lines;
    }

    /// <summary>Returns an error message if the line can no longer be fulfilled, otherwise null.</summary>
    private static string? ValidateLine(SqlConnection connection, SqlTransaction transaction, CartLine line)
    {
        if (line.IsAnimal)
        {
            using var command = new SqlCommand(
                "SELECT Status, DateOfBirth FROM dbo.Animals WHERE ProductId = @ProductId;",
                connection, transaction);
            command.Parameters.AddWithValue("@ProductId", line.ProductId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return new Bilingual($"{line.ProductName} findes ikke længere.", $"{line.ProductName} doesn't exist anymore.");

            var status = (AnimalStatus)reader.GetByte(reader.GetOrdinal("Status"));
            var dateOfBirth = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DateOfBirth")));
            var weeksOld = (DateOnly.FromDateTime(DateTime.Today).DayNumber - dateOfBirth.DayNumber) / 7;

            if (status != AnimalStatus.Available)
                return new Bilingual($"{line.ProductName} er ikke længere til salg.", $"{line.ProductName} is no longer for sale.");
            if (weeksOld < 4)
            {
                return new Bilingual(
                    $"{line.ProductName} er endnu ikke gammel nok til at flytte hjemmefra.",
                    $"{line.ProductName} isn't old enough to leave home yet.");
            }

            return null;
        }
        else
        {
            using var command = new SqlCommand(
                "SELECT StockQuantity FROM dbo.StockProducts WHERE ProductId = @ProductId;",
                connection, transaction);
            command.Parameters.AddWithValue("@ProductId", line.ProductId);

            var stock = (int?)command.ExecuteScalar();
            if (stock is null)
                return new Bilingual($"{line.ProductName} findes ikke længere.", $"{line.ProductName} doesn't exist anymore.");
            if (stock < line.Quantity)
            {
                return new Bilingual(
                    $"Der er ikke {line.Quantity} styk tilbage af {line.ProductName}.",
                    $"There aren't {line.Quantity} left of {line.ProductName}.");
            }

            return null;
        }
    }

    private static int InsertOrder(
        SqlConnection connection, SqlTransaction transaction, int userId, IReadOnlyList<CartLine> lines,
        DeliveryMethod deliveryMethod, string? shippingAddress)
    {
        var total = lines.Sum(l => l.LineTotal);

        using var orderCommand = new SqlCommand(
            """
            INSERT INTO dbo.Orders (UserId, TotalPrice, DeliveryMethod, ShippingAddress)
            OUTPUT INSERTED.OrderId
            VALUES (@UserId, @TotalPrice, @DeliveryMethod, @ShippingAddress);
            """, connection, transaction);
        orderCommand.Parameters.AddWithValue("@UserId", userId);
        orderCommand.Parameters.AddWithValue("@TotalPrice", total);
        orderCommand.Parameters.AddWithValue("@DeliveryMethod", (byte)deliveryMethod);
        orderCommand.Parameters.AddWithValue("@ShippingAddress", (object?)shippingAddress ?? DBNull.Value);
        var orderId = (int)orderCommand.ExecuteScalar()!;

        foreach (var line in lines)
        {
            using var itemCommand = new SqlCommand(
                """
                INSERT INTO dbo.OrderItems (OrderId, ProductId, ProductName, UnitPrice, Quantity, IsAnimal)
                VALUES (@OrderId, @ProductId, @ProductName, @UnitPrice, @Quantity, @IsAnimal);
                """, connection, transaction);
            itemCommand.Parameters.AddWithValue("@OrderId", orderId);
            itemCommand.Parameters.AddWithValue("@ProductId", line.ProductId);
            itemCommand.Parameters.AddWithValue("@ProductName", line.ProductName);
            itemCommand.Parameters.AddWithValue("@UnitPrice", line.UnitPrice);
            itemCommand.Parameters.AddWithValue("@Quantity", line.Quantity);
            itemCommand.Parameters.AddWithValue("@IsAnimal", line.IsAnimal);
            itemCommand.ExecuteNonQuery();
        }

        return orderId;
    }

    private static void ApplyStockChange(SqlConnection connection, SqlTransaction transaction, CartLine line)
    {
        if (line.IsAnimal)
        {
            using var command = new SqlCommand(
                "UPDATE dbo.Animals SET Status = @Sold WHERE ProductId = @ProductId;",
                connection, transaction);
            command.Parameters.AddWithValue("@Sold", (byte)AnimalStatus.Sold);
            command.Parameters.AddWithValue("@ProductId", line.ProductId);
            command.ExecuteNonQuery();
        }
        else
        {
            using var command = new SqlCommand(
                "UPDATE dbo.StockProducts SET StockQuantity = StockQuantity - @Quantity WHERE ProductId = @ProductId;",
                connection, transaction);
            command.Parameters.AddWithValue("@Quantity", line.Quantity);
            command.Parameters.AddWithValue("@ProductId", line.ProductId);
            command.ExecuteNonQuery();
        }
    }

    private static void ClearCart(SqlConnection connection, SqlTransaction transaction, int userId)
    {
        using var command = new SqlCommand(
            "DELETE FROM dbo.CartItems WHERE UserId = @UserId;", connection, transaction);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }
}
