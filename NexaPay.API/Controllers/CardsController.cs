// ============================================================
// CardsController.cs – NexaPay.API/Controllers
// ============================================================
// Rollbehörigheter:
//   GET  /cards/account/{id} → Alla inloggade
//   POST /cards              → Admin, BankManager, Teller, User
//   PUT  /cards/{id}/block   → Admin, BankManager
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Common.Constants;
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

        private bool IsStaff() =>
            User.IsInRole(Roles.Admin) ||
            User.IsInRole(Roles.BankManager) ||
            User.IsInRole(Roles.Teller) ||
            User.IsInRole(Roles.Auditor);

        // --------------------------------------------------------
        // GET api/cards/account/{accountId}
        // --------------------------------------------------------
        [HttpGet("account/{accountId:guid}")]
        public async Task<IActionResult> GetByAccount(Guid accountId)
        {
            var result = await _mediator.Send(
                new GetCardsByAccountQuery
                {
                    AccountId = accountId,
                    UserId = GetUserId(),
                    IsAdmin = IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return NotFound(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/cards
        // --------------------------------------------------------
        // Auditor kan INTE skapa kort – read-only roll
        [HttpPost]
        [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager},{Roles.Teller},{Roles.User}")]
        public async Task<IActionResult> Create(
            [FromBody] CreateCardRequest request)
        {
            var result = await _mediator.Send(
                new CreateCardCommand
                {
                    AccountId = request.AccountId,
                    CardHolderName = request.CardHolderName,
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
        // Bara Admin och BankManager kan blockera kort
        [HttpPut("{id:guid}/block")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager}")]
        public async Task<IActionResult> Block(
            Guid id,
            [FromBody] BlockCardRequest request)
        {
            var result = await _mediator.Send(
                new BlockCardCommand
                {
                    CardId = id,
                    Reason = request.Reason,
                    AdminId = GetUserId()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    message: "Kort blockerades framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }
    }

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