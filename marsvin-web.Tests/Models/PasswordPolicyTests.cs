using MarsvinWebExample.Data;
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
    [InlineData("Xx-Password-99!")]          // contains the literal word "password"
    [InlineData("Marsvin-Shop-99!")]         // the shop's own name
    [InlineData("Butik-Fleece-99!")]         // another shop-specific word (butik)
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

    [Theory]
    [InlineData(UserRole.Customer, 12)]
    [InlineData(UserRole.Employee, 12)]
    [InlineData(UserRole.Admin, 15)]
    public void MinLengthFor_IsFifteenOnlyForAnAdmin(UserRole role, int expected)
    {
        Assert.Equal(expected, PasswordPolicy.MinLengthFor(role));
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    public void Validate_AcceptsAThirteenCharacterPassword_ForANonAdmin(UserRole role)
    {
        // Meets all five rules and is 13 characters - fine for a customer or
        // an employee, whose minimum is 12.
        Assert.Null(PasswordPolicy.Validate("Fresh-Hay-9b!", null, null, english: false, role));
    }

    [Fact]
    public void Validate_RejectsTheSameThirteenCharacterPassword_ForAnAdmin()
    {
        // The admin minimum is 15, so a 13-character password an employee may
        // use is refused for an admin, with the admin number in the message.
        var problem = PasswordPolicy.Validate("Fresh-Hay-9b!", null, null, english: true, UserRole.Admin);

        Assert.NotNull(problem);
        Assert.Contains("at least 15", problem);
    }

    [Fact]
    public void Validate_AcceptsAFifteenPlusCharacterPassword_ForAnAdmin()
    {
        // Eighteen characters, all five rules met - accepted for an admin.
        Assert.Null(PasswordPolicy.Validate("Barn-Hay-Fleece-7?", null, null, english: false, UserRole.Admin));
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

    // ---- the generated demo credentials must not break our own policy -------

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Customer)]
    public void DemoCredentials_Generate_AlwaysPassesThePolicyForItsRole(UserRole role)
    {
        // The seed's random demo passwords (DemoCredentials.Generate) must never
        // produce something the server would itself reject - run it many times
        // so a rare bad draw can't slip through unnoticed.
        for (var i = 0; i < 100; i++)
        {
            var password = DemoCredentials.Generate(role);
            Assert.Null(PasswordPolicy.Validate(password, null, null, english: false, role));
        }
    }

    // ---- the large exact-match common-password list (ASVS 6.2.4) ------------

    [Fact]
    public void CommonPasswordList_IsLoadedFromTheFile()
    {
        // If the file wasn't copied to the test output, this is 0 and the two
        // tests below would fail for the wrong reason - so assert it loaded.
        Assert.True(CommonPasswordList.Count > 100);
    }

    [Theory]
    [InlineData("letmein")]
    [InlineData("Dragon")]        // exact match is case-insensitive
    [InlineData("qwerty")]
    public void IsObvious_RejectsACommonPasswordExactly(string password)
    {
        Assert.True(PasswordPolicy.IsObvious(password, null, null));
    }

    [Fact]
    public void IsObvious_AllowsALongPassphraseThatMerelyContainsACommonWord()
    {
        // "dragon" and "monkey" are on the common list, but only as whole
        // passwords; a long unique passphrase that contains one is fine.
        Assert.False(PasswordPolicy.IsObvious("Happy-Monkey-Garden-River-7!", null, null));
        Assert.False(PasswordPolicy.IsObvious("Correct-Horse-Dragon-Battery-9!", null, null));
    }

    [Fact]
    public void IsObvious_StillRejectsAShopWordAnywhere()
    {
        // Shop-specific words stay a substring match, common or not.
        Assert.True(PasswordPolicy.IsObvious("Guineapig-Garden-7!", null, null));
        Assert.True(PasswordPolicy.IsObvious("My-Butik-Login-9!", null, null));
    }
}
