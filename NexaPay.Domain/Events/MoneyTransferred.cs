using NexaPay.Domain.ValueObjects;

namespace NexaPay.Domain.Events
{
    public sealed record MoneyTransferred(
        Guid FromAccountId,
        Guid ToAccountId,
        string FromOwnerId,
        Money Amount,
        DateTime OccurredAt) : IDomainEvent;
}
