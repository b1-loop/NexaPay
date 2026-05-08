using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class AccountRepository : Repository<Account>, IAccountRepository
    {
        public AccountRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Account>> GetAllAccountsAsync(CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<IEnumerable<Account>> GetAllAccountsIncludingClosedAsync(CancellationToken cancellationToken = default)
            => await _dbSet.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);

        public async Task<Account?> GetByIdIncludingClosedAsync(Guid id, CancellationToken cancellationToken = default)
            => await _dbSet.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        public async Task<IEnumerable<Account>> GetAccountsByOwnerIdAsync(string ownerId, CancellationToken cancellationToken = default)
            => await _dbSet
                .Where(a => a.OwnerId == ownerId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        public async Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
            => await _dbSet.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, cancellationToken);

        public async Task<bool> AccountNumberExistsAsync(string accountNumber, CancellationToken cancellationToken = default)
            => await _dbSet.AnyAsync(a => a.AccountNumber == accountNumber, cancellationToken);
    }
}
