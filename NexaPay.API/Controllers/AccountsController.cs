// ============================================================
// AccountsController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar alla konto-relaterade endpoints.
//
// Endpoints:
//   GET    api/accounts         ← Hämta alla konton (RBAC)
//   GET    api/accounts/{id}    ← Hämta specifikt konto
//   POST   api/accounts         ← Skapa nytt konto
//   DELETE api/accounts/{id}    ← Stäng konto
//
// [Authorize] = kräver giltig JWT-token för alla endpoints
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Features.Accounts.Commands.CreateAccount;
using NexaPay.Application.Features.Accounts.Commands.DeleteAccount;
using NexaPay.Application.Features.Accounts.Queries.GetAccountById;
using NexaPay.Application.Features.Accounts.Queries.GetAllAccounts;
using NexaPay.Domain.Enums;
using System.Security.Claims;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // Alla endpoints i denna controller kräver inloggning
    [Authorize]
    public class AccountsController : ControllerBase
    {
        // IMediator skickar Commands och Queries till rätt Handler
        private readonly IMediator _mediator;

        public AccountsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // --------------------------------------------------------
        // Hjälpmetoder för att läsa JWT-claims
        // --------------------------------------------------------

        // Hämta inloggad användares ID från JWT-token
        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        // Kontrollera om inloggad användare är Admin
        private bool IsAdmin() =>
            User.IsInRole("Admin");

        // --------------------------------------------------------
        // GET api/accounts
        // --------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var query = new GetAllAccountsQuery
            {
                UserId = GetUserId(),
                IsAdmin = IsAdmin()
            };

            var result = await _mediator.Send(query);

            if (result.IsSuccess)
                return Ok(result.Value);

            return BadRequest(new { message = result.Error });
        }

        // --------------------------------------------------------
        // GET api/accounts/{id}
        // --------------------------------------------------------
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var query = new GetAccountByIdQuery
            {
                AccountId = id,
                UserId = GetUserId(),
                IsAdmin = IsAdmin()
            };

            var result = await _mediator.Send(query);

            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(new { message = result.Error });
        }

        // --------------------------------------------------------
        // POST api/accounts
        // --------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateAccountRequest request)
        {
            var command = new CreateAccountCommand
            {
                AccountName = request.AccountName,
                AccountType = request.AccountType,
                // OwnerId från JWT-token – ALDRIG från request-body!
                // Säkerhetskritiskt – användaren sätter inte sin
                // egen OwnerId
                OwnerId = GetUserId()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                // 201 Created med Location-header
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = result.Value!.Id },
                    result.Value);

            return BadRequest(new { message = result.Error });
        }

        // --------------------------------------------------------
        // DELETE api/accounts/{id}
        // --------------------------------------------------------
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var command = new DeleteAccountCommand
            {
                AccountId = id,
                UserId = GetUserId(),
                IsAdmin = IsAdmin()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                // 204 No Content = lyckad borttagning utan returdata
                return NoContent();

            return BadRequest(new { message = result.Error });
        }
    }

    // --------------------------------------------------------
    // Request-modell för POST /api/accounts
    // --------------------------------------------------------
    public record CreateAccountRequest
    {
        public string AccountName { get; init; } = string.Empty;
        public AccountType AccountType { get; init; }
    }
}