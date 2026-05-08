using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class MoneyTransferredHandler : INotificationHandler<MoneyTransferred>
    {
        private readonly ILogger<MoneyTransferredHandler> _logger;

        public MoneyTransferredHandler(ILogger<MoneyTransferredHandler> logger)
            => _logger = logger;

        public Task Handle(MoneyTransferred notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "MoneyTransferred: FromAccountId={FromAccountId} ToAccountId={ToAccountId} " +
                "OwnerId={OwnerId} Amount={Amount} At={OccurredAt}",
                notification.FromAccountId,
                notification.ToAccountId,
                notification.FromOwnerId,
                notification.Amount,
                notification.OccurredAt);

            return Task.CompletedTask;
        }
    }
}
