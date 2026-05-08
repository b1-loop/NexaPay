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
