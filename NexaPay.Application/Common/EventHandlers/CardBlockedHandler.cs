using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Domain.Events;

namespace NexaPay.Application.Common.EventHandlers
{
    public class CardBlockedHandler : INotificationHandler<CardBlocked>
    {
        private readonly ILogger<CardBlockedHandler> _logger;

        public CardBlockedHandler(ILogger<CardBlockedHandler> logger)
            => _logger = logger;

        public Task Handle(CardBlocked notification, CancellationToken cancellationToken)
        {
            _logger.LogWarning(
                "CardBlocked: CardId={CardId} AccountId={AccountId} At={OccurredAt}",
                notification.CardId,
                notification.AccountId,
                notification.OccurredAt);

            return Task.CompletedTask;
        }
    }
}
