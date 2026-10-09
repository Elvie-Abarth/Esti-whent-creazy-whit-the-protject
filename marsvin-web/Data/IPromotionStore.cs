using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// Time-boxed discounts (dbo.Promotions), shop-wide or for one product.
/// Managed by an Admin on /Admin/Promotions.
/// </summary>
public interface IPromotionStore
{
    IReadOnlyList<Promotion> GetAll();
    Promotion? FindById(int id);
    void Create(Promotion promotion);
    void Update(Promotion promotion);
    void Delete(int id);
}
