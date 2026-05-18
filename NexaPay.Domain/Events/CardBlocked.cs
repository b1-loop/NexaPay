// ============================================================
// CardBlocked.cs – NexaPay.Domain/Events
// ============================================================
// Publiceras när Card.Block() lyckats. Triggar bl.a. en
// notifiering till kortinnehavaren och loggning i audit-spåret.
// ============================================================

namespace NexaPay.Domain.Events
{
    public sealed record CardBlocked(
        Guid CardId,
        Guid AccountId,
        DateTime OccurredAt) : IDomainEvent;
}
