using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Common.Models;

namespace NexaPay.API.Extensions
{
    public static class ResultExtensions
    {
        // Maps a failed Result to the correct HTTP status based on ErrorType.
        // NotFound  → 404  (entity does not exist or is not visible to caller)
        // BusinessRule → 400  (domain rule violation, e.g. insufficient funds)
        public static IActionResult ToErrorResponse(this ControllerBase controller, Result result) =>
            result.ErrorType == ResultErrorType.NotFound
                ? controller.NotFound(ApiResponse.Fail(result.Error))
                : controller.BadRequest(ApiResponse.Fail(result.Error));
    }
}
