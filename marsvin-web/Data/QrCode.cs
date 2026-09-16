using QRCoder;

namespace MarsvinWebExample.Data;

/// <summary>
/// Renders a QR code entirely offline (QRCoder's pure-managed PNG renderer -
/// no System.Drawing, no network call) - used for the TOTP setup screen so
/// scanning it into an authenticator app is an option, not just typing the
/// secret in by hand.
/// </summary>
public static class QrCode
{
    /// <summary>A data: URI PNG, straight into an &lt;img src&gt; - nothing to host or clean up afterward.</summary>
    public static string ToDataUri(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }
}
