// ============================================================
// UnitOfWork.cs – NexaPay.Infrastructure/Persistence/Repositories
// ============================================================
// Konkret implementation av IUnitOfWork. Två viktiga ansvar:
//
//   1. EXPONERA REPOSITORIES – Accounts/Cards/Transactions skapas
//      lazy vid första anropet och delar samma DbContext-instans
//      så att alla ändringar deltar i samma transaktion.
//
//   2. SAVECHANGES + OUTBOX – när SaveChangesAsync anropas:
//        a) Samla in alla väntande domain events FÖRE SaveChanges.
//        b) Serialisera varje event som en OutboxEvent-rad och
//           lägg in i samma EF-transaktion.
//        c) Kör SaveChanges – aggregat + outbox-rader sparas
//           atomärt. Antingen båda eller inget.
//        d) OutboxDispatcher (BackgroundService) plockar upp
//           oprocessade rader och dispatchar via MediatR i
//           bakgrunden – så SMTP/Redis aldrig hänger fast i
//           request-tråden och en partial-write inte kan ske.
// ============================================================

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Exceptions;
using NexaPay.Domain.Interfaces;
using NexaPay.Infrastructure.Persistence.Entities;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        private IAccountRepository? _accounts;
        private ICardRepository? _cards;
        private ITransactionRepository? _transactions;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IAccountRepository Accounts =>
            _accounts ??= new AccountRepository(_context);

        public ICardRepository Cards =>
            _cards ??= new CardRepository(_context);

        public ITransactionRepository Transactions =>
            _transactions ??= new TransactionRepository(_context);

        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            // Plocka och rensa domain events innan save. Vi behåller dem
            // som outbox-rader istället för att dispatcha direkt.
            var domainEvents = _context.ChangeTracker
                .Entries<BaseEntity>()
                .SelectMany(e => e.Entity.PopDomainEvents())
                .ToList();

            foreach (var ev in domainEvents)
            {
                var outboxEvent = new OutboxEvent
                {
                    Id = Guid.NewGuid(),
                    EventTypeName = ev.GetType().AssemblyQualifiedName!,
                    PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
                    CreatedAt = DateTime.UtcNow
                };
                _context.OutboxEvents.Add(outboxEvent);
            }

            try
            {
                // Aggregat-ändringar och outbox-rader i samma transaktion.
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Build a message that names the actual conflicting entity type(s)
                // instead of hard-coding "Kontot" in a generic save method.
                var entityNames = ex.Entries
                    .Select(e => e.Entity.GetType().Name)
                    .Distinct()
                    .ToList();
                var resource = entityNames.Count == 1
                    ? entityNames[0]
                    : string.Join(", ", entityNames);

                // Clear all tracked state so ConcurrencyRetryBehavior can retry
                // with a completely fresh read from the database.
                _context.ChangeTracker.Clear();

                throw new ConcurrencyException(
                    $"{resource} ändrades av en annan begäran. Försök igen.", ex);
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
            {
                // Parallell request hann skapa en rad med samma (IdempotencyKey,
                // AccountId) och tog det unika indexet. Översätt EF-undantaget
                // till en domän-egen typ så Application-lagret kan fånga den
                // utan att referera EF Core.
                _context.ChangeTracker.Clear();

                throw new IdempotencyConflictException(
                    "En parallell begäran hann skapa samma transaktion. Slå upp den befintliga.", ex);
            }
        }

        // SQL Server returnerar 2627 (PRIMARY KEY/UNIQUE constraint) eller
        // 2601 (CANNOT INSERT DUPLICATE KEY ROW IN UNIQUE INDEX) när en
        // INSERT bryter mot ett unikt index. Övriga DbUpdateException-fall
        // (FK, check-constraint, m.fl.) bubblar upp som tidigare.
        private static bool IsUniqueIndexViolation(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sql
                && (sql.Number == 2627 || sql.Number == 2601);
        }
    }
}
