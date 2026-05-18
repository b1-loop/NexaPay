// ============================================================
// MoneyDeposited.cs – NexaPay.Domain/Events
// ============================================================
// Publiceras när Account.Deposit() lyckats. Bär med sig nytt
// saldo så att handlers (notifikation, audit) inte behöver
// göra extra DB-anrop för att läsa state.
// ============================================================

using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Events
{
    public sealed record MoneyDeposited(
        Guid AccountId,
        string OwnerId,
        Money Amount,
        Money NewBalance,
        DateTime OccurredAt) : IDomainEvent;
}
