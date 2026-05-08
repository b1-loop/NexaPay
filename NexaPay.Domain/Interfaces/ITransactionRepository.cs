using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;

namespace NexaPay.Domain.Interfaces
{
    public interface ITransactionRepository
    {
        Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
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
