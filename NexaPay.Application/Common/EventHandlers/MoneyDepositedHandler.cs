// ============================================================
// MoneyDepositedHandler.cs – NexaPay.Application/Common/EventHandlers
// ============================================================
// Reagerar när pengar satts in på ett konto. Loggar händelsen
// och skickar en notifiering ("Insättning genomförd") till ägaren.
// ============================================================

using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class MoneyDepositedHandler : INotificationHandler<MoneyDeposited>
    {
        private readonly ILogger<MoneyDepositedHandler> _logger;
        private readonly INotificationService _notifications;

        public MoneyDepositedHandler(
            ILogger<MoneyDepositedHandler> logger,
            INotificationService notifications)
        {
            _logger = logger;
            _notifications = notifications;
        }

        public async Task Handle(MoneyDeposited notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "MoneyDeposited: AccountId={AccountId} OwnerId={OwnerId} " +
                "Amount={Amount} NewBalance={NewBalance} At={OccurredAt}",
                notification.AccountId,
                notification.OwnerId,
                notification.Amount,
                notification.NewBalance,
                notification.OccurredAt);

            await _notifications.NotifyTransactionAsync(
                notification.OwnerId,
                "Insättning genomförd",
                $"{notification.Amount} har satts in på konto {notification.AccountId}. " +
                $"Nytt saldo: {notification.NewBalance}.",
                cancellationToken);
        }
    }
}
