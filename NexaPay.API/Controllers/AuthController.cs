using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using NexaPay.API.Contracts;
using NexaPay.API.Extensions;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.DTOs;
using NexaPay.Application.Features.Auth.Commands.Login;
using NexaPay.Application.Features.Auth.Commands.Register;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [EnableRateLimiting("auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ITokenDenylist _tokenDenylist;
        private readonly IAuthService _authService;

        public AuthController(IMediator mediator, ITokenDenylist tokenDenylist, IAuthService authService)
        {
            _mediator = mediator;
            _tokenDenylist = tokenDenylist;
            _authService = authService;
        }

        // --------------------------------------------------------
        // POST api/auth/register
        // --------------------------------------------------------
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (request.Role != Roles.User)
                return BadRequest(ApiResponse.Fail(
                    "Personalroller kan inte registreras via denna endpoint. Kontakta en Admin."));

            var result = await _mediator.Send(new RegisterCommand
            {
                Email = request.Email,
                Password = request.Password,
                Role = Roles.User
            });

            if (!result.IsSuccess)
                return BadRequest(ApiResponse.Fail(result.Error));

            if (result.Value!.RequiresEmailConfirmation)
                return Ok(ApiResponse.Ok(
                    new { result.Value.Email },
                    "Registrering lyckades. Kontrollera din e-post och bekräfta ditt konto innan du loggar in."));

            return Ok(ApiResponse.Ok(result.Value, "Användaren registrerades framgångsrikt"));
        }

        // --------------------------------------------------------
        // POST api/auth/login
        // --------------------------------------------------------
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<AuthDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _mediator.Send(new LoginCommand
            {
                Email = request.Email,
                Password = request.Password
            });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value, "Inloggning lyckades"));

            return Unauthorized(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/auth/confirm-email
        // --------------------------------------------------------
        [HttpPost("confirm-email")]
        [DisableRateLimiting]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            var result = await _authService.ConfirmEmailAsync(request.UserId, request.Token);

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(message: "E-postadressen har bekräftats. Du kan nu logga in."));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/auth/forgot-password
        // --------------------------------------------------------
        [HttpPost("forgot-password")]
        [DisableRateLimiting]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await _authService.ForgotPasswordAsync(request.Email);

            // Returnera alltid success – avslöja inte om e-posten finns
            return Ok(ApiResponse.Ok(
                message: "Om e-postadressen finns registrerad skickas ett återställningsmail inom kort."));
        }

        // --------------------------------------------------------
        // POST api/auth/reset-password
        // --------------------------------------------------------
        [HttpPost("reset-password")]
        [DisableRateLimiting]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _authService.ResetPasswordAsync(
                request.Email, request.Token, request.NewPassword);

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(message: "Lösenordet har återställts. Du kan nu logga in."));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/auth/change-password
        // --------------------------------------------------------
        [HttpPost("change-password")]
        [Authorize]
        [DisableRateLimiting]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var result = await _authService.ChangePasswordAsync(
                User.GetUserId(), request.CurrentPassword, request.NewPassword);

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(message: "Lösenordet har bytts."));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // GET api/auth/me
        // --------------------------------------------------------
        // Returnerar den inloggade användarens profildata från JWT-claims.
        // Användbart om frontend behöver fräscha data utan ny inloggning.
        [HttpGet("me")]
        [Authorize]
        [DisableRateLimiting]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Me()
        {
            return Ok(ApiResponse.Ok(new
            {
                UserId = User.GetUserId(),
                Email  = User.GetEmail(),
                Role   = User.GetRole()
            }));
        }

        // --------------------------------------------------------
        // POST api/auth/logout
        // --------------------------------------------------------
        [HttpPost("logout")]
        [Authorize]
        [DisableRateLimiting]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
}
