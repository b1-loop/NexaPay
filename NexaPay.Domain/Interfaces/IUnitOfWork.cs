// ============================================================
// IUnitOfWork.cs – NexaPay.Domain/Interfaces
// ============================================================
// Unit of Work-mönstret samlar alla repository-operationer
// under en enda databastransaktion.
//
// Tänk på det som en kundvagn:
//   - Du lägger varor i vagnen (repository-operationer)
//   - I kassan betalar du allt på en gång (SaveChangesAsync)
//   - Om betalningen misslyckas läggs allt tillbaka (rollback)
//
// IDisposable implementeras för att EF Core:s DbContext
// ska städas upp korrekt när UnitOfWork inte längre behövs.
// ============================================================


//Överföring 500 kr från Konto A till Konto B:

//Steg 1: Dra 500 kr från Konto A  (Balance: 1000 → 500)
//Steg 2: Sätt in 500 kr på Konto B (Balance: 200 → 700)
//Steg 3: Spara en Transaction-rad

//Om Steg 2 kraschar UTAN Unit of Work:
//→ Konto A har tappat 500 kr
//→ Konto B har inte fått sina 500 kr
//→ Pengarna är borta! 💸

//Med Unit of Work:
//→ Alla tre steg körs i SAMMA databastransaktion
//→ Kraschar ett steg → rullar ALLA steg tillbaka
//→ Ingen förlorar pengar ✅

namespace NexaPay.Domain.Interfaces
{
    // IDisposable gör att vi kan använda "using"-satser
    // vilket garanterar att resurser frigörs korrekt
    public interface IUnitOfWork : IDisposable
    {
        // --------------------------------------------------------
        // Repositories – åtkomst till alla dataoperationer
        // --------------------------------------------------------
        // Via IUnitOfWork når vi alla repositories på ett ställe
        // Istället för att injicera varje repository separat
        // injicerar vi bara IUnitOfWork i våra handlers

        // Repository för konto-operationer
        IAccountRepository Accounts { get; }

        // Repository för kort-operationer
        ICardRepository Cards { get; }

        // Repository för transaktions-operationer
        ITransactionRepository Transactions { get; }

        // --------------------------------------------------------
        // Spara alla ändringar till databasen
        // --------------------------------------------------------

        // Sparar ALLA väntande ändringar från alla repositories
        // i en enda databastransaktion.
        // Returnerar antalet rader som påverkades i databasen.
        // "CancellationToken" låter oss avbryta operationen
        // om användaren t.ex. stänger sin webbläsare
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}