// ============================================================
// IDomainEventDispatcher.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// Application-lagrets abstraktion för att dispatcha domän-events.
// UnitOfWork beror på detta interface – inte på MediatR direkt –
// så Application-koden förblir oberoende av valt MediatR-bibliotek.
// ============================================================

using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.Interfaces
{
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    }
}
