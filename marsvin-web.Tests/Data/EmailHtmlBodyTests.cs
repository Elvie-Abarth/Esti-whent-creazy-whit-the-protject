using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

public class EmailHtmlBodyTests
{
    [Fact]
    public void Build_UrlInBody_BecomesAClickableAnchor()
    {
        var html = EmailHtmlBody.Build("Open this link:\nhttp://localhost:5080/Account/ResetPassword?token=ABC123\nThanks.");

        Assert.Contains(
            """<a href="http://localhost:5080/Account/ResetPassword?token=ABC123" style="color:#2C4327;">http://localhost:5080/Account/ResetPassword?token=ABC123</a>""",
            html);
    }

    [Fact]
    public void Build_TextAroundTheUrl_IsStillPresentAndEncoded()
    {
        var html = EmailHtmlBody.Build("Hej <script>, se linket her: https://example.com/x");

        Assert.Contains("Hej &lt;script&gt;, se linket her:", html);
        Assert.Contains("""<a href="https://example.com/x" style="color:#2C4327;">https://example.com/x</a>""", html);
    }

    [Fact]
    public void Build_NoUrl_StillHtmlEncodesAndConvertsNewlines()
    {
        var html = EmailHtmlBody.Build("Line one <b>\nLine two & more");

        Assert.Equal("Line one &lt;b&gt;<br>Line two &amp; more", html);
    }

    [Fact]
    public void Build_TwoUrlsInOneBody_BothBecomeLinks()
    {
        var html = EmailHtmlBody.Build("First: http://a.example/1 then http://b.example/2");

        Assert.Contains("href=\"http://a.example/1\"", html);
        Assert.Contains("href=\"http://b.example/2\"", html);
    }

    [Fact]
    public void Build_MaliciousDisplayNameNextToAUrl_CannotBreakOutOfTheAnchorMarkup()
    {
        // A display name is user-controlled (see Register/Profile) - make sure
        // it can't inject its own tag right next to the real link.
        var html = EmailHtmlBody.Build("""Hej "><script>alert(1)</script>, se http://example.com/x""");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("""<a href="http://example.com/x" style="color:#2C4327;">http://example.com/x</a>""", html);
    }
}
