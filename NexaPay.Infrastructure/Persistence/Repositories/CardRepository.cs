// ============================================================
// CardRepository.cs
// NexaPay.Infrastructure/Persistence/Repositories
// ============================================================
// Konkret EF Core-implementation av ICardRepository. Ärver
// grundläggande CRUD från Repository<T>.
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class CardRepository : Repository<Card>, ICardRepository
    {
        public CardRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Card>> GetCardsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
            => await _dbSet
                .Where(c => c.AccountId == accountId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        public async Task<Card?> GetByCardTokenAsync(string cardToken, CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().FirstOrDefaultAsync(c => c.CardToken == cardToken, cancellationToken);
    }
}
