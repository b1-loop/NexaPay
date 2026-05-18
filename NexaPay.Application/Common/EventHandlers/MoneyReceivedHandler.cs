// ============================================================
// MoneyReceivedHandler.cs – NexaPay.Application/Common/EventHandlers
// ============================================================
// Reagerar när en överföring kommit IN på ett konto. Notifierar
// mottagaren ("Du har fått en överföring"). Eventet raise:as på
// mottagar-aggregatet i Account.TransferTo så OwnerId här pekar
// alltid på mottagaren.
// ============================================================

using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Events;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class MoneyReceivedHandler : INotificationHandler<DomainEventNotification<MoneyReceived>>
    {
        private readonly ILogger<MoneyReceivedHandler> _logger;
        private readonly INotificationService _notifications;

        public MoneyReceivedHandler(
            ILogger<MoneyReceivedHandler> logger,
            INotificationService notifications)
        {
            _logger = logger;
            _notifications = notifications;
        }

        public async Task Handle(DomainEventNotification<MoneyReceived> wrapper, CancellationToken cancellationToken)
        {
            var notification = wrapper.Event;
            _logger.LogInformation(
                "MoneyReceived: AccountId={AccountId} OwnerId={OwnerId} FromAccountId={FromAccountId} " +
                "Amount={Amount} NewBalance={NewBalance} At={OccurredAt}",
                notification.AccountId,
                notification.OwnerId,
                notification.FromAccountId,
                notification.Amount,
                notification.NewBalance,
                notification.OccurredAt);

            await _notifications.NotifyTransactionAsync(
                notification.OwnerId,
                "Överföring mottagen",
                $"{notification.Amount} har överförts till ditt konto {notification.AccountId}. " +
                $"Nytt saldo: {notification.NewBalance}.",
                cancellationToken);
        }
    }
}
