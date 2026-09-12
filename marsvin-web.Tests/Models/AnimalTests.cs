using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Models;

public class AnimalTests
{
    private static Animal MakeAnimal(int weeksOld, AnimalStatus status = AnimalStatus.Available, Sex sex = Sex.Boar) => new()
    {
        ProductId = 1,
        Name = "Test",
        Breed = "Abyssinier",
        Sex = sex,
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(-7 * weeksOld)),
        Colour = "Rød",
        Price = 100,
        CoatPrimary = "#000000",
        CoatSecondary = "#ffffff",
        Status = status,
        Description = "Test description"
    };

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(20, true)]
    public void IsOldEnoughToSell_UsesFourWeekThreshold(int weeksOld, bool expected)
    {
        var animal = MakeAnimal(weeksOld);

        Assert.Equal(expected, animal.IsOldEnoughToSell());
    }

    [Fact]
    public void AgeText_UnderSixteenWeeks_IsShownInWeeks()
    {
        var animal = MakeAnimal(10);

        Assert.Equal("10 uger", animal.AgeText);
        Assert.Equal("10 weeks", animal.AgeTextEn);
    }

    [Fact]
    public void AgeText_AtLeastSixteenWeeks_IsShownInMonths()
    {
        var animal = MakeAnimal(20);

        Assert.Equal("5 måneder", animal.AgeText);
        Assert.Equal("5 months", animal.AgeTextEn);
    }

    [Fact]
    public void AgeText_TwoYearsOrOlder_IsShownInYears()
    {
        var animal = MakeAnimal(24 * 4);

        Assert.Equal("2 år", animal.AgeText);
        Assert.Equal("2 years", animal.AgeTextEn);
    }

    [Theory]
    [InlineData(Sex.Boar, "Han", "Male")]
    [InlineData(Sex.Sow, "Hun", "Female")]
    public void SexName_MatchesSex(Sex sex, string expectedDa, string expectedEn)
    {
        var animal = MakeAnimal(10, sex: sex);

        Assert.Equal(expectedDa, animal.SexName);
        Assert.Equal(expectedEn, animal.SexNameEn);
    }

    [Theory]
    [InlineData(AnimalStatus.Available, "Ledig", "Available")]
    [InlineData(AnimalStatus.Reserved, "Reserveret", "Reserved")]
    [InlineData(AnimalStatus.Sold, "Solgt", "Sold")]
    [InlineData(AnimalStatus.NotForSale, "Ikke til salg", "Not for sale")]
    public void StatusText_MatchesStatus(AnimalStatus status, string expectedDa, string expectedEn)
    {
        var animal = MakeAnimal(10, status);

        Assert.Equal(expectedDa, animal.StatusText);
        Assert.Equal(expectedEn, animal.StatusTextEn);
    }

    [Fact]
    public void CanBeAddedToCart_RequiresAvailableOldEnoughAndSingleQuantity()
    {
        var available = MakeAnimal(10, AnimalStatus.Available);
        var reserved = MakeAnimal(10, AnimalStatus.Reserved);
        var tooYoung = MakeAnimal(2, AnimalStatus.Available);

        Assert.True(available.CanBeAddedToCart(1));
        Assert.False(available.CanBeAddedToCart(2));
        Assert.False(reserved.CanBeAddedToCart(1));
        Assert.False(tooYoung.CanBeAddedToCart(1));
    }
}
