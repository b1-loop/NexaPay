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

using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexaPay.API.Contracts;
using NexaPay.API.Extensions;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.DTOs;
using NexaPay.Application.Features.Auth.Commands.Register;
using NexaPay.Infrastructure.Persistence;

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
        private readonly ApplicationDbContext _dbContext;

        public AdminController(
            IMediator mediator,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext dbContext)
        {
            _mediator = mediator;
            _userManager = userManager;
            _dbContext = dbContext;
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
        public async Task<IActionResult> GetUsers(
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 50)
        {
            // Async + paginerad query mot Identity-tabellerna direkt.
            // Ersätter tidigare _userManager.Users.ToList() (sync) +
            // GetRolesAsync i loop (N+1).
            var pagedUsers = await _userManager.Users
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userIds = pagedUsers.Select(u => u.Id).ToList();

            // Ett enda JOIN-anrop för alla rollkopplingar i den paginerade
            // sidan – inte N anrop. En användare antas ha exakt en roll
            // (avsiktlig design enligt JWT-genereringen).
            var rolesByUserId = await (
                from ur in _dbContext.UserRoles
                join r in _dbContext.Roles on ur.RoleId equals r.Id
                where userIds.Contains(ur.UserId)
                select new { ur.UserId, r.Name }
            ).ToDictionaryAsync(x => x.UserId, x => x.Name);

            var items = pagedUsers.Select(u => new
            {
                id             = u.Id,
                email          = u.Email,
                role           = rolesByUserId.GetValueOrDefault(u.Id) ?? "User",
                emailConfirmed = u.EmailConfirmed,
                lockoutEnd     = u.LockoutEnd
            }).ToList();

            return Ok(ApiResponse.Ok(items));
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
            // Skydd 1: en admin får inte ta bort sitt eget konto –
            // det skulle göra dem permanent utelåsta från admin-UIt.
            if (id == User.GetUserId())
                return BadRequest(ApiResponse.Fail("Du kan inte ta bort ditt eget konto."));

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse.Fail("Användaren hittades inte."));

            // Skydd 2: vägra ta bort den sista administratören. Annars riskerar
            // systemet att hamna i ett tillstånd utan någon som kan administrera.
            var targetRoles = await _userManager.GetRolesAsync(user);
            if (targetRoles.Contains(Roles.Admin))
            {
                var allAdmins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
                if (allAdmins.Count <= 1)
                    return BadRequest(ApiResponse.Fail(
                        "Kan inte ta bort den sista administratören. Skapa en till administratör först."));
            }

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
