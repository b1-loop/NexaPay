using MediatR;
using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Exceptions;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly IPublisher _publisher;

        private IAccountRepository? _accounts;
        private ICardRepository? _cards;
        private ITransactionRepository? _transactions;

        public UnitOfWork(ApplicationDbContext context, IPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
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
            // Collect and clear domain events before saving so that
            // a failed save does not leave events queued for dispatch.
            var domainEvents = _context.ChangeTracker
                .Entries<BaseEntity>()
                .SelectMany(e => e.Entity.PopDomainEvents())
                .ToList();

            try
            {
                var result = await _context.SaveChangesAsync(cancellationToken);

                // Dispatch events only after the save succeeds.
                foreach (var domainEvent in domainEvents)
                    await _publisher.Publish(domainEvent, cancellationToken);

                return result;
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
        }
    }
}
