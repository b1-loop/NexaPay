// ============================================================
// AuditLog.cs – NexaPay.Infrastructure/Persistence
// ============================================================
// Persistent audit-rad som skrivs av EfAuditService för varje
// kommando som passerat AuditBehavior eller misslyckats i
// ValidationBehavior. Lever i Infrastructure (inte Domain)
// eftersom den är ett rent infrastruktur-koncept utan business-
// regler. Tabellen är append-only.
// ============================================================

namespace NexaPay.Infrastructure.Persistence
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Command { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
