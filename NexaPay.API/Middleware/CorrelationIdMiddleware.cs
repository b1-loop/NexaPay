// ============================================================
// CorrelationIdMiddleware.cs – NexaPay.API/Middleware
// ============================================================
// Plockar X-Correlation-Id-headern från inkommande request,
// eller genererar en ny om den saknas. ID:t läggs i Serilogs
// LogContext så att alla loggrader för en request kan kopplas
// ihop, och eko-skickas tillbaka i svaret så klienten kan
// referera den i felrapporter.
// ============================================================

using Serilog.Context;

namespace NexaPay.API.Middleware
{
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.ToString()
                : Guid.NewGuid().ToString();

            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
