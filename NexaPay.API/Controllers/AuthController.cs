// ============================================================
// AuthController.cs – NexaPay.API/Controllers
// ============================================================
// Uppdaterad med rollbaserad registrering.
// Istället för isAdmin (bool) skickar vi nu en rollsträng.
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using NexaPay.API.Extensions;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Features.Auth.Commands.Login;
using NexaPay.Application.Features.Auth.Commands.Register;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ITokenDenylist _tokenDenylist;

        public AuthController(IMediator mediator, ITokenDenylist tokenDenylist)
        {
            _mediator = mediator;
            _tokenDenylist = tokenDenylist;
        }

        // --------------------------------------------------------
        // POST api/auth/register
        // --------------------------------------------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            // Personalroller skapas bara av Admin via POST /api/admin/users
            if (request.Role != Roles.User)
                return BadRequest(ApiResponse.Fail(
                    "Personalroller kan inte registreras via denna endpoint. " +
                    "Kontakta en Admin."));

            var result = await _mediator.Send(
                new RegisterCommand
                {
                    Email = request.Email,
                    Password = request.Password,
                    Role = Roles.User
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

        // --------------------------------------------------------
        // POST api/auth/logout
        // --------------------------------------------------------
        // Återkallar den aktuella JWT-token via denylist.
        // Alla efterföljande requests med samma token avvisas med 401.
        [HttpPost("logout")]
        [Authorize]
        [DisableRateLimiting]
        public IActionResult Logout()
        {
            var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

            if (jti != null && long.TryParse(expClaim, out var expUnix))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                _tokenDenylist.Revoke(jti, expiry);
            }

            return Ok(ApiResponse.Ok(message: "Utloggning lyckades"));
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