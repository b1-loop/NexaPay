// ============================================================
// EfAuditService.cs – NexaPay.Infrastructure/Persistence
// ============================================================
// EF Core-implementation av IAuditService. Skriver en ny
// AuditLog-rad och kör en omedelbar SaveChanges – auditeringen
// måste persisteras SEPARAT från det egentliga kommandot för
// att inte rullas tillbaka om kommandot misslyckas senare.
// ============================================================

using NexaPay.Application.Common.Interfaces;

namespace NexaPay.Infrastructure.Persistence
{
    public class EfAuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public EfAuditService(ApplicationDbContext context) => _context = context;

        public async Task LogAsync(
            string command,
            string userId,
            bool isSuccess,
            CancellationToken cancellationToken = default)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Command = command,
                UserId = userId,
                IsSuccess = isSuccess,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
