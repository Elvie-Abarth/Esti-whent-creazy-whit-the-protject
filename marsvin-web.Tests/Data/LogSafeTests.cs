using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

public class LogSafeTests
{
    [Fact]
    public void Line_TurnsLineBreaksAndOtherControlCharactersIntoSpaces()
    {
        // A display name written to forge a second log entry.
        var forged = "Anna\r\nwarn: Admin password changed\tby system\u001b[31m";

        var safe = LogSafe.Line(forged);

        Assert.DoesNotContain('\n', safe);
        Assert.DoesNotContain('\r', safe);
        Assert.DoesNotContain('\t', safe);
        Assert.DoesNotContain('\u001b', safe);
        Assert.StartsWith("Anna  warn: Admin password changed by system", safe);
    }

    [Fact]
    public void Block_KeepsTheLines_ButNoneOfThemCanStartALogEntry()
    {
        var body = "Hej Anna,\n\nfail: Database dropped\r\nhttp://localhost:5080/Account/ConfirmLogin?token=abc";

        var lines = LogSafe.Block(body).Split('\n');

        Assert.Equal(4, lines.Length);
        Assert.All(lines, line => Assert.StartsWith("    | ", line));
        // Still readable, link intact - the developer copies it from the console.
        Assert.Contains("    | http://localhost:5080/Account/ConfirmLogin?token=abc", lines);
    }

    [Fact]
    public void NothingIn_NothingOut()
    {
        Assert.Equal("", LogSafe.Line(null));
        Assert.Equal("", LogSafe.Block(""));
    }
}
