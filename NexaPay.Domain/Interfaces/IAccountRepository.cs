using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    // Ärver GetByIdAsync, GetAllAsync, AddAsync från IGenericRepository<Account>.
    // Lägger till entitet-specifika queries nedan.
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
