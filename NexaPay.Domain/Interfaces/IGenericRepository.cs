// ============================================================
// IGenericRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Generiskt repository-interface som samlar gemensamma
// CRUD-operationer för alla aggregat-rötter.
//
// Designval:
// - Bara "additiva" operationer (GetByIdAsync, GetAllAsync,
//   AddAsync) ligger här. Mutationer (Update, Remove) exponeras
//   AVSIKTLIGT INTE i det generiska gränssnittet eftersom DDD-
//   stilen i NexaPay arbetar med "intention-revealing" metoder
//   på aggregaten själva (t.ex. Account.Close(), Card.Block())
//   – inte med generisk soft/hard-delete.
// - Entitet-specifika interface (IAccountRepository,
//   ICardRepository, ITransactionRepository) ärver från detta
//   och lägger till sina domänspecifika queries.
// - Repository<T> (Infrastructure) implementerar detta interface
//   och blir därmed den gemensamma basen för alla concrete repos.
// ============================================================

using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    public interface IGenericRepository<T> where T : BaseEntity
    {
        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
        Task AddAsync(T entity, CancellationToken cancellationToken = default);
    }
}
