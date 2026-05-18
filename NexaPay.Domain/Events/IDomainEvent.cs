// ============================================================
// IDomainEvent.cs – NexaPay.Domain/Events
// ============================================================
// Markerar att en typ är ett domän-event. Ärver MediatRs
// INotification så att vi kan publicera eventen direkt med
// IPublisher.Publish() från UnitOfWork. Då anropas alla
// klasser som implementerar INotificationHandler<TEvent>
// (t.ex. SmtpNotificationService för MoneyDeposited).
// ============================================================

using MediatR;

namespace NexaPay.Domain.Events
{
    public interface IDomainEvent : INotification { }
}
