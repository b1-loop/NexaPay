using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Interfaces;
using System.Net;
using System.Net.Mail;

namespace NexaPay.Infrastructure.Notifications
{
    public class SmtpNotificationService : INotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpNotificationService> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public SmtpNotificationService(
            IConfiguration configuration,
            ILogger<SmtpNotificationService> logger,
            UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task NotifyTransactionAsync(
            string ownerId,
            string subject,
            string body,
            CancellationToken ct = default)
        {
            var email = await ResolveEmailAsync(ownerId);
            if (email is null) return;
            await SendAsync(email, subject, body, ct);
        }

        public async Task NotifyCardBlockedAsync(
            string ownerId,
            Guid cardId,
            CancellationToken ct = default)
        {
            var email = await ResolveEmailAsync(ownerId);
            if (email is null) return;
            await SendAsync(
                email,
                "Ditt kort har blockerats",
                $"Ditt kort (ID: {cardId}) har blockerats. Kontakta oss om du inte begärde detta.",
                ct);
        }

        public async Task NotifyAccountClosedAsync(
            string ownerId,
            Guid accountId,
            CancellationToken ct = default)
        {
            var email = await ResolveEmailAsync(ownerId);
            if (email is null) return;
            await SendAsync(
                email,
                "Ditt konto har stängts",
                $"Ditt konto (ID: {accountId}) har stängts. Kontakta oss om du har frågor.",
                ct);
        }

        // Slår upp e-postadressen från Identity via userId.
        // Returnerar null och loggar varning om användaren inte hittas.
        private async Task<string?> ResolveEmailAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email is null)
            {
                _logger.LogWarning(
                    "Kunde inte hitta e-postadress för userId {UserId} – notifiering ej skickad.",
                    userId);
                return null;
            }
            return user.Email;
        }

        private async Task SendAsync(
            string to,
            string subject,
            string body,
            CancellationToken ct)
        {
            var host = _configuration["Smtp:Host"];
            var username = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];
            var fromName = _configuration["Smtp:FromName"] ?? "NexaPay";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning(
                    "SMTP ej konfigurerat – notifiering ej skickad. Subject: {Subject}", subject);
                return;
            }

            if (!int.TryParse(_configuration["Smtp:Port"], out var port))
                port = 587;

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(username, password)
                };

                var message = new MailMessage
                {
                    From = new MailAddress(username, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                message.To.Add(to);

                await client.SendMailAsync(message, ct);

                _logger.LogInformation(
                    "Mail skickat till {To} | Subject: {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Misslyckades att skicka mail till {To} | Subject: {Subject}", to, subject);
            }
        }
    }
}
