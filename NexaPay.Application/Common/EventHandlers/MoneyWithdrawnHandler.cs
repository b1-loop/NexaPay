// ============================================================
// MoneyWithdrawnHandler.cs – NexaPay.Application/Common/EventHandlers
// ============================================================
// Reagerar när pengar tagits ut (uttag eller fakturabetalning).
// Loggar händelsen och skickar notifiering till ägaren.
// ============================================================

using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class MoneyWithdrawnHandler : INotificationHandler<MoneyWithdrawn>
    {
        private readonly ILogger<MoneyWithdrawnHandler> _logger;
        private readonly INotificationService _notifications;

        public MoneyWithdrawnHandler(
            ILogger<MoneyWithdrawnHandler> logger,
            INotificationService notifications)
        {
            _logger = logger;
            _notifications = notifications;
        }

        public async Task Handle(MoneyWithdrawn notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "MoneyWithdrawn: AccountId={AccountId} OwnerId={OwnerId} " +
                "Amount={Amount} NewBalance={NewBalance} At={OccurredAt}",
                notification.AccountId,
                notification.OwnerId,
                notification.Amount,
                notification.NewBalance,
                notification.OccurredAt);

            await _notifications.NotifyTransactionAsync(
                notification.OwnerId,
                "Uttag genomfört",
                $"{notification.Amount} har tagits ut från konto {notification.AccountId}. " +
                $"Nytt saldo: {notification.NewBalance}.",
                cancellationToken);
        }
    }
}
