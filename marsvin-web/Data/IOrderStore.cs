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
    /// partially fulfilling it if anything no longer qualifies. Shipping ships
    /// whatever's shippable in the cart - a guinea pig always still needs a
    /// separate in-store pickup regardless, so Shipping is only rejected
    /// outright when there's nothing shippable at all (the cart is only
    /// animals); a mixed cart succeeds and still sells the animal. Checked
    /// here rather than trusted from whatever the submitted form claims.
    /// Defaults to Pickup with no address so every pre-existing call site
    /// keeps working unchanged. A Shipping order with no carrier given
    /// defaults to PostNord rather than failing - unlike the address, a
    /// carrier is always presented pre-selected on the form, so a missing
    /// one only ever happens from an old call site or a tampered POST.
    /// </summary>
    CheckoutResult Checkout(int userId, DeliveryMethod deliveryMethod = DeliveryMethod.Pickup, string? shippingAddress = null,
        ShippingCarrier? shippingCarrier = null, PaymentMethod paymentMethod = PaymentMethod.Card);

    /// <summary>Looks up an order, but only if it belongs to the given user (prevents one customer from viewing another's order by guessing an id).</summary>
    Order? FindForUser(int orderId, int userId);

    /// <summary>Every order this user has placed, most recent first.</summary>
    IReadOnlyList<Order> GetOrdersForUser(int userId);

    /// <summary>Every order ever placed, across every customer, most recent first - the admin order list.</summary>
    IReadOnlyList<Order> GetAllOrders();
}
