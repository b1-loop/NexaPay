// ============================================================
// AdminController.cs – NexaPay.API/Controllers
// ============================================================
// Endpoints som endast Admin får anropa:
//   POST   /api/admin/users           – skapa personal-/användarkonto
//   GET    /api/admin/users           – lista alla användare
//   DELETE /api/admin/users/{id}      – ta bort användare
//
// Klassen är låst med [Authorize(Roles = Roles.Admin)] på toppnivå
// så att inte ens BankManager kommer åt dessa endpoints.
// ============================================================

using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(IMediator mediator, UserManager<IdentityUser> userManager)
        {
            _mediator = mediator;
            _userManager = userManager;
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
                    Role = request.Role,
                    SkipEmailConfirmation = true
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    $"Användare skapad med rollen {request.Role}"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // GET api/admin/users
        // --------------------------------------------------------
        [HttpGet("users")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUsers()
        {
            var users = _userManager.Users.ToList();
            var result = new List<object>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                result.Add(new
                {
                    id             = u.Id,
                    email          = u.Email,
                    role           = roles.FirstOrDefault() ?? "User",
                    emailConfirmed = u.EmailConfirmed,
                    lockoutEnd     = u.LockoutEnd
                });
            }
            return Ok(ApiResponse.Ok(result));
        }

        // --------------------------------------------------------
        // DELETE api/admin/users/{id}
        // --------------------------------------------------------
        [HttpDelete("users/{id}")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse.Fail("Användaren hittades inte."));

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                var errors = string.Join(", ", deleteResult.Errors.Select(e => e.Description));
                return BadRequest(ApiResponse.Fail(errors));
            }
            return Ok(ApiResponse.Ok(message: "Användaren har tagits bort."));
        }
    }

}
