// ============================================================
// UnitOfWork.cs
// NexaPay.Infrastructure/Persistence/Repositories
// ============================================================
// Implementerar IUnitOfWork.
// Samlar alla repositories och hanterar atomära sparningar.
//
// Tänk på UnitOfWork som en kundvagn:
//   1. Du lägger varor i vagnen (repository-operationer)
//   2. I kassan betalar du allt på en gång (SaveChangesAsync)
//   3. Om betalningen misslyckas läggs allt tillbaka (rollback)
//
// Alla handlers injicerar IUnitOfWork istället för
// individuella repositories – enklare och mer sammanhängande.
// ============================================================

using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        // --------------------------------------------------------
        // Privata fält
        // --------------------------------------------------------

        // DbContext – den delade anslutningen till databasen
        // ALLA repositories delar SAMMA DbContext-instans
        // Det är detta som möjliggör atomära sparningar!
        private readonly ApplicationDbContext _context;

        // Privata backing fields för lazy initialization
        // Lazy initialization = skapa repository först när det behövs
        private IAccountRepository? _accounts;
        private ICardRepository? _cards;
        private ITransactionRepository? _transactions;

        // Konstruktor
        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // --------------------------------------------------------
        // Repository-egenskaper med Lazy Initialization
        // --------------------------------------------------------
        // "??" = null-coalescing operator
        // Om _accounts är null → skapa ett nytt AccountRepository
        // Om _accounts redan finns → återanvänd det befintliga
        // Detta sparar minne och säkerställer att vi alltid
        // använder samma DbContext-instans

        public IAccountRepository Accounts =>
            // Om _accounts är null, skapa ett nytt och spara det
            _accounts ??= new AccountRepository(_context);

        public ICardRepository Cards =>
            _cards ??= new CardRepository(_context);

        public ITransactionRepository Transactions =>
            _transactions ??= new TransactionRepository(_context);

        // --------------------------------------------------------
        // SaveChangesAsync – kärnan i Unit of Work
        // --------------------------------------------------------
        // Sparar ALLA väntande ändringar från ALLA repositories
        // i en enda databastransaktion.
        //
        // EF Core samlar ihop alla ändringar (INSERT, UPDATE, DELETE)
        // och kör dem i en enda SQL-transaktion.
        // Om något misslyckas → rullar EF Core tillbaka allt automatiskt.
        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            // Returnerar antalet rader som påverkades
            // T.ex. om vi uppdaterade 2 konton och skapade 1 transaktion
            // returneras 3
            return await _context.SaveChangesAsync(cancellationToken);
        }

        // --------------------------------------------------------
        // Dispose – städa upp resurser
        // --------------------------------------------------------
        // IDisposable.Dispose() anropas automatiskt när UnitOfWork
        // går ut ur scope (om det används med "using")
        // eller när DI-containern städar upp
        public void Dispose()
        {
            // Stäng databasanslutningen och frigör resurser
            _context.Dispose();
        }
    }
}