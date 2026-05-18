// ============================================================
// ResultExtensions.cs – NexaPay.API/Extensions
// ============================================================
// Översätter ett misslyckat Result från Application-lagret till
// rätt HTTP-status. Controllers slipper duplicera 'if NotFound
// → 404 else → 400'-logiken på varje endpoint.
//   NotFound      → 404 (resursen finns inte / är inte synlig)
//   BusinessRule  → 400 (domänregel bröts, t.ex. otillräckligt saldo)
// ============================================================

using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Common.Models;

namespace NexaPay.API.Extensions
{
    public static class ResultExtensions
    {
        public static IActionResult ToErrorResponse(this ControllerBase controller, Result result) =>
            result.ErrorType == ResultErrorType.NotFound
                ? controller.NotFound(ApiResponse.Fail(result.Error))
                : controller.BadRequest(ApiResponse.Fail(result.Error));
    }
}
