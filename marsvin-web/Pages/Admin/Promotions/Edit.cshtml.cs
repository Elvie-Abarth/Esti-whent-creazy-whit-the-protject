using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Promotions;

[Authorize(Roles = "Admin")]
public class EditModel(IPromotionStore promotions, ICatalog catalog, IAuditLogStore audit) : PageModel
{
    public int PromotionId { get; set; }
    public bool IsNew => PromotionId == 0;
    public IReadOnlyList<StockProduct> Products { get; private set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet(int id)
    {
        PromotionId = id;
        Products = catalog.Accessories;

        if (id == 0)
        {
            Input.StartDate = DateOnly.FromDateTime(DateTime.Today);
            Input.EndDate = Input.StartDate.AddDays(14);
            return Page();
        }

        var promotion = promotions.FindById(id);
        if (promotion is null) return NotFound();

        Input = new InputModel
        {
            Title = promotion.Title,
            Description = promotion.Description,
            DiscountPercent = promotion.DiscountPercent,
            ProductId = promotion.ProductId,
            StartDate = promotion.StartDate,
            EndDate = promotion.EndDate,
            IsActive = promotion.IsActive
        };
        return Page();
    }

    public IActionResult OnPost(int id)
    {
        PromotionId = id;
        Products = catalog.Accessories;
        if (Input.EndDate < Input.StartDate)
            // This page has no per-field error spans (see Edit.cshtml), only
            // the ModelOnly summary - a field-keyed error here would silently
            // never render, the same bug this fixes elsewhere on this page's
            // siblings (Register/Profile/Payment, which do have per-field spans).
            ModelState.AddModelError(string.Empty, "Slutdato skal være efter startdato.");
        if (!ModelState.IsValid) return Page();

        var promotion = new Promotion
        {
            PromotionId = id,
            Title = Input.Title,
            Description = Input.Description,
            DiscountPercent = Input.DiscountPercent,
            ProductId = Input.ProductId,
            StartDate = Input.StartDate,
            EndDate = Input.EndDate,
            IsActive = Input.IsActive
        };

        if (id == 0)
        {
            promotions.Create(promotion);
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Promotion.Created", promotion.Title);
        }
        else
        {
            promotions.Update(promotion);
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Promotion.Updated", promotion.Title);
        }

        return RedirectToPage("Index");
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string Title { get; set; } = "";

        [Required, StringLength(500)]
        public string Description { get; set; } = "";

        [Range(1, 100)]
        public int DiscountPercent { get; set; } = 10;

        public int? ProductId { get; set; }

        [Required]
        public DateOnly StartDate { get; set; }

        [Required]
        public DateOnly EndDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
