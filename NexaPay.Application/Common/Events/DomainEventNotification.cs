// ============================================================
// DomainEventNotification.cs – NexaPay.Application/Common/Events
// ============================================================
// Lindar ett domän-event från Domain (som inte känner till MediatR)
// i en MediatR-INotification så att eventet kan dispatchas via
// IPublisher. Handlers implementerar
// INotificationHandler<DomainEventNotification<TEvent>>.
// ============================================================

using MediatR;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.Events
{
    public sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
        where TEvent : IDomainEvent;
}
