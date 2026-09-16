using System.Security.Cryptography;
using System.Text;

namespace MarsvinWebExample.Data;

/// <summary>
/// A from-scratch RFC 6238 TOTP implementation (the same standard behind
/// Google Authenticator, Microsoft Authenticator, etc.) - real MitID isn't
/// something a local demo project can integrate (it requires being a
/// registered, certified Danish service provider with government-issued
/// certificates), but a self-hosted authenticator-app second factor is a
/// realistic, honest stand-in for what those slides are asking for. No new
/// NuGet dependency - just HMAC-SHA1, which .NET already ships.
/// </summary>
public static class Totp
{
    private const int Digits = 6;
    private const int StepSeconds = 30;

    // ±1 step (30s) either side of "now" - forgives ordinary clock drift
    // between the server and the phone without meaningfully weakening the
    // window an attacker could guess a code in.
    private const int AllowedSkewSteps = 1;

    /// <summary>A fresh random 160-bit secret, Base32-encoded (the format every authenticator app expects).</summary>
    public static string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    /// <summary>
    /// The otpauth:// URI an authenticator app can import - shown as text (and,
    /// if the user's app supports it, pasted straight in) since generating an
    /// actual scannable QR code image would mean either a new dependency or a
    /// hand-rolled QR encoder; every authenticator app also accepts typing the
    /// secret in manually, which this same value covers.
    /// </summary>
    public static string BuildOtpAuthUri(string secret, string accountEmail) =>
        $"otpauth://totp/Marsvin:{Uri.EscapeDataString(accountEmail)}?secret={secret}&issuer=Marsvin&digits={Digits}&period={StepSeconds}";

    /// <summary>Verifies a user-entered code against the secret, allowing ±1 step of clock drift.</summary>
    public static bool ValidateCode(string secret, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length != Digits || !code.All(char.IsDigit)) return false;

        var key = Base32Decode(secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

        for (var skew = -AllowedSkewSteps; skew <= AllowedSkewSteps; skew++)
        {
            var candidate = ComputeCode(key, counter + skew);
            // Fixed-time compare - a code is only 6 digits, but there's no
            // reason to let a timing side-channel narrow that down digit by digit.
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    // RFC 4226 HOTP, applied to the 30-second counter RFC 6238 defines.
    private static string ComputeCode(byte[] key, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var code = binary % (int)Math.Pow(10, Digits);
        return code.ToString().PadLeft(Digits, '0');
    }

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
            result.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        return result.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        base32 = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(base32.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in base32)
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0) continue; // ignores stray whitespace/formatting a user might paste
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
