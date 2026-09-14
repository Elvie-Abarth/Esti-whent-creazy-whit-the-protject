using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MarsvinWebExample.Data;

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options, IWebHostEnvironment environment, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private const string LogoContentId = "marsvin-mark";

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.Username));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = BuildBody(body);

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls);
        try
        {
            await client.AuthenticateAsync(settings.Username, settings.Password);
            await client.SendAsync(message);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }

        logger.LogInformation("Sent {Subject} to {ToEmail}.", subject, toEmail);
    }

    // Multipart/alternative: a plain-text part identical to what every email
    // sent by this project always looked like (still what a text-only client,
    // or someone with images/HTML off, sees), plus an HTML part with the shop
    // mark and contact details every other page already shows in the footer -
    // recipients get the same branding a page of the site gives them, instead
    // of a bare unbranded message that's easy to mistake for spam.
    private MimeEntity BuildBody(string plainTextBody)
    {
        var builder = new BodyBuilder { TextBody = plainTextBody };

        var logoPath = Path.Combine(environment.WebRootPath, "img", "marsvin-mark.png");
        if (File.Exists(logoPath))
        {
            var logo = builder.LinkedResources.Add(logoPath);
            logo.ContentId = LogoContentId;
            builder.HtmlBody = BuildHtmlBody(plainTextBody, $"cid:{LogoContentId}");
        }
        else
        {
            // Falls back to text-only HTML (still branded via the footer) if
            // the asset is ever missing - never lets a missing logo file turn
            // into a failed send.
            builder.HtmlBody = BuildHtmlBody(plainTextBody, logoCid: null);
        }

        return builder.ToMessageBody();
    }

    private static string BuildHtmlBody(string plainTextBody, string? logoCid)
    {
        // WebUtility.HtmlEncode first, <br> only afterwards - the body can
        // contain a customer's own display name or reason text, so it has to
        // be encoded the same way any other user-controlled text going into
        // HTML would be, before any markup (the line breaks) is added back in.
        var encodedBody = WebUtility.HtmlEncode(plainTextBody).Replace("\n", "<br>");
        var logoImg = logoCid is null
            ? ""
            : $"""<img src="{logoCid}" width="48" height="32" alt="Marsvin" style="display:block;border:0;">""";

        return $$"""
            <!DOCTYPE html>
            <html lang="da">
            <body style="margin:0;padding:0;background:#F0E7D2;font-family:Georgia,'Times New Roman',serif;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#F0E7D2;padding:24px 0;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background:#FBF7EE;border-radius:8px;overflow:hidden;">
                                <tr>
                                    <td style="background:#2C4327;padding:20px 28px;">
                                        <table role="presentation" cellpadding="0" cellspacing="0">
                                            <tr>
                                                <td style="padding-right:10px;">{{logoImg}}</td>
                                                <td style="color:#F0E7D2;font-size:20px;font-weight:bold;">Marsvin</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:28px;color:#23301F;font-size:15px;line-height:1.6;">
                                        {{encodedBody}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:18px 28px;background:#F0E7D2;color:#5B5142;font-size:12px;line-height:1.7;">
                                        <strong>Marsvin</strong> &middot; Havnegade 12, 6700 Esbjerg<br>
                                        Torsdag og fredag 14&ndash;18, l&oslash;rdag 10&ndash;14<br>
                                        <a href="mailto:kontakt@marsvin.dk" style="color:#2C4327;">kontakt@marsvin.dk</a>
                                        &middot;
                                        <a href="tel:+4570123456" style="color:#2C4327;">70 12 34 56</a><br>
                                        <span style="color:#8A8168;">Eksempelprojekt, ikke en registreret virksomhed.</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }
}
