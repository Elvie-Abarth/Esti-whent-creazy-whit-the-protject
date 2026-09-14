namespace MarsvinWebExample.Models;

/// <summary>
/// A short user-facing message in both languages, packed into a single
/// string so it can travel through a plain `string?` TempData property
/// exactly like ErrorMessage/ToastMessage already do - TempData's default
/// provider only supports simple types, not arbitrary objects, so this
/// deliberately isn't its own TempData-serialized type. The implicit
/// conversion to string is what lets `ErrorMessage = new Bilingual(da, en);`
/// just work against an existing `string?` property with no property-type
/// changes needed anywhere else.
///
/// _Layout.cshtml unpacks it back into <see cref="Da"/> (the toast's text)
/// and <see cref="En"/> (its data-en attribute), so the site's ordinary
/// data-en swap (lang-toggle.js) picks it up for free - the same mechanism
/// used for every other piece of user-facing text, just packed differently
/// because this text originates in C# code, not Razor markup.
/// </summary>
public sealed record Bilingual(string Da, string En)
{
    // U+241F, the control-picture for "unit separator" - never typed by a
    // person and never produced by anything in this app's own message text,
    // so safe to split on without risk of colliding with real content.
    // Written as an escape (not the literal glyph) so it survives round-trips
    // through tools/terminals with no dependency on font rendering.
    private const char Separator = (char)0x241F;

    public static implicit operator string(Bilingual bilingual) => $"{bilingual.Da}{Separator}{bilingual.En}";

    public static Bilingual Parse(string encoded)
    {
        var parts = encoded.Split(Separator, 2);
        return parts.Length == 2 ? new Bilingual(parts[0], parts[1]) : new Bilingual(encoded, encoded);
    }
}
