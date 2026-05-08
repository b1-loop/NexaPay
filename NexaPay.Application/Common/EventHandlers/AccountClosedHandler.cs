using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class AccountClosedHandler : INotificationHandler<AccountClosed>
    {
        private readonly ILogger<AccountClosedHandler> _logger;

        public AccountClosedHandler(ILogger<AccountClosedHandler> logger)
            => _logger = logger;

        public Task Handle(AccountClosed notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "AccountClosed: AccountId={AccountId} OwnerId={OwnerId} At={OccurredAt}",
                notification.AccountId,
                notification.OwnerId,
                notification.OccurredAt);

            return Task.CompletedTask;
        }
    }
}
