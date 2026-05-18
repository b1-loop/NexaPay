// ============================================================
// ITransactionRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Repository-kontrakt för transaktioner. Förutom grundläggande
// CRUD exponeras:
//   * GetByIdempotencyKey – för att short-circuita dubbla POSTs.
//   * GetTransactionsByAccountIdPaged – pagering för historik.
//   * GetTransactionsByTypeAsync – för rapporter (t.ex. alla
//     uttag under en period).
// ============================================================

using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;

namespace NexaPay.Domain.Interfaces
{
    public interface ITransactionRepository : IGenericRepository<Transaction>
    {
        // Slår upp en transaktion via (idempotency-nyckel, konto). Nyckeln är
        // unik per konto – så User B kan aldrig återanvända User A:s nyckel.
        Task<Transaction?> GetByIdempotencyKeyAsync(Guid idempotencyKey, Guid accountId, CancellationToken cancellationToken = default);
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
