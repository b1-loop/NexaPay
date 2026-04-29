// ============================================================
// AuthController.cs – NexaPay.API/Controllers
// ============================================================
// Uppdaterad med rollbaserad registrering.
// Istället för isAdmin (bool) skickar vi nu en rollsträng.
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Features.Auth.Commands.Login;
using NexaPay.Application.Features.Auth.Commands.Register;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // --------------------------------------------------------
        // POST api/auth/register
        // --------------------------------------------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            var result = await _mediator.Send(
                new RegisterCommand
                {
                    Email = request.Email,
                    Password = request.Password,
                    // Skicka roll direkt istället för bool isAdmin
                    Role = request.Role
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Användaren registrerades framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/auth/login
        // --------------------------------------------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            var result = await _mediator.Send(
                new LoginCommand
                {
                    Email = request.Email,
                    Password = request.Password
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Inloggning lyckades"));

            return Unauthorized(ApiResponse.Fail(result.Error));
        }
    }

    // --------------------------------------------------------
    // Request-modeller
    // --------------------------------------------------------
    public record RegisterRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;

        // Roll istället för bool isAdmin
        // Standard är User – den säkraste standardrollen
        // Giltiga värden: Admin, BankManager, Teller, Auditor, User
        public string Role { get; init; } = Roles.User;
    }

    public record LoginRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}