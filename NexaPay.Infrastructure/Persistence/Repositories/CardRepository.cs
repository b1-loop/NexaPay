// ============================================================
// CardRepository.cs
// NexaPay.Infrastructure/Persistence/Repositories
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class CardRepository
        : Repository<Card>, ICardRepository
    {
        public CardRepository(ApplicationDbContext context)
            : base(context)
        {
        }

        // Hämta alla kort för ett specifikt konto
        public async Task<IEnumerable<Card>> GetCardsByAccountIdAsync(
            Guid accountId)
        {
            return await _dbSet
                .Where(c => c.AccountId == accountId)
                .AsNoTracking()
                .ToListAsync();
        }

        // Hämta ett kort via kortnummer
        public async Task<Card?> GetByCardNumberAsync(string cardNumber)
        {
            return await _dbSet
                .FirstOrDefaultAsync(c => c.CardNumber == cardNumber);
        }
    }
}