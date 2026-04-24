// ============================================================
// CardsController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar alla kort-relaterade endpoints.
//
// Endpoints:
//   GET api/cards/account/{accountId} ← Hämta kort per konto
//   POST api/cards                    ← Skapa nytt kort
//   PUT  api/cards/{id}/block         ← Blockera kort (Admin)
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

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        private bool IsAdmin() => User.IsInRole("Admin");

        // --------------------------------------------------------
        // GET api/cards/account/{accountId}
        // --------------------------------------------------------
        [HttpGet("account/{accountId:guid}")]
        public async Task<IActionResult> GetByAccount(Guid accountId)
        {
            var query = new GetCardsByAccountQuery
            {
                AccountId = accountId,
                UserId = GetUserId(),
                IsAdmin = IsAdmin()
            };

            var result = await _mediator.Send(query);

            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(new { message = result.Error });
        }

        // --------------------------------------------------------
        // POST api/cards
        // --------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateCardRequest request)
        {
            var command = new CreateCardCommand
            {
                AccountId = request.AccountId,
                CardHolderName = request.CardHolderName,
                UserId = GetUserId()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result.Value);

            return BadRequest(new { message = result.Error });
        }

        // --------------------------------------------------------
        // PUT api/cards/{id}/block
        // --------------------------------------------------------
        // Bara Admin kan blockera kort
        [HttpPut("{id:guid}/block")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Block(
            Guid id,
            [FromBody] BlockCardRequest request)
        {
            var command = new BlockCardCommand
            {
                CardId = id,
                Reason = request.Reason,
                AdminId = GetUserId()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return NoContent();

            return BadRequest(new { message = result.Error });
        }
    }

    // --------------------------------------------------------
    // Request-modeller
    // --------------------------------------------------------
    public record CreateCardRequest
    {
        public Guid AccountId { get; init; }
        public string CardHolderName { get; init; } = string.Empty;
    }

    public record BlockCardRequest
    {
        public string Reason { get; init; } = string.Empty;
    }
}