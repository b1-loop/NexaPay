// ============================================================
// IAccountRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Domänen definierar VAD som ska gå att fråga om konton, men inte
// HUR det implementeras. Infrastructure-lagret implementerar
// metoderna mot EF Core. På så sätt kan Application-handlers
// testas mot ett in-memory-fake utan att starta SQL Server.
//
// Den här interfacen ärver grundläggande CRUD från IGenericRepository
// (GetByIdAsync, GetAllAsync, AddAsync) och lägger till queries
// som är specifika för konto-aggregatet.
// ============================================================

using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    public interface IAccountRepository : IGenericRepository<Account>
    {
        Task<IEnumerable<Account>> GetAllAccountsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Account>> GetAllAccountsIncludingClosedAsync(CancellationToken cancellationToken = default);
        Task<Account?> GetByIdIncludingClosedAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Account>> GetAccountsByOwnerIdAsync(string ownerId, CancellationToken cancellationToken = default);
        Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
        Task<bool> AccountNumberExistsAsync(string accountNumber, CancellationToken cancellationToken = default);
        Task<bool> AccountExistsAsync(Guid accountId, CancellationToken cancellationToken = default);
        Task<bool> AccountOwnedByAsync(Guid accountId, string ownerId, CancellationToken cancellationToken = default);
    }
}
