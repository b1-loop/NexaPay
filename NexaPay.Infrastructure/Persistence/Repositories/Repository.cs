// ============================================================
// Repository.cs – NexaPay.Infrastructure/Persistence/Repositories
// ============================================================
// Den generiska repository-implementationen.
// Implementerar IRepository<T> med Entity Framework Core.
//
// Alla specifika repositories (AccountRepository, CardRepository
// osv.) ärver från denna klass och får CRUD gratis.
//
// "<T> where T : BaseEntity" = T måste ärva från BaseEntity
// vilket garanterar att T har Id, CreatedAt, UpdatedAt.
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    // "abstract" = kan inte instansieras direkt
    // Bara specifika repositories som AccountRepository kan användas
    public abstract class Repository<T> : IRepository<T>
        where T : BaseEntity
    {
        // --------------------------------------------------------
        // Skyddade fält – tillgängliga för ärvande klasser
        // --------------------------------------------------------

        // DbContext – vår anslutning till databasen
        // "protected" = tillgänglig i ärvande klasser men inte utifrån
        protected readonly ApplicationDbContext _context;

        // DbSet<T> – representerar tabellen för typen T
        // T.ex. om T = Account → _dbSet = Accounts-tabellen
        protected readonly DbSet<T> _dbSet;

        // Konstruktor – tar emot DbContext via Dependency Injection
        protected Repository(ApplicationDbContext context)
        {
            _context = context;
            // Set<T>() hämtar rätt DbSet baserat på typen T
            // T.ex. Set<Account>() returnerar _context.Accounts
            _dbSet = context.Set<T>();
        }

        // --------------------------------------------------------
        // Implementationer av IRepository<T>
        // --------------------------------------------------------

        // Hämta ett objekt med ett specifikt ID
        // FindAsync är optimerad för primärnyckelsökningar
        // Den kollar EF Cores cache först innan den frågar databasen
        public async Task<T?> GetByIdAsync(Guid id)
        {
            // FindAsync returnerar null om inget hittas
            return await _dbSet.FindAsync(id);
        }

        // Hämta alla objekt av typen T
        // AsNoTracking() = EF Core spårar inte ändringarna
        // Mycket snabbare för läsoperationer eftersom
        // EF Core inte behöver hålla koll på alla objekt
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet
                .AsNoTracking() // Snabbare för läsoperationer
                .ToListAsync();
        }

        // Lägg till ett nytt objekt
        // AddAsync lägger till objektet i EF Cores change tracker
        // men sparar det INTE till databasen än
        // Det sparas när UnitOfWork.SaveChangesAsync() anropas
        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        // Uppdatera ett befintligt objekt
        // Update markerar objektet som "Modified" i change trackern
        // EF Core genererar sedan ett UPDATE SQL-statement
        public void Update(T entity)
        {
            // Attach kopplar objektet till DbContext om det inte redan är det
            _dbSet.Attach(entity);
            // Entry().State = Modified markerar ALLA properties som ändrade
            _context.Entry(entity).State = EntityState.Modified;
        }

        // Ta bort ett objekt
        // Remove markerar objektet som "Deleted" i change trackern
        // EF Core genererar ett DELETE SQL-statement vid SaveChanges
        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }
    }
}