namespace MarsvinWebExample.Models;

/// <summary>A time-boxed discount, optionally tied to one product.</summary>
public sealed class Promotion
{
    public int PromotionId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required int DiscountPercent { get; init; }
    public int? ProductId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public bool IsActive { get; init; } = true;

    public bool IsCurrentlyRunning(DateOnly today) =>
        IsActive && today >= StartDate && today <= EndDate;
}
