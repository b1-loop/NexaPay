// ============================================================
// INotificationService.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// Skickar notifikationer (e-post) till användare. Anropas av
// event handlers (MoneyDeposited, CardBlocked, AccountClosed)
// och AuthService (registrering, lösenordsåterställning).
//
// Två implementationer i Infrastructure:
//   * SmtpNotificationService – riktig SMTP (Gmail)
//   * LoggingNotificationService – skriver bara till loggen
//     (används i tester och utveckling)
// ============================================================

namespace NexaPay.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task NotifyTransactionAsync(string ownerId, string subject, string body, CancellationToken cancellationToken = default);
        Task NotifyCardBlockedAsync(string ownerId, Guid cardId, CancellationToken cancellationToken = default);
        Task NotifyAccountClosedAsync(string ownerId, Guid accountId, CancellationToken cancellationToken = default);

        // Skickar direkt till angiven e-postadress (ingen ownerId-uppslag behövs)
        Task NotifyEmailConfirmationAsync(string email, string confirmationToken, CancellationToken cancellationToken = default);
        Task NotifyPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken = default);
    }
}
