using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class MoneyWithdrawnHandler : INotificationHandler<MoneyWithdrawn>
    {
        private readonly ILogger<MoneyWithdrawnHandler> _logger;

        public MoneyWithdrawnHandler(ILogger<MoneyWithdrawnHandler> logger)
            => _logger = logger;

        public Task Handle(MoneyWithdrawn notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "MoneyWithdrawn: AccountId={AccountId} OwnerId={OwnerId} " +
                "Amount={Amount} NewBalance={NewBalance} At={OccurredAt}",
                notification.AccountId,
                notification.OwnerId,
                notification.Amount,
                notification.NewBalance,
                notification.OccurredAt);

            return Task.CompletedTask;
        }
    }
}
