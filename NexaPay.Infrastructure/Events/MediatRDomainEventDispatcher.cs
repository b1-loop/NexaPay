// ============================================================
// MediatRDomainEventDispatcher.cs – NexaPay.Infrastructure/Events
// ============================================================
// Implementerar IDomainEventDispatcher genom att linda varje
// domän-event i en DomainEventNotification<T> och publicera
// via MediatRs IPublisher.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Events;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Domain.Events;

namespace NexaPay.Infrastructure.Events
{
    public class MediatRDomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IPublisher _publisher;

        public MediatRDomainEventDispatcher(IPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            foreach (var ev in events)
            {
                // Bygg DomainEventNotification<T> där T = runtime-typen av eventet
                // så att rätt INotificationHandler<DomainEventNotification<T>> aktiveras.
                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(ev.GetType());
                var notification = (INotification)Activator.CreateInstance(notificationType, ev)!;
                await _publisher.Publish(notification, cancellationToken);
            }
        }
    }
}
