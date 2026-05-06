// ============================================================
// ITransactionRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Specifikt interface för Transaction-operationer.
// Uppdaterad med paginerad metod för transaktionshistorik.
// ============================================================

using NexaPay.Domain.Enums;
using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    public interface ITransactionRepository : IRepository<Transaction>
    {
        Task<(IEnumerable<Transaction> Transactions, int TotalCount)>
            GetTransactionsByAccountIdPagedAsync(
                Guid accountId,
                int page,
                int pageSize);

        // Hämta transaktioner filtrerade på typ
        Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(
            Guid accountId,
            TransactionType type);
    }
}