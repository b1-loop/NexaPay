// ============================================================
// ValidationBehavior.cs – NexaPay.Application/Common/Behaviors
// ============================================================
// En MediatR Pipeline Behavior som automatiskt validerar
// alla inkommande requests med FluentValidation.
//
// Flöde:
//   1. Hämta alla validators för TRequest (kan vara 0 eller flera)
//   2. Om inga validators finns → fortsätt direkt till Handler
//   3. Kör alla validators parallellt
//   4. Samla ihop alla valideringsfel
//   5. Om fel finns → logga till AuditLogs (kommandon) och kasta ValidationException
//   6. Om inga fel → fortsätt till Handler
// ============================================================

using FluentValidation;
using MediatR;
using NexaPay.Application.Common.Exceptions;
using NexaPay.Application.Common.Interfaces;
using ValidationException = NexaPay.Application.Common.Exceptions.ValidationException;

namespace NexaPay.Application.Common.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        private readonly IAuditService _auditService;

        public ValidationBehavior(
            IEnumerable<IValidator<TRequest>> validators,
            IAuditService auditService)
        {
            _validators = validators;
            _auditService = auditService;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validators.Select(v =>
                    v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(result => result.Errors)
                .Where(failure => failure != null)
                .ToList();

            if (failures.Count != 0)
            {
                // Logga validationsfel för kommandon till audit-tabellen.
                // Queries auditeras inte – de förändrar inget tillstånd.
                var requestName = typeof(TRequest).Name;
                if (!requestName.EndsWith("Query"))
                {
                    var userId = typeof(TRequest)
                        .GetProperty("UserId")
                        ?.GetValue(request)
                        ?.ToString() ?? "unknown";
                    await _auditService.LogAsync(requestName, userId, false, cancellationToken);
                }

                throw new ValidationException(failures);
            }

            return await next();
        }
    }
}
