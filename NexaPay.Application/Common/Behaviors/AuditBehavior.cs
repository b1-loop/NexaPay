using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;

namespace NexaPay.Application.Common.Behaviors
{
    // Loggar alla kommandon (ej queries) med vem som utförde dem och om det lyckades.
    // Körs sist i pipeline – efter validering – så bara giltiga kommandon auditeras.
    public class AuditBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;
        private readonly IAuditService _auditService;

        public AuditBehavior(
            ILogger<AuditBehavior<TRequest, TResponse>> logger,
            IAuditService auditService)
        {
            _logger = logger;
            _auditService = auditService;
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

            var userId = typeof(TRequest)
                .GetProperty("UserId")
                ?.GetValue(request)
                ?.ToString() ?? "system";

            var response = await next();

            var isSuccess = response is IResult result && result.IsSuccess;

            _logger.LogInformation(
                "AUDIT | {Command} | User: {UserId} | Success: {Success} | {Timestamp:O}",
                requestName,
                userId,
                isSuccess,
                DateTime.UtcNow);

            await _auditService.LogAsync(requestName, userId, isSuccess, cancellationToken);

            return response;
        }
    }
}
