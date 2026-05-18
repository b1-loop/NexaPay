// ============================================================
// IDomainEvent.cs – NexaPay.Domain/Events
// ============================================================
// Markerar att en typ är ett domän-event. Domain har INGA
// beroenden mot infrastrukturpaket – varken EF Core eller
// MediatR. Application lindar varje event i en
// DomainEventNotification<T> innan dispatch via MediatR.
// ============================================================

namespace NexaPay.Domain.Events
{
    public interface IDomainEvent { }
}
