// ============================================================
// ApplicationDbContext.cs – NexaPay.Infrastructure/Persistence
// ============================================================
// Ärver nu från IdentityDbContext istället för DbContext
// för att inkludera Identity-tabeller (Users, Roles osv.)
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexaPay.Domain.Entities;

namespace NexaPay.Infrastructure.Persistence
{
    // IdentityDbContext<IdentityUser, IdentityRole, string>
    // ger oss alla Identity-tabeller automatiskt:
    //   AspNetUsers, AspNetRoles, AspNetUserRoles osv.
    public class ApplicationDbContext
        : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // --------------------------------------------------------
        // Våra egna tabeller
        // --------------------------------------------------------
        public DbSet<Account> Accounts { get; set; } = null!;
        public DbSet<Card> Cards { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            // VIKTIGT: Anropa base först så Identity
            // konfigurerar sina egna tabeller
            base.OnModelCreating(modelBuilder);

            // Tillämpa våra egna konfigurationer
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ApplicationDbContext).Assembly);

            // Global filter – visa bara aktiva konton
            modelBuilder.Entity<Account>()
                .HasQueryFilter(a => a.IsActive);
        }
    }
}