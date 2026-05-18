// ============================================================
// BaseEntity.cs – NexaPay.Domain/Entities
// ============================================================
// Gemensam basklass för alla aggregat-rötter i domänen
// (Account, Card, Transaction).
//
// Den ansvarar för två saker:
//   1. Identitet och spårning – Id + CreatedAt/UpdatedAt
//      används av EF Core och i hela applikationen.
//   2. Domain Events – varje aggregat samlar interna händelser
//      (t.ex. MoneyDeposited, AccountClosed) i en lista. När
//      UnitOfWork har sparat ändringarna i databasen poppar
//      den eventen och låter MediatR publicera dem till sina
//      handlers (notifikationer, audit-logg m.m.).
// ============================================================

using NexaPay.Domain.Events;

namespace NexaPay.Domain.Entities
{
    public abstract class BaseEntity
    {
        // Primärnyckel – Guid används för att kunna generera id
        // utan att behöva fråga databasen (viktigt för Domain Events
        // som refererar till id innan SaveChanges).
        public Guid Id { get; set; }

        // Tidsstämplar (alltid i UTC). UpdatedAt är nullable för
        // att skilja "aldrig ändrad" från "ändrad nu".
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Intern eventlista – aldrig exponerad direkt för att förhindra
        // att externa klasser stoppar in events utanför aggregatet.
        private readonly List<IDomainEvent> _domainEvents = new();

        // Skrivskyddad vy som tester och infrastruktur kan inspektera.
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        // Skyddad metod – endast subklasser (Account, Card osv.)
        // får skapa nya händelser. Detta säkrar att events alltid
        // skapas av rätt aggregat och inte utifrån.
        protected void RaiseDomainEvent(IDomainEvent domainEvent)
            => _domainEvents.Add(domainEvent);

        // Plockar ut och rensar listan. Anropas av UnitOfWork
        // efter att SaveChangesAsync har lyckats – på så vis
        // publiceras inga events om transaktionen rullas tillbaka.
        public IReadOnlyList<IDomainEvent> PopDomainEvents()
        {
            var copy = _domainEvents.ToList();
            _domainEvents.Clear();
            return copy;
        }
    }
}
