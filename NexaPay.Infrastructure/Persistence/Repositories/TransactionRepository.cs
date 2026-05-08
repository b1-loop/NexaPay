using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class TransactionRepository : Repository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Transaction?> GetByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken = default)
            => await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

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
