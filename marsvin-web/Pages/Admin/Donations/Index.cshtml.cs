using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Donations;

// Donations registered on /Stoet, newest first. Read-only for both staff
// roles. A money donation is a demo (recorded, never charged), so the total
// here is what was pledged, not money received.
[Authorize(Roles = "Admin,Employee")]
public class IndexModel(IInboxStore inbox) : PageModel
{
    public IReadOnlyList<Donation> Items { get; private set; } = [];
    public decimal MoneyTotal => Items.Sum(d => d.AmountKr ?? 0);

    public void OnGet() => Items = inbox.GetDonations(200);
}
