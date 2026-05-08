using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    public interface ICardRepository
    {
        Task<Card?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task AddAsync(Card card, CancellationToken cancellationToken = default);
        Task<IEnumerable<Card>> GetCardsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
        Task<Card?> GetByCardTokenAsync(string cardToken, CancellationToken cancellationToken = default);
    }
}
