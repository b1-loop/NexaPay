// ============================================================
// TransactionRepository.cs
// NexaPay.Infrastructure/Persistence/Repositories
// ============================================================
// Implementerar ITransactionRepository med EF Core.
// Uppdaterad med paginerad metod.
// ============================================================

using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Infrastructure.Persistence.Repositories
{
    public class TransactionRepository
        : Repository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(ApplicationDbContext context)
            : base(context)
        {
        }

        // Hämta alla transaktioner för ett konto
        // Sorterade med senaste transaktion FÖRST
        public async Task<IEnumerable<Transaction>>
            GetTransactionsByAccountIdAsync(Guid accountId)
        {
            return await _dbSet
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        // --------------------------------------------------------
        // NY METOD: Paginerad version
        // --------------------------------------------------------
        // Returnerar en tuple med transaktioner och totalt antal
        // Detta gör att vi kan returnera pagineringsinformation
        // utan att behöva göra två separata databasfrågor
        public async Task<(IEnumerable<Transaction> Transactions,
            int TotalCount)>
            GetTransactionsByAccountIdPagedAsync(
                Guid accountId,
                int page,
                int pageSize)
        {
            // Skapa en bas-query för kontots transaktioner
            // Vi separerar query-byggandet från exekveringen
            var query = _dbSet
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .AsNoTracking();

            // Räkna totalt antal transaktioner
            // CountAsync kör en SELECT COUNT(*) i databasen
            // Vi gör detta INNAN vi paginerar för att få rätt totalCount
            var totalCount = await query.CountAsync();

            // Hämta transaktionerna för den aktuella sidan
            // Skip() = hoppa över tidigare sidor
            // Take() = ta bara pageSize antal
            //
            // Exempel: page=2, pageSize=20
            // Skip((2-1) * 20) = Skip(20) → hoppa över sida 1
            // Take(20) → hämta 20 transaktioner
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Returnera som tuple
            // (Transactions: transaktioner, TotalCount: totalCount)
            return (transactions, totalCount);
        }

        // Hämta transaktioner filtrerade på typ
        public async Task<IEnumerable<Transaction>>
            GetTransactionsByTypeAsync(
                Guid accountId,
                TransactionType type)
        {
            return await _dbSet
                .Where(t => t.AccountId == accountId
                    && t.Type == type)
                .OrderByDescending(t => t.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}