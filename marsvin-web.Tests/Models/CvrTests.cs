using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Models;

public class CvrTests
{
    [Theory]
    [InlineData("12345674")]
    [InlineData("12 34 56 74")]
    [InlineData(" 12345674 ")]
    public void IsValid_AcceptsEightDigitsWithACorrectCheckDigit(string input) =>
        Assert.True(Cvr.IsValid(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678")]     // check digit wrong
    [InlineData("12345647")]     // two digits swapped
    [InlineData("1234567")]      // too short
    [InlineData("123456740")]    // too long
    [InlineData("02345674")]     // can't start with 0
    [InlineData("1234567a")]
    [InlineData("DK12345674")]   // letters aren't quietly dropped
    [InlineData("12345674; DROP TABLE Orders")]
    public void IsValid_RejectsEverythingElse(string? input) =>
        Assert.False(Cvr.IsValid(input));

    [Fact]
    public void Normalize_KeepsOnlyTheDigits() =>
        Assert.Equal("12345674", Cvr.Normalize("12 34 56 74"));

    [Fact]
    public void ShopKnowledge_NeverContainsARealStockCount()
    {
        // Customers only ever see "in stock / low / out" - the chat must not
        // know more than the page does. Product 116 has 5 in stock and a
        // price that doesn't contain a 5 followed by "på lager".
        var facts = ShopKnowledge.Build(new DemoCatalog());

        Assert.DoesNotContain(facts, f => f.Da.Contains("5 på lager") || f.En.Contains("5 in stock"));
        Assert.Contains(facts, f => f.Da.Contains("Modulbur, 3 etager") && f.Da.Contains("Få tilbage"));
    }

    [Fact]
    public void ShopKnowledge_IncludesTheFaq()
    {
        var facts = ShopKnowledge.Build(new DemoCatalog());

        Assert.All(FaqData.Entries, entry => Assert.Contains(facts, f => f.Da.Contains(entry.Answer)));
    }
}
