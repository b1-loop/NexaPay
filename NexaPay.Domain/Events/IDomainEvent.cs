using MediatR;

namespace NexaPay.Domain.Events
{
    // Marker interface for all domain events.
    // Extends INotification so MediatR can dispatch them directly.
    public interface IDomainEvent : INotification { }
}
