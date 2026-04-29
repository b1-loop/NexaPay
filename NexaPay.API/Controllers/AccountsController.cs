// ============================================================
// AccountsController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar alla konto-relaterade HTTP-endpoints.
//
// Controllern är medvetet TUNN – den gör bara tre saker:
//   1. Ta emot HTTP-request
//   2. Skicka ett Command/Query till MediatR
//   3. Returnera ett standardiserat ApiResponse
//
// All affärslogik finns i Application-lagrets Handlers.
// Controllern känner INTE till databaser eller Identity.
//
// Endpoints:
//   GET    api/accounts         ← Hämta alla konton (RBAC)
//   GET    api/accounts/{id}    ← Hämta specifikt konto
//   POST   api/accounts         ← Skapa nytt konto
//   DELETE api/accounts/{id}    ← Stäng konto
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
    // Alla endpoints i denna controller kräver giltig JWT-token
    [Authorize]
    public class AccountsController : ControllerBase
    {
        // IMediator är vår enda dependency – vi pratar bara med MediatR
        // MediatR skickar vidare till rätt Handler automatiskt
        private readonly IMediator _mediator;

        public AccountsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // --------------------------------------------------------
        // Hjälpmetoder för att läsa JWT-claims
        // --------------------------------------------------------

        // Hämta inloggad användares ID från JWT-token
        // ClaimTypes.NameIdentifier = "sub"-claimet vi satte i JwtService
        // Returnerar tom sträng om claim inte finns (bör inte hända)
        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        // Kontrollera om inloggad användare har Admin-rollen
        // Används för RBAC – Role-Based Access Control
        private bool IsAdmin() => User.IsInRole("Admin");

        // --------------------------------------------------------
        // GET api/accounts
        // --------------------------------------------------------
        // Admin ser alla konton i systemet
        // Vanlig User ser bara sina egna konton
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Skapa Query med användarinfo från JWT-token
            var result = await _mediator.Send(
                new GetAllAccountsQuery
                {
                    // UserId och IsAdmin hämtas från JWT-token
                    // Inte från request-body – det vore en säkerhetsrisk
                    UserId = GetUserId(),
                    IsAdmin = IsAdmin()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // GET api/accounts/{id}
        // --------------------------------------------------------
        // Hämtar ett specifikt konto om användaren har behörighet
        // ":guid" = URL-parametern måste vara ett giltigt Guid
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _mediator.Send(
                new GetAccountByIdQuery
                {
                    AccountId = id,
                    UserId = GetUserId(),
                    IsAdmin = IsAdmin()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            // 404 om kontot inte finns eller användaren saknar behörighet
            // Vi returnerar samma fel i båda fallen av säkerhetsskäl
            return NotFound(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/accounts
        // --------------------------------------------------------
        // Skapar ett nytt bankkonto för den inloggade användaren
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateAccountRequest request)
        {
            var result = await _mediator.Send(
                new CreateAccountCommand
                {
                    AccountName = request.AccountName,
                    AccountType = request.AccountType,
                    // OwnerId sätts från JWT-token – ALDRIG från request!
                    // Kritisk säkerhetsregel – användaren kan inte
                    // sätta sig själv som ägare av ett annat konto
                    OwnerId = GetUserId()
                });

            if (result.IsSuccess)
                // 201 Created med Location-header som pekar på det nya kontot
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = result.Value!.Id },
                    ApiResponse.Ok(
                        result.Value,
                        "Konto skapades framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // DELETE api/accounts/{id}
        // --------------------------------------------------------
        // Stänger ett konto (soft delete – kontot markeras som inaktivt)
        // Bara ägaren eller Admin kan stänga ett konto
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _mediator.Send(
                new DeleteAccountCommand
                {
                    AccountId = id,
                    UserId = GetUserId(),
                    IsAdmin = IsAdmin()
                });

            if (result.IsSuccess)
                // 200 OK med bekräftelsemeddelande
                // Vi returnerar 200 istället för 204 för att
                // inkludera bekräftelsemeddelandet i ApiResponse
                return Ok(ApiResponse.Ok(
                    message: "Konto stängdes framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }
    }

    // --------------------------------------------------------
    // Request-modell för POST /api/accounts
    // --------------------------------------------------------
    // Enkel modell som representerar request-body
    // OwnerId inkluderas INTE här – den hämtas från JWT-token
    public record CreateAccountRequest
    {
        // Namnet användaren ger sitt konto
        // T.ex. "Mitt sparkonto" eller "Hushållskassan"
        public string AccountName { get; init; } = string.Empty;

        // Typen av konto – Checking, Savings eller ISK
        public AccountType AccountType { get; init; }
    }
}