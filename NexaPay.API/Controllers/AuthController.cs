// ============================================================
// AuthController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar registrering och inloggning.
//
// EFTER refaktorering för Clean Architecture:
// Controllern känner bara till MediatR och Commands.
// Den vet INGENTING om Identity, UserManager eller databaser.
//
// Flödet:
//   Controller → MediatR → Handler → IAuthService → AuthService
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
    // Ingen [Authorize] här – dessa endpoints är publika
    // Användaren är inte inloggad när de registrerar/loggar in
    public class AuthController : ControllerBase
    {
        // Bara IMediator – ingenting annat!
        // Controllern delegerar allt till MediatR
        // som hittar rätt Handler automatiskt
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // --------------------------------------------------------
        // POST api/auth/register
        // --------------------------------------------------------
        // Registrerar en ny användare och returnerar en JWT-token
        // Klienten är direkt inloggad efter registrering
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            // Skapa Command från request-body
            // MediatR hittar RegisterHandler automatiskt
            var result = await _mediator.Send(
                new RegisterCommand
                {
                    Email = request.Email,
                    Password = request.Password,
                    IsAdmin = request.IsAdmin
                });

            if (result.IsSuccess)
                // 200 OK med token och användarinfo
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Användaren registrerades framgångsrikt"));

            // 400 Bad Request om registreringen misslyckades
            // T.ex. om e-posten redan används
            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/auth/login
        // --------------------------------------------------------
        // Loggar in en befintlig användare och returnerar JWT-token
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
                // 200 OK med token och användarinfo
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Inloggning lyckades"));

            // 401 Unauthorized vid fel lösenord eller e-post
            // Vi använder samma felmeddelande för båda fallen
            // av säkerhetsskäl – avslöjar inte om e-posten finns
            return Unauthorized(ApiResponse.Fail(result.Error));
        }
    }

    // --------------------------------------------------------
    // Request-modeller
    // --------------------------------------------------------

    // Request-modell för POST /api/auth/register
    public record RegisterRequest
    {
        // E-postadressen för den nya användaren
        public string Email { get; init; } = string.Empty;

        // Lösenordet – valideras av RegisterValidator
        // Hashas av Identity i AuthService
        public string Password { get; init; } = string.Empty;

        // Om true skapas användaren med Admin-rollen
        // Standard är false – vanlig User
        public bool IsAdmin { get; init; } = false;
    }

    // Request-modell för POST /api/auth/login
    public record LoginRequest
    {
        // E-postadressen för användaren som loggar in
        public string Email { get; init; } = string.Empty;

        // Lösenordet som kontrolleras mot det hashade värdet
        public string Password { get; init; } = string.Empty;
    }
}