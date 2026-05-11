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

        public SmtpNotificationService(
            IConfiguration configuration,
            ILogger<SmtpNotificationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task NotifyTransactionAsync(
            string ownerId,
            string subject,
            string body,
            CancellationToken ct = default)
        {
            await SendAsync(ownerId, subject, body, ct);
        }

        public async Task NotifyCardBlockedAsync(
            string ownerId,
            Guid cardId,
            CancellationToken ct = default)
        {
            await SendAsync(
                ownerId,
                "Ditt kort har blockerats",
                $"Ditt kort (ID: {cardId}) har blockerats. Kontakta oss om du inte begärde detta.",
                ct);
        }

        public async Task NotifyAccountClosedAsync(
            string ownerId,
            Guid accountId,
            CancellationToken ct = default)
        {
            await SendAsync(
                ownerId,
                "Ditt konto har stängts",
                $"Ditt konto (ID: {accountId}) har stängts. Kontakta oss om du har frågor.",
                ct);
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
