// ============================================================
// IAuditService.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// Skriver audit-rader till databasen. Implementeras av
// EfAuditService i Infrastructure och anropas av AuditBehavior
// (för alla kommandon) och ValidationBehavior (för misslyckade
// valideringar).
// ============================================================

namespace NexaPay.Application.Common.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string command, string userId, bool isSuccess, CancellationToken cancellationToken = default);
    }
}
