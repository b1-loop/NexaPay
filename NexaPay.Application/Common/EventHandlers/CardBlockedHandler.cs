// ============================================================
// CardBlockedHandler.cs – NexaPay.Application/Common/EventHandlers
// ============================================================
// Reagerar när ett kort blockerats. Skriver en VARNING i loggen
// (säkerhetsrelevant händelse) och skickar notifikation till
// kortinnehavaren. CardBlocked-eventet bär inte OwnerId, så vi
// slår upp kontot för att hitta ägaren.
// ============================================================

using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Events;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Domain.Events;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Common.EventHandlers
{
    public class CardBlockedHandler : INotificationHandler<DomainEventNotification<CardBlocked>>
    {
        private readonly ILogger<CardBlockedHandler> _logger;
        private readonly INotificationService _notifications;
        private readonly IUnitOfWork _unitOfWork;

        public CardBlockedHandler(
            ILogger<CardBlockedHandler> logger,
            INotificationService notifications,
            IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _notifications = notifications;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DomainEventNotification<CardBlocked> wrapper, CancellationToken cancellationToken)
        {
            var notification = wrapper.Event;
            _logger.LogWarning(
                "CardBlocked: CardId={CardId} AccountId={AccountId} At={OccurredAt}",
                notification.CardId,
                notification.AccountId,
                notification.OccurredAt);

            // CardBlocked-eventet bär inte OwnerId — slå upp kontot för att hitta ägaren.
            var account = await _unitOfWork.Accounts.GetByIdAsync(
                notification.AccountId, cancellationToken);

            if (account is not null)
                await _notifications.NotifyCardBlockedAsync(
                    account.OwnerId,
                    notification.CardId,
                    cancellationToken);
        }
    }
}
