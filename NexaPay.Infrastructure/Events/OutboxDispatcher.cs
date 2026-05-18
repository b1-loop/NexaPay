// ============================================================
// OutboxDispatcher.cs – NexaPay.Infrastructure/Events
// ============================================================
// BackgroundService som plockar oprocessade rader ur OutboxEvents,
// rekonstruerar domän-eventet, lindar i DomainEventNotification<T>
// och publicerar via MediatR. Lyckade rader markeras med
// ProcessedAt. Misslyckade rader får sitt Error-fält uppdaterat
// och retry:as nästa varv (med ökat Attempts-räknare).
//
// Lite att tänka på:
//   * BackgroundService är singleton men ApplicationDbContext är
//     scoped, så vi öppnar en egen scope per polling-iteration.
//   * Vi tar en liten batch (50) per varv så en lång kö inte
//     blockerar hela dispatchern på en iteration.
//   * Polling-intervall är medvetet kort (1 sek) eftersom
//     latency-känsligheten är hög för notifikationer.
// ============================================================

using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Events;
using NexaPay.Domain.Events;
using NexaPay.Infrastructure.Persistence;

namespace NexaPay.Infrastructure.Events
{
    public class OutboxDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxDispatcher> _logger;

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
        private const int BatchSize = 50;

        public OutboxDispatcher(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    // Dispatcher får aldrig dö – en bugg i en handler ska
                    // inte ta hela bakgrundsloopen. Logga och fortsätt.
                    _logger.LogError(ex, "Outbox-dispatcher iteration misslyckades");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (TaskCanceledException) { /* shutdown */ }
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            var pending = await dbContext.OutboxEvents
                .Where(o => o.ProcessedAt == null)
                .OrderBy(o => o.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0)
                return;

            foreach (var row in pending)
            {
                try
                {
                    var eventType = Type.GetType(row.EventTypeName, throwOnError: false);
                    if (eventType is null)
                        throw new InvalidOperationException(
                            $"Kunde inte ladda eventtyp '{row.EventTypeName}'.");

                    var domainEvent = (IDomainEvent?)JsonSerializer.Deserialize(row.PayloadJson, eventType)
                        ?? throw new InvalidOperationException(
                            $"Deserialisering av '{row.EventTypeName}' gav null.");

                    var notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
                    var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;

                    await publisher.Publish(notification, cancellationToken);

                    row.ProcessedAt = DateTime.UtcNow;
                    row.Error = null;
                }
                catch (Exception ex)
                {
                    row.Attempts++;
                    row.Error = ex.Message;
                    _logger.LogWarning(ex,
                        "Outbox-event {OutboxId} ({EventType}) misslyckades (försök {Attempts})",
                        row.Id, row.EventTypeName, row.Attempts);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
