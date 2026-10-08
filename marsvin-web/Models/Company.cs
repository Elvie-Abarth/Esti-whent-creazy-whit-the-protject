namespace MarsvinWebExample.Models;

public enum BuyerType
{
    Private,
    Company
}

/// <summary>
/// The Danish company registration number: eight digits, the last of which is
/// a check digit. Validated the way the number itself is defined (modulus 11
/// with the weights 2-7-6-5-4-3-2-1), which catches a mistyped digit or two
/// swapped ones - it says the number is well-formed, not that the company exists.
/// </summary>
public static class Cvr
{
    private static readonly int[] Weights = [2, 7, 6, 5, 4, 3, 2, 1];

    /// <summary>Digits only - "12 34 56 74" and "DK12345674" style spacing is common when people type it.</summary>
    public static string Normalize(string? input) =>
        new((input ?? "").Where(char.IsAsciiDigit).ToArray());

    public static bool IsValid(string? input)
    {
        // Anything other than digits and spaces means it isn't a CVR number at all.
        if (input is null || input.Any(c => !char.IsAsciiDigit(c) && c != ' ')) return false;

        var digits = Normalize(input);
        if (digits.Length != 8 || digits[0] == '0') return false;

        var sum = digits.Select((c, i) => (c - '0') * Weights[i]).Sum();
        return sum % 11 == 0;
    }
}
