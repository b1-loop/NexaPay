// ============================================================
// MoneyTransferredHandler.cs – NexaPay.Application/Common/EventHandlers
// ============================================================
// Reagerar när en överföring genomförts. Loggar och notifierar
// AVSÄNDAREN. Mottagaren får en separat MoneyReceived-händelse
// som hanteras av MoneyReceivedHandler.
// ============================================================

using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Events;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class MoneyTransferredHandler : INotificationHandler<DomainEventNotification<MoneyTransferred>>
    {
        private readonly ILogger<MoneyTransferredHandler> _logger;
        private readonly INotificationService _notifications;

        public MoneyTransferredHandler(
            ILogger<MoneyTransferredHandler> logger,
            INotificationService notifications)
        {
            _logger = logger;
            _notifications = notifications;
        }

        public async Task Handle(DomainEventNotification<MoneyTransferred> wrapper, CancellationToken cancellationToken)
        {
            var notification = wrapper.Event;
            _logger.LogInformation(
                "MoneyTransferred: FromAccountId={FromAccountId} ToAccountId={ToAccountId} " +
                "OwnerId={OwnerId} Amount={Amount} At={OccurredAt}",
                notification.FromAccountId,
                notification.ToAccountId,
                notification.FromOwnerId,
                notification.Amount,
                notification.OccurredAt);

            await _notifications.NotifyTransactionAsync(
                notification.FromOwnerId,
                "Överföring genomförd",
                $"{notification.Amount} har överförts från konto {notification.FromAccountId} " +
                $"till konto {notification.ToAccountId}.",
                cancellationToken);
        }
    }
}
