using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Cart;

// No [Authorize] - a guest needs to see their own receipt right after
// checking out too, with no account to prove ownership through. See the
// guest branch below for how that stays IDOR-safe without one.
public class ConfirmationModel(IOrderStore orders) : PageModel
{
    public Order Order { get; private set; } = null!;

    public IActionResult OnGet(int orderId)
    {
        Order? order;
        if (User.Identity?.IsAuthenticated == true)
        {
            // FindForUser only returns the order if it belongs to the current
            // user - without that check, a customer could view anyone's order
            // just by changing the orderId in the URL.
            order = orders.FindForUser(orderId, this.CurrentUserId());
        }
        else
        {
            // A guest has no account for FindForUser's ownership check to use -
            // instead, Payment.OnPostAsync stamps the id of the order a guest
            // checkout just created into that same browser's own session
            // (HttpContext.Session.SetInt32("GuestOrderId", ...)). Only that
            // exact id, in that exact session, is ever allowed through here;
            // nobody can view any other guest's order just by guessing its id,
            // since their own session never had it stamped into it.
            var allowedGuestOrderId = HttpContext.Session.GetInt32("GuestOrderId");
            order = allowedGuestOrderId == orderId ? orders.FindById(orderId) : null;
            // Belt and braces: only ever trust this path for an order that's
            // actually a guest order (UserId null) in the first place - the
            // session stamp above already guarantees that today, but this
            // keeps it true even if that ever changes.
            if (order?.UserId is not null) order = null;
        }

        if (order is null) return NotFound();

        Order = order;
        return Page();
    }
}
