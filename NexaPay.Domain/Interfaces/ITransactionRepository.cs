using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;

namespace NexaPay.Domain.Interfaces
{
    // Ärver GetByIdAsync, GetAllAsync, AddAsync från IGenericRepository<Transaction>.
    public interface ITransactionRepository : IGenericRepository<Transaction>
    {
        Task<Transaction?> GetByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken = default);
        Task<(IEnumerable<Transaction> Transactions, int TotalCount)> GetTransactionsByAccountIdPagedAsync(
            Guid accountId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(
            Guid accountId,
            TransactionType type,
            CancellationToken cancellationToken = default);
    }
}
