// ============================================================
// TransactionRepository.cs
// NexaPay.Infrastructure/Persistence/Repositories
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class TransactionRepository
        : Repository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(ApplicationDbContext context)
            : base(context)
        {
        }

        // Hämta alla transaktioner för ett konto
        // Sorterade med senaste transaktion FÖRST
        // OrderByDescending(t => t.CreatedAt) = nyaste överst
        public async Task<IEnumerable<Transaction>> GetTransactionsByAccountIdAsync(
            Guid accountId)
        {
            return await _dbSet
                .Where(t => t.AccountId == accountId)
                // Senaste transaktion visas överst – standard för kontoutdrag
                .OrderByDescending(t => t.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        // Hämta transaktioner filtrerade på typ
        public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(
            Guid accountId,
            TransactionType type)
        {
            return await _dbSet
                .Where(t => t.AccountId == accountId && t.Type == type)
                .OrderByDescending(t => t.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}