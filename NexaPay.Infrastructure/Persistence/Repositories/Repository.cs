using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    // Infrastructure-only base class — not exposed via any domain interface.
    // Provides _context and _dbSet helpers to concrete repositories.
    public abstract class Repository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        protected Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _dbSet.FindAsync(new object[] { id }, cancellationToken);

        public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
            => await _dbSet.AddAsync(entity, cancellationToken);
    }
}
