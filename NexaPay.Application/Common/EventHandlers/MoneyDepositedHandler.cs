using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class MoneyDepositedHandler : INotificationHandler<MoneyDeposited>
    {
        private readonly ILogger<MoneyDepositedHandler> _logger;

        public MoneyDepositedHandler(ILogger<MoneyDepositedHandler> logger)
            => _logger = logger;

        public Task Handle(MoneyDeposited notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "MoneyDeposited: AccountId={AccountId} OwnerId={OwnerId} " +
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
