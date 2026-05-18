// ============================================================
// AccountClosed.cs – NexaPay.Domain/Events
// ============================================================
// Publiceras när Account.Close() lyckats. Används bland annat
// för att skicka avslutningsmejl och skriva audit-logg.
// ============================================================

namespace NexaPay.Domain.Events
{
    public sealed record AccountClosed(
        Guid AccountId,
        string OwnerId,
        DateTime OccurredAt) : IDomainEvent;
}
