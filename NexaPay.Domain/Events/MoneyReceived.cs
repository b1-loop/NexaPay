// ============================================================
// MoneyReceived.cs – NexaPay.Domain/Events
// ============================================================
// Publiceras till mottagarkontots ägare när en överföring landat.
// Skild från MoneyDeposited (som är för manuella insättningar) och
// från MoneyTransferred (som adresserar avsändaren). Det är denna
// händelse som triggar "Du har fått en överföring"-notifikationen.
// ============================================================

using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Events
{
    public sealed record MoneyReceived(
        Guid AccountId,
        string OwnerId,
        Guid FromAccountId,
        Money Amount,
        Money NewBalance,
        DateTime OccurredAt) : IDomainEvent;
}
