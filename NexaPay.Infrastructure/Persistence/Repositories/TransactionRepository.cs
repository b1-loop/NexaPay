// ============================================================
// TransactionRepository.cs
// NexaPay.Infrastructure/Persistence/Repositories
// ============================================================
// Konkret EF Core-implementation av ITransactionRepository.
// Innehåller pagineringen som körs i ett enda Skip/Take-anrop
// med separat Count för totalt antal. AsNoTracking används
// genomgående eftersom transaktioner aldrig muteras efter att
// de skapats – endast läses.
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class TransactionRepository : Repository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Transaction?> GetByIdempotencyKeyAsync(Guid idempotencyKey, Guid accountId, CancellationToken cancellationToken = default)
            => await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey && t.AccountId == accountId, cancellationToken);

        public async Task<(IEnumerable<Transaction> Transactions, int TotalCount)>
            GetTransactionsByAccountIdPagedAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _dbSet
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .AsNoTracking();

            var totalCount = await query.CountAsync(cancellationToken);
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (transactions, totalCount);
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(
            Guid accountId,
            TransactionType type,
            CancellationToken cancellationToken = default)
            => await _dbSet
                .Where(t => t.AccountId == accountId && t.Type == type)
                .OrderByDescending(t => t.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
    }
}
