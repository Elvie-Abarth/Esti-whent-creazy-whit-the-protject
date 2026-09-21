using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MarsvinWebExample.Data;

/// <summary>
/// Turns a plain-text email body into safe HTML, the same way every email
/// this project sends is written - and, unlike a plain WebUtility.HtmlEncode
/// pass, makes any http(s) link in it an actual clickable &lt;a&gt; instead
/// of inert text an email client won't let you tap. A confirmation/reset
/// link is the whole point of most of these emails, so this isn't cosmetic.
/// </summary>
public static class EmailHtmlBody
{
    private static readonly Regex UrlPattern = new(@"https?://[^\s<>""']+", RegexOptions.Compiled);

    /// <summary>
    /// HTML-encodes <paramref name="plainTextBody"/> (so any user-controlled
    /// text in it - a display name, a reason - can't break out of the
    /// markup) and converts newlines to &lt;br&gt;, the same as before, but
    /// first replaces every bare URL with a real anchor tag.
    /// </summary>
    public static string Build(string plainTextBody)
    {
        var result = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in UrlPattern.Matches(plainTextBody))
        {
            result.Append(WebUtility.HtmlEncode(plainTextBody[lastIndex..match.Index]));

            var encodedUrl = WebUtility.HtmlEncode(match.Value);
            result.Append($"""<a href="{encodedUrl}" style="color:#2C4327;">{encodedUrl}</a>""");

            lastIndex = match.Index + match.Length;
        }

        result.Append(WebUtility.HtmlEncode(plainTextBody[lastIndex..]));
        return result.ToString().Replace("\n", "<br>");
    }
}
