using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    // Generisk repository-bas som implementerar IGenericRepository<T> en gång
    // åt alla konkreta repos (AccountRepository, CardRepository, TransactionRepository).
    // Eliminerar duplicerad CRUD-kod i varje implementation.
    public abstract class Repository<T> : IGenericRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        protected Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _dbSet.FindAsync(new object[] { id }, cancellationToken);

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

        public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
            => await _dbSet.AddAsync(entity, cancellationToken);
    }
}
