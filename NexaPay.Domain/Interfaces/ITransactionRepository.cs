// ============================================================
// ITransactionRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Specifikt interface för Transaction-operationer.
// Ärver från IRepository<Transaction> för grundläggande CRUD.
//
// Transaktionsspecifika operationer:
//   - Hämta transaktioner per konto
//   - Hämta transaktioner per typ
// ============================================================

using NexaPay.Domain.Enums;
using System.Transactions;
using NexaPay.Domain.Entities;
using Transaction = NexaPay.Domain.Entities.Transaction;  // ← VÅR Transaction-klass


namespace NexaPay.Domain.Interfaces
{
    public interface ITransactionRepository : IRepository<Transaction>
    {
        // Hämta alla transaktioner för ett specifikt konto
        // Sorterade med senaste transaktionen först (standard i bankhistorik)
        Task<IEnumerable<Transaction>> GetTransactionsByAccountIdAsync(Guid accountId);

        // Hämta transaktioner filtrerade på typ
        // T.ex. visa bara uttag eller bara insättningar
        // Användbart för rapporter och filtrering i UI
        Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(
            Guid accountId,
            TransactionType type);
    }
}