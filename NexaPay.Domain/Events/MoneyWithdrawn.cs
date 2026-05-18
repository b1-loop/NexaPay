// ============================================================
// MoneyWithdrawn.cs – NexaPay.Domain/Events
// ============================================================
// Publiceras när Account.Withdraw() eller PayInvoice() lyckats
// (båda minskar saldot). Innehåller saldot efter uttaget så att
// notifikations-mejlet kan visa nytt belopp.
// ============================================================

using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Events
{
    public sealed record MoneyWithdrawn(
        Guid AccountId,
        string OwnerId,
        Money Amount,
        Money NewBalance,
        DateTime OccurredAt) : IDomainEvent;
}
