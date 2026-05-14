using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    // Ärver GetByIdAsync, GetAllAsync, AddAsync från IGenericRepository<Card>.
    public interface ICardRepository : IGenericRepository<Card>
    {
        Task<IEnumerable<Card>> GetCardsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
        Task<Card?> GetByCardTokenAsync(string cardToken, CancellationToken cancellationToken = default);
    }
}
