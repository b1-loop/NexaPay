using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Interfaces;

namespace NexaPay.Infrastructure.Notifications
{
    // Placeholder — byt ut mot riktig e-post/SMS-provider inför produktion.
    // Att byta implementation kräver bara en ändring i DependencyInjection.cs.
    public class LoggingNotificationService : INotificationService
    {
        private readonly ILogger<LoggingNotificationService> _logger;

        public LoggingNotificationService(ILogger<LoggingNotificationService> logger)
            => _logger = logger;

        public Task NotifyTransactionAsync(
            string ownerId,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "NOTIFY | {Subject} | UserId={OwnerId} | {Body}",
                subject, ownerId, body);
            return Task.CompletedTask;
        }

        public Task NotifyCardBlockedAsync(
            string ownerId,
            Guid cardId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning(
                "NOTIFY | Kort blockat | UserId={OwnerId} | CardId={CardId}",
                ownerId, cardId);
            return Task.CompletedTask;
        }

        public Task NotifyAccountClosedAsync(
            string ownerId,
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "NOTIFY | Konto stängt | UserId={OwnerId} | AccountId={AccountId}",
                ownerId, accountId);
            return Task.CompletedTask;
        }

        public Task NotifyEmailConfirmationAsync(
            string email,
            string confirmationToken,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "NOTIFY | E-postbekräftelse | Email={Email} | Token={Token}",
                email, confirmationToken);
            return Task.CompletedTask;
        }

        public Task NotifyPasswordResetAsync(
            string email,
            string resetToken,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "NOTIFY | Lösenordsåterställning | Email={Email} | Token={Token}",
                email, resetToken);
            return Task.CompletedTask;
        }
    }
}
