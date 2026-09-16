using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

public class QrCodeTests
{
    [Fact]
    public void ToDataUri_ReturnsAPngDataUri()
    {
        var uri = QrCode.ToDataUri("otpauth://totp/Marsvin:person@example.com?secret=ABC&issuer=Marsvin");

        Assert.StartsWith("data:image/png;base64,", uri);
        var base64 = uri["data:image/png;base64,".Length..];
        var bytes = Convert.FromBase64String(base64); // throws if not valid base64
        // PNG file signature: 89 50 4E 47 0D 0A 1A 0A
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], bytes.Take(8));
    }
}
