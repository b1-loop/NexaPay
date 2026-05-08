using MediatR;
using Microsoft.Extensions.Logging;

namespace NexaPay.Application.Common.Behaviors
{
    // Loggar alla kommandon (ej queries) med vem som utförde dem och om det lyckades.
    // Körs sist i pipeline – efter validering – så bara giltiga kommandon auditeras.
    // Format: "AUDIT | {Kommando} | User: {UserId} | Success: {bool}"
    // Kan routas till valfri logg-sink (Seq, Application Insights, fil, osv.)
    public class AuditBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

        public AuditBehavior(ILogger<AuditBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;

            // Queries auditeras inte – de förändrar inget tillstånd
            if (requestName.EndsWith("Query"))
                return await next();

            // Extrahera UserId via reflektion om kommandot bär det
            var userId = typeof(TRequest)
                .GetProperty("UserId")
                ?.GetValue(request)
                ?.ToString() ?? "system";

            var response = await next();

            // Läs IsSuccess från Result-mönstret via reflektion
            var isSuccess = response?.GetType()
                .GetProperty("IsSuccess")
                ?.GetValue(response) as bool? ?? true;

            _logger.LogInformation(
                "AUDIT | {Command} | User: {UserId} | Success: {Success} | {Timestamp:O}",
                requestName,
                userId,
                isSuccess,
                DateTime.UtcNow);

            return response;
        }
    }
}
