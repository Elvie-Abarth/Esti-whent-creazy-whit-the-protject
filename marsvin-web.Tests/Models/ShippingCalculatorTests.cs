using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Models;

public class ShippingCalculatorTests
{
    private static CartLine Line(int weightGrams, int quantity, bool isAnimal = false) => new()
    {
        ProductId = 1,
        ProductName = "x",
        UnitPrice = 10,
        Quantity = quantity,
        IsAnimal = isAnimal,
        WeightGrams = weightGrams
    };

    [Fact]
    public void ShippableWeight_MultipliesByQuantity_AndIgnoresAnimals()
    {
        var lines = new[] { Line(2_100, 2), Line(120, 3), Line(900, 1, isAnimal: true) };

        Assert.Equal(4_560, ShippingCalculator.ShippableWeightGrams(lines));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(20_000, 1)]
    [InlineData(20_001, 2)]
    [InlineData(40_000, 2)]
    [InlineData(40_001, 3)]
    public void ParcelCount_SplitsAtTwentyKilos(int weightGrams, int expectedParcels) =>
        Assert.Equal(expectedParcels, ShippingCalculator.ParcelCount(weightGrams));

    [Theory]
    [InlineData(ShippingCarrier.PostNord, 1_000, 55)]
    [InlineData(ShippingCarrier.PostNord, 1_001, 69)]
    [InlineData(ShippingCarrier.Gls, 5_000, 55)]
    [InlineData(ShippingCarrier.Gls, 10_000, 75)]
    [InlineData(ShippingCarrier.DaoPakkeshop, 120, 39)]
    [InlineData(ShippingCarrier.DaoPakkeshop, 20_000, 89)]
    public void Cost_FollowsTheCarriersWeightBrackets(ShippingCarrier carrier, int weightGrams, int expected) =>
        Assert.Equal(expected, ShippingCalculator.Cost(carrier, weightGrams));

    [Fact]
    public void Cost_ForAnOrderOverTwentyKilos_ChargesEachParcelOnItsOwnWeight()
    {
        // 32 kg -> 2 parcels of 16 kg each, both in the "up to 20 kg" bracket.
        Assert.Equal(2 * 119, ShippingCalculator.Cost(ShippingCarrier.PostNord, 32_000));
        // 24 kg -> 2 parcels of 12 kg; 42 kg -> 3 parcels of 14 kg.
        Assert.Equal(2 * 99, ShippingCalculator.Cost(ShippingCarrier.Gls, 24_000));
        Assert.Equal(3 * 89, ShippingCalculator.Cost(ShippingCarrier.DaoPakkeshop, 42_000));
    }

    [Fact]
    public void DeliveryWindow_CountsWeekdaysOnly()
    {
        // Friday 9 October 2026 - PostNord is 1-2 weekdays, so the weekend is skipped.
        var friday = new DateOnly(2026, 10, 9);

        var (earliest, latest) = ShippingCalculator.DeliveryWindow(ShippingCarrier.PostNord, friday);

        Assert.Equal(new DateOnly(2026, 10, 12), earliest); // Monday
        Assert.Equal(new DateOnly(2026, 10, 13), latest);   // Tuesday
    }

    [Fact]
    public void DeliveryWindow_MidWeek_IsJustTheNextDays()
    {
        var monday = new DateOnly(2026, 10, 5);

        var (earliest, latest) = ShippingCalculator.DeliveryWindow(ShippingCarrier.DaoPakkeshop, monday);

        Assert.Equal(new DateOnly(2026, 10, 7), earliest);
        Assert.Equal(new DateOnly(2026, 10, 9), latest);
    }

    [Fact]
    public void Quote_PutsItAllTogether()
    {
        var quote = ShippingCalculator.Quote(ShippingCarrier.Gls, 25_000, new DateOnly(2026, 10, 5));

        Assert.Equal(2, quote.ParcelCount);
        Assert.Equal(25_000, quote.WeightGrams);
        Assert.Equal(2 * 99, quote.Cost);
        Assert.Equal("6/10 - 8/10", quote.DeliveryWindowText);
    }

    [Theory]
    [InlineData(120, false, "120 g")]
    [InlineData(3_200, false, "3,2 kg")]
    [InlineData(3_200, true, "3.2 kg")]
    [InlineData(16_000, false, "16 kg")]
    public void FormatWeight_UsesGramsBelowAKilo_AndTheLanguagesOwnDecimalSeparator(int grams, bool english, string expected) =>
        Assert.Equal(expected, ShippingCalculator.FormatWeight(grams, english));

    [Fact]
    public void OnlyPostNordDeliversToTheDoor()
    {
        Assert.False(ShippingCarrier.PostNord.DeliversToParcelShop());
        Assert.True(ShippingCarrier.Gls.DeliversToParcelShop());
        Assert.True(ShippingCarrier.DaoPakkeshop.DeliversToParcelShop());
    }

    [Theory]
    [InlineData(OrderStatus.Sent, DeliveryMethod.Shipping, "Afsendt")]
    [InlineData(OrderStatus.Sent, DeliveryMethod.Pickup, "Klar til afhentning")]
    [InlineData(OrderStatus.Completed, DeliveryMethod.Shipping, "Leveret")]
    [InlineData(OrderStatus.Completed, DeliveryMethod.Pickup, "Afhentet")]
    public void OrderStatus_NamesTheLastTwoStepsByHowTheOrderIsDelivered(
        OrderStatus status, DeliveryMethod deliveryMethod, string expected) =>
        Assert.Equal(expected, status.DisplayName(deliveryMethod));
}
