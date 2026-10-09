using System.Text;

namespace MarsvinWebExample.Data;

/// <summary>
/// Output sanitising for the one "output" that is easy to forget: the log.
/// A log is read line by line, so text a visitor typed - a display name, an
/// email subject built from one - must not be able to start a line of its
/// own and pass for a real log entry ("log injection"). Same principle as
/// HTML-encoding before a page: encode for the place the text is going.
/// </summary>
public static class LogSafe
{
    /// <summary>
    /// For a value that belongs on one line: every control character
    /// (line breaks, tabs, escape sequences) becomes a space.
    /// </summary>
    public static string Line(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var result = new StringBuilder(value.Length);
        foreach (var character in value)
            result.Append(char.IsControl(character) ? ' ' : character);
        return result.ToString();
    }

    /// <summary>
    /// For text that is meant to span lines (an email body): its line breaks
    /// are kept, but every line is prefixed, so none of them can be mistaken
    /// for the start of a log entry. Other control characters are removed.
    /// </summary>
    public static string Block(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var lines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return string.Join("\n", lines.Select(line => "    | " + Line(line)));
    }
}
