// ============================================================
// AccountRepository.cs
// NexaPay.Infrastructure/Persistence/Repositories
// ============================================================
// Implementerar IAccountRepository med EF Core.
// Ärver Repository<Account> för att få CRUD gratis.
// Implementerar bara de kontospecifika metoderna.
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    // Ärver Repository<Account> = får GetByIdAsync, GetAllAsync osv. gratis
    // Implementerar IAccountRepository = måste implementera kontospecifika metoder
    public class AccountRepository
        : Repository<Account>, IAccountRepository
    {
        // Konstruktor – skicka context till basklassen
        public AccountRepository(ApplicationDbContext context)
            : base(context)
        {
        }

        // Hämta alla konton för en specifik användare
        public async Task<IEnumerable<Account>> GetAccountsByOwnerIdAsync(
            string ownerId)
        {
            // Where filtrerar på OwnerId
            // AsNoTracking för snabbare läsning
            // ToListAsync kör SQL-frågan mot databasen
            return await _dbSet
                .Where(a => a.OwnerId == ownerId)
                .AsNoTracking()
                .ToListAsync();
        }

        // Hämta ett konto via kontonummer
        public async Task<Account?> GetByAccountNumberAsync(
            string accountNumber)
        {
            // FirstOrDefaultAsync returnerar första matchande eller null
            return await _dbSet
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
        }

        // Kontrollera om ett kontonummer redan finns
        public async Task<bool> AccountNumberExistsAsync(
            string accountNumber)
        {
            // AnyAsync är mycket effektivare än Count() > 0
            // Den genererar SELECT TOP 1 istället för COUNT(*)
            return await _dbSet
                .AnyAsync(a => a.AccountNumber == accountNumber);
        }
    }
}