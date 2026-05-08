namespace NexaPay.Domain.Events
{
    public sealed record AccountClosed(
        Guid AccountId,
        string OwnerId,
        DateTime OccurredAt) : IDomainEvent;
}
