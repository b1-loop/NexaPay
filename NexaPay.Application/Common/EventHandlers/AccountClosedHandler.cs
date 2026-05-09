using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class AccountClosedHandler : INotificationHandler<AccountClosed>
    {
        private readonly ILogger<AccountClosedHandler> _logger;
        private readonly INotificationService _notifications;

        public AccountClosedHandler(
            ILogger<AccountClosedHandler> logger,
            INotificationService notifications)
        {
            _logger = logger;
            _notifications = notifications;
        }

        public async Task Handle(AccountClosed notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "AccountClosed: AccountId={AccountId} OwnerId={OwnerId} At={OccurredAt}",
                notification.AccountId,
                notification.OwnerId,
                notification.OccurredAt);

            await _notifications.NotifyAccountClosedAsync(
                notification.OwnerId,
                notification.AccountId,
                cancellationToken);
        }
    }
}
