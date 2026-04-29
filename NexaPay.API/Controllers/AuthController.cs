// ============================================================
// AuthController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar registrering och inloggning.
//
// EFTER refaktorering:
// Controllern känner bara till MediatR och Commands.
// Den vet INGENTING om Identity, UserManager eller databaser.
// Det är precis vad Clean Architecture kräver!
//
// Endpoints:
//   POST /api/auth/register  ← Registrera ny användare
//   POST /api/auth/login     ← Logga in och få JWT-token
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Features.Auth.Commands.Login;
using NexaPay.Application.Features.Auth.Commands.Register;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        // Bara IMediator – ingenting annat!
        // Controllern delegerar allt till MediatR
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
            // Skapa ett Command och skicka via MediatR
            // MediatR hittar RegisterHandler automatiskt
            var command = new RegisterCommand
            {
                Email = request.Email,
                Password = request.Password,
                IsAdmin = request.IsAdmin
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return Ok(new
                {
                    message = "Användaren registrerades framgångsrikt",
                    data = result.Value
                });

            return BadRequest(new { message = result.Error });
        }

        // --------------------------------------------------------
        // POST api/auth/login
        // --------------------------------------------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            var command = new LoginCommand
            {
                Email = request.Email,
                Password = request.Password
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result.Value);

            // 401 Unauthorized vid felaktig inloggning
            return Unauthorized(new { message = result.Error });
        }
    }

    // --------------------------------------------------------
    // Request-modeller – enkla och rena
    // --------------------------------------------------------
    public record RegisterRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public bool IsAdmin { get; init; } = false;
    }

    public record LoginRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}