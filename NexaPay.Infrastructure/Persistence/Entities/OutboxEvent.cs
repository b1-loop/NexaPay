// ============================================================
// OutboxEvent.cs – NexaPay.Infrastructure/Persistence/Entities
// ============================================================
// En rad per domän-event som persisteras i SAMMA databastransaktion
// som det aggregat som genererade eventet. En BackgroundService
// (OutboxDispatcher) plockar oprocessade rader och dispatchar dem
// via MediatR i bakgrunden. Detta löser två problem:
//
//   1. SMTP/Redis hänger inte fast i request-tråden.
//   2. Save + dispatch är atomärt: antingen finns både entitet
//      och event i databasen, eller ingenting. Inga "persisted
//      but not notified"-tillstånd.
//
// Outbox-tabellen är intern till Infrastructure – Domain och
// Application känner inte till den.
// ============================================================

namespace NexaPay.Infrastructure.Persistence.Entities
{
    public class OutboxEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Fully qualified type name så dispatchern kan ladda typen
        // via Type.GetType() utan att gissa assembly.
        public string EventTypeName { get; set; } = string.Empty;

        // JSON-serialiserad payload av det ursprungliga eventet.
        public string PayloadJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Null tills dispatchern lyckats publicera eventet.
        public DateTime? ProcessedAt { get; set; }

        // Sista felmeddelande – för felsökning av retries.
        public string? Error { get; set; }

        public int Attempts { get; set; }
    }
}
