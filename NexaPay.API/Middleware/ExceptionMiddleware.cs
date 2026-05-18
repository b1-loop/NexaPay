// ============================================================
// ExceptionMiddleware.cs – NexaPay.API/Middleware
// ============================================================
// Global felhantering för hela API:et.
// Fångar alla ohanterade exceptions och returnerar
// ett RFC 7807-formaterat ProblemDetails-svar i JSON.
//
// Mappning av exceptions till HTTP-statuskoder:
//   ValidationException  → 400 Bad Request (ValidationProblemDetails)
//   UnauthorizedAccess   → 403 Forbidden
//   ConcurrencyException → 409 Conflict
//   Övriga exceptions    → 500 Internal Server Error
//
// Not-found och business-rule-fel hanteras via Result<T>.ErrorType
// i handlers och mappas till 404/400 av controllers via ToErrorResponse().
// ============================================================

using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Common.Exceptions;
using NexaPay.Domain.Exceptions;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexaPay.API.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Application.Common.Exceptions.ValidationException ex)
            {
                _logger.LogWarning("Valideringsfel: {@Errors}", ex.Errors);
                await WriteValidationProblemAsync(context, ex.Errors);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Obehörig åtkomst: {Message}", ex.Message);
                await WriteProblemAsync(context, HttpStatusCode.Forbidden, "Obehörig åtkomst", ex.Message);
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogWarning("Konkurrensproblem: {Message}", ex.Message);
                await WriteProblemAsync(context, HttpStatusCode.Conflict, "Konkurrenskonflikt", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel: {Message}", ex.Message);
                await WriteProblemAsync(context, HttpStatusCode.InternalServerError,
                    "Internt serverfel", "Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        // RFC 7807 ProblemDetails. Content-Type är application/problem+json.
        private static Task WriteProblemAsync(
            HttpContext context,
            HttpStatusCode statusCode,
            string title,
            string? detail)
        {
            var problem = new ProblemDetails
            {
                Status = (int)statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path,
                Type = $"https://httpstatuses.io/{(int)statusCode}"
            };
            return WriteAsync(context, statusCode, problem);
        }

        // RFC 7807 ValidationProblemDetails – innehåller en "errors"-dictionary
        // med fältnamn → meddelanden, vilket standard-klienter kan rendera direkt.
        private static Task WriteValidationProblemAsync(
            HttpContext context,
            IDictionary<string, string[]> errors)
        {
            var problem = new ValidationProblemDetails(errors)
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Valideringsfel",
                Instance = context.Request.Path,
                Type = $"https://httpstatuses.io/{(int)HttpStatusCode.BadRequest}"
            };
            return WriteAsync(context, HttpStatusCode.BadRequest, problem);
        }

        private static async Task WriteAsync(HttpContext context, HttpStatusCode statusCode, object body)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, body.GetType(), jsonOptions));
        }
    }
}
