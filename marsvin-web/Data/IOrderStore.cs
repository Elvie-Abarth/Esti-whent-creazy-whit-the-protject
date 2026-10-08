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
    ///
    /// userId is nullable for a guest checkout (no account at all, not even
    /// a deleted one) - guestName/guestEmail are required in that case
    /// instead, since there's no Users row to pull a name/email from for the
    /// confirmation email or the admin order list. A logged-in checkout's
    /// cart is loaded straight from dbo.CartItems inside the same
    /// transaction as the order itself; a guest's cart isn't in SQL at all
    /// (it lives in the caller's own session-backed ICartStore), so
    /// guestLines carries the already-resolved lines in that case instead
    /// - required (and ignored otherwise) exactly when userId is null.
    /// </summary>
    CheckoutResult Checkout(int? userId, DeliveryMethod deliveryMethod = DeliveryMethod.Pickup, string? shippingAddress = null,
        ShippingCarrier? shippingCarrier = null, PaymentMethod paymentMethod = PaymentMethod.Card,
        string? guestName = null, string? guestEmail = null, IReadOnlyList<CartLine>? guestLines = null,
        string? contactPhone = null, bool ageConfirmed = false,
        string? companyName = null, string? companyCvr = null);

    /// <summary>Looks up an order, but only if it belongs to the given user (prevents one customer from viewing another's order by guessing an id).</summary>
    Order? FindForUser(int orderId, int userId);

    /// <summary>
    /// Looks up an order by id alone, with no ownership check at all - unlike
    /// FindForUser, safe to call only once the caller has already verified
    /// some other way that whoever's asking is allowed to see it (see
    /// Cart/Confirmation's guest path, gated on a one-time id stamped into
    /// that same browser's own session right after its own checkout).
    /// </summary>
    Order? FindById(int orderId);

    /// <summary>Every order this user has placed, most recent first.</summary>
    IReadOnlyList<Order> GetOrdersForUser(int userId);

    /// <summary>Every order ever placed, across every customer, most recent first - the admin order list.</summary>
    IReadOnlyList<Order> GetAllOrders();

    /// <summary>
    /// Staff moving an order along (see Admin/Orders), and recording the
    /// carrier's tracking number alongside. Never into or out of Cancelled -
    /// that's <see cref="Cancel"/>, which also puts the stock back. False if
    /// nothing was changed.
    /// </summary>
    bool UpdateStatus(int orderId, OrderStatus status, string? trackingNumber = null);

    /// <summary>
    /// Calls an order off and puts what was in it back on sale: accessory
    /// stock is added back, a guinea pig goes from Sold to Available again.
    /// Only possible while the order hasn't got further than
    /// <paramref name="latestCancellableStatus"/> - Placed for the buyer
    /// themselves, Processing for staff; never once it has been sent. The
    /// status check and the restock happen in one transaction, so two
    /// cancel requests at once can't put the stock back twice. False if the
    /// order doesn't exist or is already too far along.
    /// </summary>
    bool Cancel(int orderId, OrderStatus latestCancellableStatus);

    /// <summary>
    /// Hands a guest order over to an account, so it shows up in that
    /// account's order history - used when a guest creates an account right
    /// after checking out. Only ever touches an order that is still a guest
    /// order placed with this same email address; the caller is additionally
    /// responsible for knowing the order came from this browser (the same
    /// session stamp that lets a guest view their own receipt). False if
    /// nothing was claimed.
    /// </summary>
    bool ClaimGuestOrder(int orderId, int userId, string email);
}
