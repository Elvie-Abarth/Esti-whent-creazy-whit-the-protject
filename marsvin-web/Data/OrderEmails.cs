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
    public static async Task SendStatusUpdateAsync(IEmailSender emailSender, IUserAccountStore users, Order order)
    {
        // A signed-in buyer's address comes from their account as it is now;
        // a guest's is the one given at checkout. Neither (the account has
        // since been deleted): nobody left to tell.
        var buyer = order.UserId is int userId ? users.FindById(userId) : null;
        var toEmail = buyer?.Email ?? order.GuestEmail;
        var toName = buyer?.DisplayName ?? order.GuestName;
        if (toEmail is null) return;

        var status = order.Status.DisplayName(order.DeliveryMethod);
        var shipped = order.DeliveryMethod == DeliveryMethod.Shipping;
        var detail = order.Status switch
        {
            OrderStatus.Processing => "Vi er ved at gøre din ordre klar.",
            OrderStatus.Sent when shipped =>
                $"Din pakke er afleveret til {order.ShippingCarrier?.DisplayName()}." +
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
