using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

public interface IPromotionStore
{
    IReadOnlyList<Promotion> GetAll();
    Promotion? FindById(int id);
    void Create(Promotion promotion);
    void Update(Promotion promotion);
    void Delete(int id);
}
