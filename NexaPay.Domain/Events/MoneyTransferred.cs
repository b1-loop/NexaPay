// ============================================================
// MoneyTransferred.cs – NexaPay.Domain/Events
// ============================================================
// Publiceras när Account.TransferTo() lyckats. Innehåller både
// avsändar- och mottagarid så att notifikationer kan adresseras
// till båda parter.
// ============================================================

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
