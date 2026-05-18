// ============================================================
// ICardRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Repository-kontrakt för kort. Ärver grundläggande CRUD från
// IGenericRepository<Card> och lägger till kort-specifika
// queries (per konto + per token).
// ============================================================

using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    public interface ICardRepository : IGenericRepository<Card>
    {
        Task<IEnumerable<Card>> GetCardsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
        Task<Card?> GetByCardTokenAsync(string cardToken, CancellationToken cancellationToken = default);
    }
}
