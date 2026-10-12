using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// The "your order has moved on" email - sent when staff change an order's
/// status on Admin/Orders, and when an order is cancelled. Without it, the
/// only way to find out an order was sent is to log in and look, and a guest
/// (no account, no order history) couldn't find out at all.
/// </summary>
public static class OrderEmails
{
    /// <param name="english">
    /// The language to write in when the buyer is the one who triggered the
    /// mail (cancelling their own order) and so is reading the site right
    /// now. Left out when staff trigger it: then it is the language the
    /// order was placed in, since the staff member's own language says
    /// nothing about the buyer's.
    /// </param>
    public static async Task SendStatusUpdateAsync(
        IEmailSender emailSender, IUserAccountStore users, Order order, bool? english = null)
    {
        var inEnglish = english ?? order.PlacedInEnglish;

        // A signed-in buyer's address comes from their account as it is now;
        // a guest's is the one given at checkout. Neither (the account has
        // since been deleted): nobody left to tell.
        var buyer = order.UserId is int userId ? users.FindById(userId) : null;
        var toEmail = buyer?.Email ?? order.GuestEmail;
        var toName = buyer?.DisplayName ?? order.GuestName;
        if (toEmail is null) return;

        var status = order.Status.DisplayName(order.DeliveryMethod, inEnglish);
        var shipped = order.DeliveryMethod == DeliveryMethod.Shipping;
        var carrier = order.ShippingCarrier?.DisplayName(inEnglish);

        if (inEnglish)
        {
            var detailEn = order.Status switch
            {
                OrderStatus.Processing => "We are getting your order ready.",
                OrderStatus.Sent when shipped =>
                    $"Your parcel has been handed over to {carrier}." +
                    (order.TrackingNumber is not null ? $"\nTrack & trace number: {order.TrackingNumber}" : "") +
                    (order.ExpectedDelivery is var (earliest, latest)
                        ? $"\nExpected delivery: {ShippingCalculator.FormatWindow(earliest, latest)}" : ""),
                OrderStatus.Sent =>
                    "Your order is ready in the shop: Marsvin, Havnegade 12, 6700 Esbjerg.\n" +
                    "Opening hours: Thursday and Friday 14-18, Saturday 10-14.",
                OrderStatus.Completed => "Thank you for shopping with us - we hope you'll be happy with it.",
                OrderStatus.Cancelled =>
                    $"The order has been cancelled, and the amount of {order.TotalPrice:N0} kr. will be returned to you (demo - nothing was ever charged).",
                _ => ""
            };

            await emailSender.SendAsync(toEmail, $"Order #{order.OrderId}: {status}",
                $"""
                Hello {toName},

                Your order #{order.OrderId} has a new status: {status}.

                {detailEn}

                Kind regards
                Marsvin
                """, english: true);
            return;
        }

        var detail = order.Status switch
        {
            OrderStatus.Processing => "Vi er ved at gøre din ordre klar.",
            OrderStatus.Sent when shipped =>
                $"Din pakke er afleveret til {carrier}." +
                (order.TrackingNumber is not null ? $"\nTrack & trace-nummer: {order.TrackingNumber}" : "") +
                (order.ExpectedDelivery is var (earliest, latest)
                    ? $"\nForventet levering: {ShippingCalculator.FormatWindow(earliest, latest)}" : ""),
            OrderStatus.Sent =>
                "Din ordre ligger klar i butikken: Marsvin, Havnegade 12, 6700 Esbjerg.\n" +
                "Åbningstider: torsdag og fredag 14-18, lørdag 10-14.",
            OrderStatus.Completed => "Tak for handlen - vi håber, du bliver glad for det.",
            OrderStatus.Cancelled =>
                $"Ordren er annulleret, og beløbet på {order.TotalPrice:N0} kr. føres tilbage til dig (demo - der blev aldrig trukket noget).",
            _ => ""
        };

        await emailSender.SendAsync(toEmail, $"Ordre #{order.OrderId}: {status}",
            $"""
            Hej {toName},

            Din ordre #{order.OrderId} har fået ny status: {status}.

            {detail}

            Venlig hilsen
            Marsvin
            """);
    }
}
