namespace NexaPay.Domain.Events
{
    public sealed record CardBlocked(
        Guid CardId,
        Guid AccountId,
        DateTime OccurredAt) : IDomainEvent;
}
