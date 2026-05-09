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
