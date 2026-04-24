// ============================================================
// ExceptionMiddleware.cs – NexaPay.API/Middleware
// ============================================================
// Global felhantering för hela API:et.
// Fångar alla ohanterade exceptions och returnerar
// ett strukturerat JSON-svar med rätt HTTP-statuskod.
//
// Utan denna middleware skulle ASP.NET returnera en ful
// HTML-felsida vid exceptions – inte bra för ett API!
//
// Mappning av exceptions till HTTP-statuskoder:
//   ValidationException → 400 Bad Request
//   NotFoundException   → 404 Not Found
//   Övriga exceptions   → 500 Internal Server Error
// ============================================================

using NexaPay.Application.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace NexaPay.API.Middleware
{
    public class ExceptionMiddleware
    {
        // RequestDelegate = nästa middleware i kedjan
        // Om inget fel uppstår anropar vi _next för att
        // skicka requesten vidare
        private readonly RequestDelegate _next;

        // ILogger för att logga felen
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        // InvokeAsync anropas för varje HTTP-request
        // "context" innehåller all information om requesten och svaret
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Försök köra nästa middleware i kedjan
                // Om allt går bra returneras svaret normalt
                await _next(context);
            }
            catch (Application.Common.Exceptions.ValidationException ex)
            {
                // ValidationException kastas av ValidationBehavior
                // när FluentValidation-regler misslyckas
                // HTTP 400 Bad Request är rätt statuskod
                _logger.LogWarning(
                    "Valideringsfel: {@Errors}", ex.Errors);

                await HandleExceptionAsync(
                    context,
                    HttpStatusCode.BadRequest,
                    "Valideringsfel",
                    ex.Errors);
            }
            catch (NotFoundException ex)
            {
                // NotFoundException kastas när en resurs inte hittas
                // HTTP 404 Not Found
                _logger.LogWarning(
                    "Resurs hittades inte: {Message}", ex.Message);

                await HandleExceptionAsync(
                    context,
                    HttpStatusCode.NotFound,
                    ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Kastas vid behörighetsfel
                // HTTP 403 Forbidden
                _logger.LogWarning(
                    "Obehörig åtkomst: {Message}", ex.Message);

                await HandleExceptionAsync(
                    context,
                    HttpStatusCode.Forbidden,
                    ex.Message);
            }
            catch (Exception ex)
            {
                // Alla andra exceptions är oväntade serverfel
                // HTTP 500 Internal Server Error
                _logger.LogError(
                    ex,
                    "Oväntat fel: {Message}", ex.Message);

                await HandleExceptionAsync(
                    context,
                    HttpStatusCode.InternalServerError,
                    "Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        // --------------------------------------------------------
        // Hjälpmetod utan errors-dictionary (för enkla fel)
        // --------------------------------------------------------
        private static async Task HandleExceptionAsync(
            HttpContext context,
            HttpStatusCode statusCode,
            string message)
        {
            await HandleExceptionAsync(context, statusCode, message, null);
        }

        // --------------------------------------------------------
        // Huvud-implementation med valfritt errors-dictionary
        // --------------------------------------------------------
        private static async Task HandleExceptionAsync(
            HttpContext context,
            HttpStatusCode statusCode,
            string message,
            object? errors)
        {
            // Sätt rätt Content-Type och statuskod på svaret
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            // Skapa ett strukturerat felsvar
            var response = new
            {
                // HTTP-statuskoden som heltal (t.ex. 400, 404, 500)
                status = (int)statusCode,

                // Felmeddelandet
                message,

                // Valideringsfel om de finns (null annars)
                errors,

                // Tidsstämpel för när felet uppstod
                timestamp = DateTime.UtcNow
            };

            // Serialisera till JSON med camelCase-namngivning
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, jsonOptions));
        }
    }
}