using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Models;

public class BilingualTests
{
    [Fact]
    public void ImplicitStringConversion_ThenParse_RoundTripsBothLanguages()
    {
        var bilingual = new Bilingual("Dansk tekst.", "English text.");

        string encoded = bilingual;
        var parsed = Bilingual.Parse(encoded);

        Assert.Equal("Dansk tekst.", parsed.Da);
        Assert.Equal("English text.", parsed.En);
    }

    [Fact]
    public void Parse_PlainUnencodedString_FallsBackToTheSameTextForBothLanguages()
    {
        // A message that was never wrapped in Bilingual shouldn't crash the
        // toast - it just shows the same text regardless of language.
        var parsed = Bilingual.Parse("Just some plain string.");

        Assert.Equal("Just some plain string.", parsed.Da);
        Assert.Equal("Just some plain string.", parsed.En);
    }

    [Fact]
    public void RoundTrip_PreservesOrdinaryPunctuationInRealMessages()
    {
        // Sanity check that ordinary punctuation in real messages (colons,
        // dashes, parentheses, digits) doesn't get mistaken for the separator.
        var bilingual = new Bilingual(
            "Der er ikke 3 styk tilbage af Høpose (2 kg).",
            "There aren't 3 left of Hay bag (2 kg).");

        string encoded = bilingual;
        var parsed = Bilingual.Parse(encoded);

        Assert.Equal(bilingual.Da, parsed.Da);
        Assert.Equal(bilingual.En, parsed.En);
    }
}
