// ============================================================
// IAccountRepository.cs – NexaPay.Domain/Interfaces
// ============================================================
// Specifikt interface för Account-operationer.
// Ärver från IRepository<Account> vilket ger oss
// GetByIdAsync, GetAllAsync, AddAsync, Update, Delete gratis.
//
// Här lägger vi bara till metoder som är SPECIFIKA för konton
// och som inte passar i det generiska interfacet.
// ============================================================

using NexaPay.Domain.Entities;

namespace NexaPay.Domain.Interfaces
{
    // Ärver IRepository<Account> – vi får alla grundoperationer
    // PLUS de kontospecifika metoderna vi definierar här
    public interface IAccountRepository : IRepository<Account>
    {
        // Hämta alla konton som tillhör en specifik användare
        // En användare kan ha flera konton (Checking, Savings, ISK)
        // OwnerId är string eftersom ASP.NET Identity använder string-ID
        Task<IEnumerable<Account>> GetAccountsByOwnerIdAsync(string ownerId);

        // Hämta ett konto via dess kontonummer
        // Kontonummer är unikt per konto (som ett personnummer för kontot)
        // Returnerar null om kontonumret inte finns
        Task<Account?> GetByAccountNumberAsync(string accountNumber);

        // Kontrollera om ett kontonummer redan används
        // Används när vi skapar ett nytt konto för att garantera unikhet
        Task<bool> AccountNumberExistsAsync(string accountNumber);
    }
}