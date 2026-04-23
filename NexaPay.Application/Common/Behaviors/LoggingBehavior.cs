// ============================================================
// LoggingBehavior.cs – NexaPay.Application/Common/Behaviors
// ============================================================
// En MediatR Pipeline Behavior som automatiskt loggar
// alla inkommande requests och deras svarstider.
//
// Loggar:
//   - När en request börjar behandlas (med requestens data)
//   - När en request är klar (med hur lång tid det tog)
//   - Om en request tar för lång tid (varning)
//
// TRequest = typen på requesten (t.ex. DepositCommand)
// TResponse = typen på svaret (t.ex. Result<TransactionDto>)
// ============================================================

using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics; // Stopwatch för att mäta tid

namespace NexaPay.Application.Common.Behaviors
{
    // IPipelineBehavior<TRequest, TResponse> är MediatRs interface för behaviors
    // Vi implementerar det generiskt så det gäller för ALLA requests automatiskt
    public class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull // TRequest får inte vara null
    {
        // ILogger injiceras via Dependency Injection
        // Vi loggar med den requestens typnamn som kategori
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        // Konstruktor – ILogger injiceras automatiskt av DI-containern
        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        // Handle är den metod MediatR anropar för varje request
        // "next" är nästa steg i pipeline – antingen nästa behavior eller handleren
        // Vi måste anropa "next()" för att requesten ska fortsätta framåt
        public async Task<TResponse> Handle(
            TRequest request,           // Requesten som kom in (t.ex. DepositCommand)
            RequestHandlerDelegate<TResponse> next, // Nästa steg i pipeline
            CancellationToken cancellationToken)
        {
            // Hämta requestens typnamn för logging (t.ex. "DepositCommand")
            var requestName = typeof(TRequest).Name;

            // Logga att vi börjat behandla requesten
            // LogInformation = normal information (inte fel, inte varning)
            _logger.LogInformation(
                "NexaPay: Hanterar request {@RequestName} {@Request}",
                requestName,
                request); // Loggar hela request-objektet med alla properties

            // Starta en stopwatch för att mäta hur lång tid det tar
            var stopwatch = Stopwatch.StartNew();

            TResponse response;

            try
            {
                // Anropa nästa steg i pipeline (nästa behavior eller handleren)
                // "await" väntar på att det asynkrona anropet ska slutföras
                response = await next();
            }
            finally
            {
                // finally körs ALLTID – oavsett om det gick bra eller inte
                stopwatch.Stop();

                var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                // Om requesten tog mer än 500ms – logga en varning
                // Det kan indikera ett prestandaproblem (långsam databasfråga osv.)
                if (elapsedMilliseconds > 500)
                {
                    _logger.LogWarning(
                        "NexaPay: Långsam request {@RequestName} ({@ElapsedMilliseconds}ms) {@Request}",
                        requestName,
                        elapsedMilliseconds,
                        request);
                }
                else
                {
                    // Normal loggning av svarstid
                    _logger.LogInformation(
                        "NexaPay: Request klar {@RequestName} ({@ElapsedMilliseconds}ms)",
                        requestName,
                        elapsedMilliseconds);
                }
            }

            return response;
        }
    }
}