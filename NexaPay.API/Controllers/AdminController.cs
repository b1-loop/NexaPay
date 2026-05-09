using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaPay.API.Contracts;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.DTOs;
using NexaPay.Application.Features.Auth.Commands.Register;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [Authorize(Roles = Roles.Admin)]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // --------------------------------------------------------
        // POST api/admin/users
        // --------------------------------------------------------
        // Skapar ett konto med valfri roll.
        // Kräver Admin-token.
        // Personalroller (Admin, BankManager, Teller, Auditor)
        // kräver fortfarande @nexapay.com-epost (hanteras av RegisterHandler).
        [HttpPost("users")]
        [ProducesResponseType(typeof(ApiResponse<AuthDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateUser(
            [FromBody] AdminCreateUserRequest request)
        {
            var result = await _mediator.Send(
                new RegisterCommand
                {
                    Email = request.Email,
                    Password = request.Password,
                    Role = request.Role
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    $"Användare skapad med rollen {request.Role}"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }
    }

}
