using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

public class TotpTests
{
    [Fact]
    public void GenerateSecret_TwoCalls_ProduceDifferentSecrets()
    {
        Assert.NotEqual(Totp.GenerateSecret(), Totp.GenerateSecret());
    }

    [Fact]
    public void ValidateCode_CurrentCode_IsAccepted()
    {
        var secret = Totp.GenerateSecret();
        var code = TotpTestHelper.CurrentCode(secret);

        Assert.True(Totp.ValidateCode(secret, code));
    }

    [Fact]
    public void ValidateCode_WrongCode_IsRejected()
    {
        var secret = Totp.GenerateSecret();
        var wrong = TotpTestHelper.CurrentCode(secret) == "000000" ? "111111" : "000000";

        Assert.False(Totp.ValidateCode(secret, wrong));
    }

    [Fact]
    public void ValidateCode_DifferentSecret_IsRejected()
    {
        var secretA = Totp.GenerateSecret();
        var secretB = Totp.GenerateSecret();
        var codeForA = TotpTestHelper.CurrentCode(secretA);

        Assert.False(Totp.ValidateCode(secretB, codeForA));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]   // too short
    [InlineData("1234567")] // too long
    [InlineData("abcdef")]  // not digits
    public void ValidateCode_MalformedInput_IsRejectedWithoutThrowing(string? code)
    {
        var secret = Totp.GenerateSecret();

        Assert.False(Totp.ValidateCode(secret, code));
    }

    [Fact]
    public void BuildOtpAuthUri_IncludesIssuerAndAccountAndSecret()
    {
        var secret = Totp.GenerateSecret();

        var uri = Totp.BuildOtpAuthUri(secret, "person@example.com");

        Assert.StartsWith("otpauth://totp/Marsvin:", uri);
        Assert.Contains("secret=" + secret, uri);
        Assert.Contains("issuer=Marsvin", uri);
        Assert.Contains(Uri.EscapeDataString("person@example.com"), uri);
    }
}
