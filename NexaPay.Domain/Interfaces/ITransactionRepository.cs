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
        // Hämta alla transaktioner för ett konto
        // Sorterade med senaste transaktion FÖRST
        Task<IEnumerable<Transaction>> GetTransactionsByAccountIdAsync(
            Guid accountId);

        // --------------------------------------------------------
        // NY METOD: Paginerad version
        // --------------------------------------------------------
        // Hämtar transaktioner med paginering
        // page = vilken sida (börjar på 1)
        // pageSize = hur många per sida (t.ex. 20)
        // totalCount = ut-parameter som returnerar totalt antal
        //
        // Varför både paginerad och icke-paginerad?
        // Den icke-paginerade används internt i handlerar
        // Den paginerade används för API-endpoints
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