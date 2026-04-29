// ============================================================
// CardsController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar alla kort-relaterade HTTP-endpoints.
//
// Controllern är tunn och delegerar all logik till MediatR.
// Den känner inte till Identity, databaser eller affärsregler.
//
// Endpoints:
//   GET api/cards/account/{accountId} ← Hämta kort per konto
//   POST api/cards                    ← Skapa nytt kort
//   PUT  api/cards/{id}/block         ← Blockera kort (Admin only)
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Features.Cards.Commands.BlockCard;
using NexaPay.Application.Features.Cards.Commands.CreateCard;
using NexaPay.Application.Features.Cards.Queries.GetCardsByAccount;
using System.Security.Claims;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CardsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CardsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // Hämta inloggad användares ID från JWT-token
        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        // Kontrollera om användaren är Admin
        private bool IsAdmin() => User.IsInRole("Admin");

        // --------------------------------------------------------
        // GET api/cards/account/{accountId}
        // --------------------------------------------------------
        // Hämtar alla kort kopplade till ett specifikt konto
        // Kontrollerar att användaren äger kontot (eller är Admin)
        [HttpGet("account/{accountId:guid}")]
        public async Task<IActionResult> GetByAccount(Guid accountId)
        {
            var result = await _mediator.Send(
                new GetCardsByAccountQuery
                {
                    AccountId = accountId,
                    UserId = GetUserId(),
                    IsAdmin = IsAdmin()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return NotFound(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/cards
        // --------------------------------------------------------
        // Skapar ett nytt bankkort kopplat till ett konto
        // Kortet skapas alltid med status Inactive
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateCardRequest request)
        {
            var result = await _mediator.Send(
                new CreateCardCommand
                {
                    AccountId = request.AccountId,
                    CardHolderName = request.CardHolderName,
                    // UserId från JWT – kontrollerar att kontot tillhör användaren
                    UserId = GetUserId()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Kort skapades framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // PUT api/cards/{id}/block
        // --------------------------------------------------------
        // Blockerar ett bankkort – bara Admin kan göra detta
        // [Authorize(Roles = "Admin")] = 403 Forbidden om inte Admin
        [HttpPut("{id:guid}/block")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Block(
            Guid id,
            [FromBody] BlockCardRequest request)
        {
            var result = await _mediator.Send(
                new BlockCardCommand
                {
                    CardId = id,
                    Reason = request.Reason,
                    // AdminId för loggning och revisionsspår
                    AdminId = GetUserId()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    message: "Kort blockerades framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }
    }

    // --------------------------------------------------------
    // Request-modeller
    // --------------------------------------------------------

    // Request-modell för POST /api/cards
    public record CreateCardRequest
    {
        // Vilket konto kortet ska kopplas till
        public Guid AccountId { get; init; }

        // Namnet som ska stå på kortet – t.ex. "ANNA SVENSSON"
        public string CardHolderName { get; init; } = string.Empty;
    }

    // Request-modell för PUT /api/cards/{id}/block
    public record BlockCardRequest
    {
        // Obligatorisk anledning till blockeringen
        // Viktigt för revisionsspår i ett banksystem
        public string Reason { get; init; } = string.Empty;
    }
}