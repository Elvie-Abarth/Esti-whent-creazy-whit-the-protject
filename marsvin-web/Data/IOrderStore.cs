using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

public sealed record CheckoutResult(bool Success, Order? Order, string? ErrorMessage)
{
    public static CheckoutResult Ok(Order order) => new(true, order, null);
    public static CheckoutResult Fail(string message) => new(false, null, message);
}

public interface IOrderStore
{
    /// <summary>
    /// Turns the user's current cart into an order. Re-checks stock/animal
    /// availability inside the transaction - the cart can go stale between
    /// "add to cart" and checkout - and fails the whole checkout rather than
    /// partially fulfilling it if anything no longer qualifies.
    /// </summary>
    CheckoutResult Checkout(int userId);

    /// <summary>Looks up an order, but only if it belongs to the given user (prevents one customer from viewing another's order by guessing an id).</summary>
    Order? FindForUser(int orderId, int userId);

    /// <summary>Every order this user has placed, most recent first.</summary>
    IReadOnlyList<Order> GetOrdersForUser(int userId);
}
