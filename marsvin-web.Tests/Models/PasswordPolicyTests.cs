using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Models;

/// <summary>
/// The password rules the server actually enforces (and the strength meter
/// mirrors): at least 12 characters, upper and lower case, a digit, a symbol,
/// and not an obvious password - not a common one, the shop's own words, or
/// the person's own email or name.
/// </summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Fresh-Timothy-99!")]       // all five met
    [InlineData("Barn-Hay-Fleece-7?")]
    [InlineData("Jo-Is-Terrific-9!")]        // email "jo@..." is too short a fragment to flag
    public void Validate_AcceptsAStrongPassword(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password, "owner@example.com", "Owner Ownersen", english: false));
    }

    [Theory]
    [InlineData("Ab1!cdef", "12 tegn")]              // 8 chars - too short
    [InlineData("fresh-timothy-99!", "bogstaver")]   // no upper case
    [InlineData("FRESH-TIMOTHY-99!", "bogstaver")]   // no lower case
    [InlineData("Fresh-Timothy-XX!", "tal")]         // no digit
    [InlineData("FreshTimothy1234", "tegn")]         // no symbol
    public void Validate_RejectsAStructuralMiss_WithTheRightDanishReason(string password, string reasonFragment)
    {
        var problem = PasswordPolicy.Validate(password, null, null, english: false);

        Assert.NotNull(problem);
        Assert.Contains(reasonFragment, problem);
    }

    [Theory]
    [InlineData("Xx-Password-99!")]          // a blocklisted common password
    [InlineData("Marsvin-Shop-99!")]         // the shop's own name
    [InlineData("Qwerty-Fleece-9!")]         // keyboard run
    public void Validate_RejectsAnObviousPassword(string password)
    {
        var problem = PasswordPolicy.Validate(password, null, null, english: false);

        Assert.NotNull(problem);
        Assert.Contains("gætte", problem);               // "...too easy to guess..."
    }

    [Fact]
    public void Validate_RejectsAPasswordBuiltFromTheOwnEmail()
    {
        // Structurally fine, but it is the account's own address.
        Assert.NotNull(PasswordPolicy.Validate("Annabelle-Hay-9!", "annabelle@example.com", "Someone", english: false));
    }

    [Fact]
    public void Validate_RejectsAPasswordBuiltFromTheOwnName()
    {
        Assert.NotNull(PasswordPolicy.Validate("Annabelle-Hay-9!", "nothing@example.com", "Annabelle", english: false));
    }

    [Fact]
    public void Validate_WritesTheReasonInTheRequestedLanguage()
    {
        Assert.Contains("at least 12", PasswordPolicy.Validate("Ab1!cd", null, null, english: true));
        Assert.Contains("mindst 12", PasswordPolicy.Validate("Ab1!cd", null, null, english: false));
    }

    [Fact]
    public void IsObvious_IgnoresAnEmailFragmentShorterThanThreeCharacters()
    {
        // "jo" is too short to treat the whole password as "built from the email".
        Assert.False(PasswordPolicy.IsObvious("Jo-Is-Terrific-9!", "jo@example.com", null));
    }

    [Fact]
    public void IsObvious_IsCaseInsensitive()
    {
        Assert.True(PasswordPolicy.IsObvious("XX-PASSWORD-99!", null, null));
        Assert.True(PasswordPolicy.IsObvious("MARSVIN-is-best", null, null));
    }
}
