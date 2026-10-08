using System.Globalization;

namespace MarsvinWebExample.Models;

/// <summary>
/// What shipping an order with one carrier comes to: price, how many parcels
/// it's split into, and when it should arrive. Shown per carrier on the
/// payment page before the buyer chooses, and re-computed (never taken from
/// the form) by SqlOrderStore.Checkout when the order is actually placed.
/// </summary>
public sealed record ShippingQuote(
    ShippingCarrier Carrier, int WeightGrams, int ParcelCount, decimal Cost,
    DateOnly EarliestDelivery, DateOnly LatestDelivery)
{
    public string DeliveryWindowText => ShippingCalculator.FormatWindow(EarliestDelivery, LatestDelivery);
}

/// <summary>
/// Demo shipping rules - the rates and delivery times are made up to be
/// plausible, not fetched from the carriers (a real shop would get both, and
/// the actual list of parcel shops near an address, from each carrier's own
/// API). Everything is derived from two inputs only: the total weight of the
/// shippable lines, and the carrier.
/// </summary>
public static class ShippingCalculator
{
    /// <summary>Heaviest single parcel any of the three carriers accepts on a standard consumer rate.</summary>
    public const int MaxParcelGrams = 20_000;

    /// <summary>Guinea pigs are never shipped (see Betaling &amp; levering), so they never count towards the parcel's weight.</summary>
    public static int ShippableWeightGrams(IEnumerable<CartLine> lines) =>
        lines.Where(l => !l.IsAnimal).Sum(l => l.WeightGrams * l.Quantity);

    public static int ParcelCount(int weightGrams) =>
        Math.Max(1, (int)Math.Ceiling(weightGrams / (double)MaxParcelGrams));

    /// <summary>Each parcel is priced on its own weight bracket, with the order's weight spread evenly across them.</summary>
    public static decimal Cost(ShippingCarrier carrier, int weightGrams)
    {
        var parcels = ParcelCount(weightGrams);
        var gramsPerParcel = (int)Math.Ceiling(weightGrams / (double)parcels);
        return parcels * ParcelPrice(carrier, gramsPerParcel);
    }

    private static decimal ParcelPrice(ShippingCarrier carrier, int grams)
    {
        // Columns: up to 1 kg, 5 kg, 10 kg, 20 kg. Home delivery (PostNord)
        // costs more than collecting from a parcel shop yourself.
        decimal[] prices = carrier switch
        {
            ShippingCarrier.PostNord => [55, 69, 89, 119],
            ShippingCarrier.Gls => [45, 55, 75, 99],
            _ => [39, 45, 65, 89]
        };
        return grams switch
        {
            <= 1_000 => prices[0],
            <= 5_000 => prices[1],
            <= 10_000 => prices[2],
            _ => prices[3]
        };
    }

    /// <summary>Business days from order to delivery, earliest and latest.</summary>
    public static (int Min, int Max) BusinessDays(ShippingCarrier carrier) => carrier switch
    {
        ShippingCarrier.PostNord => (1, 2),
        ShippingCarrier.Gls => (1, 3),
        _ => (2, 4)
    };

    public static (DateOnly Earliest, DateOnly Latest) DeliveryWindow(ShippingCarrier carrier, DateOnly orderDate)
    {
        var (min, max) = BusinessDays(carrier);
        return (AddBusinessDays(orderDate, min), AddBusinessDays(orderDate, max));
    }

    /// <summary>Shipping is free once the shippable items (never the animals - they aren't shipped) come to this much.</summary>
    public const decimal FreeShippingThreshold = 499m;

    public static decimal ShippableTotal(IEnumerable<CartLine> lines) =>
        lines.Where(l => !l.IsAnimal).Sum(l => l.LineTotal);

    /// <param name="shippableItemsTotal">What the shipped items cost together - at or above <see cref="FreeShippingThreshold"/> the shipping is free, whatever it weighs.</param>
    public static ShippingQuote Quote(ShippingCarrier carrier, int weightGrams, DateOnly orderDate, decimal shippableItemsTotal = 0)
    {
        var (earliest, latest) = DeliveryWindow(carrier, orderDate);
        var cost = shippableItemsTotal >= FreeShippingThreshold ? 0 : Cost(carrier, weightGrams);
        return new ShippingQuote(carrier, weightGrams, ParcelCount(weightGrams), cost, earliest, latest);
    }

    // No carrier delivers on a Saturday or Sunday at these rates. Public
    // holidays aren't accounted for - an estimate, labelled as one.
    private static DateOnly AddBusinessDays(DateOnly date, int days)
    {
        while (days > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days--;
        }
        return date;
    }

    // day/month in digits rather than a month name: reads the same in the
    // Danish and English versions of a page, whatever culture the server runs under.
    public static string FormatWindow(DateOnly earliest, DateOnly latest) =>
        earliest == latest
            ? $"{earliest.Day}/{earliest.Month}"
            : $"{earliest.Day}/{earliest.Month} - {latest.Day}/{latest.Month}";

    /// <summary>"3,2 kg" (Danish) / "3.2 kg" (English); grams below one kilo.</summary>
    public static string FormatWeight(int grams, bool english = false) =>
        grams < 1_000
            ? $"{grams} g"
            : (grams / 1000m).ToString("0.#", CultureInfo.GetCultureInfo(english ? "en-GB" : "da-DK")) + " kg";
}
