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
//   5. Om fel finns → kasta ValidationException (Handler körs ALDRIG)
//   6. Om inga fel → fortsätt till Handler
// ============================================================

using FluentValidation;
using NexaPay.Application.Common.Exceptions;
using ValidationException = NexaPay.Application.Common.Exceptions.ValidationException;
using MediatR;

namespace NexaPay.Application.Common.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        // IEnumerable<IValidator<TRequest>> injiceras av DI-containern
        // DI hittar automatiskt ALLA validators för TRequest
        // T.ex. för DepositCommand injiceras DepositCommandValidator
        // Om ingen validator finns är listan tom – inte null
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // Om det inte finns några validators för denna request
            // hoppar vi direkt vidare till nästa steg i pipeline
            // Det är viktigt att inte kräva validators för alla requests
            if (!_validators.Any())
            {
                return await next();
            }

            // Skapa en ValidationContext för requesten
            // ValidationContext innehåller objektet som ska valideras
            var context = new ValidationContext<TRequest>(request);

            // Kör alla validators parallellt med Task.WhenAll för bättre prestanda
            // Istället för att köra dem en i taget väntar vi på alla samtidigt
            var validationResults = await Task.WhenAll(
                _validators.Select(v =>
                    v.ValidateAsync(context, cancellationToken)));

            // Samla ihop alla valideringsfel från alla validators
            // SelectMany plattar ut listan av listor till en enda lista
            // Where filtrerar bort tomma fel (lyckade valideringar)
            var failures = validationResults
                .SelectMany(result => result.Errors)
                .Where(failure => failure != null)
                .ToList();

            // Om det finns valideringsfel – kasta ValidationException
            // Detta stoppar pipeline och handleren körs ALDRIG
            // ExceptionMiddleware i API-lagret fångar detta och returnerar 400
            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }

            // Inga valideringsfel – fortsätt till nästa steg i pipeline
            return await next();
        }
    }
}