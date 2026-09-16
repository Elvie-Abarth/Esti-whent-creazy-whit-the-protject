using System.Security.Cryptography;

namespace MarsvinWebExample.Tests.Data;

/// <summary>
/// Stands in for an authenticator app in tests: computes the code a real one
/// would show right now for a given secret, so a test can prove the
/// production Totp.ValidateCode accepts it - deliberately a separate,
/// independent implementation of RFC 6238 (not a call into Totp's own
/// private ComputeCode) so a bug shared between both wouldn't go unnoticed.
/// </summary>
internal static class TotpTestHelper
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string CurrentCode(string secret)
    {
        var key = Base32Decode(secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString().PadLeft(6, '0');
    }

    private static byte[] Base32Decode(string base32)
    {
        base32 = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(base32.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in base32)
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0) continue;
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return bytes.ToArray();
    }
}
