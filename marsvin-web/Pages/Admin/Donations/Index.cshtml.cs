using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Donations;

[Authorize(Roles = "Admin,Employee")]
public class IndexModel(IInboxStore inbox) : PageModel
{
    public IReadOnlyList<Donation> Items { get; private set; } = [];
    public decimal MoneyTotal => Items.Sum(d => d.AmountKr ?? 0);

    public void OnGet() => Items = inbox.GetDonations(200);
}
