// ============================================================
// IRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Det generiska repository-interfacet.
// Definierar CURD-operationer (Create, Update, Read, Delete)
// som ALLA repositories måste implementera.
//
// "<T>" är en typparameter – en platshållare.
// IRepository<Account> = ett repository för Account-objekt
// IRepository<Card>    = ett repository för Card-objekt
//
// "where T : BaseEntity" är en constraint som säger:
// T måste vara en klass som ärver från BaseEntity.
// Det garanterar att T alltid har ett Id, CreatedAt, UpdatedAt.
// ============================================================

using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    public interface IRepository<T> where T : BaseEntity
    {
        // Hämta ett objekt med ett specifikt ID
        // "Task<>" betyder att metoden är asynkron
        // "?" efter T betyder att den kan returnera null om inget hittas
        // t.ex. om vi söker efter ett konto som inte finns
        Task<T?> GetByIdAsync(Guid id);

        // Hämta alla objekt av typen T från databasen
        // Returnerar en lista – tom lista om inga finns
        // Aldrig null – det är god praxis att returnera tom lista
        Task<IEnumerable<T>> GetAllAsync();

        // Lägg till ett nytt objekt i databasen
        // Asynkront eftersom databasoperationer kan ta tid
        // Sparar inte till databasen ännu – det gör UnitOfWork
        Task AddAsync(T entity);

        // Uppdatera ett befintligt objekt
        // Entity Framework spårar ändringar automatiskt
        // men vi har denna metod för tydlighetens skull
        void Update(T entity);

        // Ta bort ett objekt från databasen
        // Obs: transaktioner tas aldrig bort i verkligheten
        // men vi har metoden för fullständighetens skull
        void Delete(T entity);
    }
}