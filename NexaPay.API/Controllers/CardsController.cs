// ============================================================
// CardsController.cs – NexaPay.API/Controllers
// ============================================================
// Rollbehörigheter:
//   GET  /cards/account/{id}    → Alla inloggade
//   POST /cards                 → Admin, BankManager, Teller, User
//   PUT  /cards/{id}/activate   → Admin, BankManager, Teller, User
//   PUT  /cards/{id}/block      → Admin, BankManager
// ============================================================

using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NexaPay.API.Contracts;
using NexaPay.API.Extensions;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Models;
using NexaPay.Application.Features.Cards.Commands.ActivateCard;
using NexaPay.Application.Features.Cards.Commands.BlockCard;
using NexaPay.Application.DTOs;
using NexaPay.Application.Features.Cards.Commands.CreateCard;
using NexaPay.Application.Features.Cards.Queries.GetCardsByAccount;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("financial")]
    [Produces("application/json")]
    public class CardsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CardsController(IMediator mediator)
        {
            _mediator = mediator;
        }


        // --------------------------------------------------------
        // GET api/cards/account/{accountId}
        // --------------------------------------------------------
        [HttpGet("account/{accountId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<CardDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetByAccount(Guid accountId)
        {
            var result = await _mediator.Send(
                new GetCardsByAccountQuery
                {
                    AccountId = accountId,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // POST api/cards
        // --------------------------------------------------------
        // Auditor kan INTE skapa kort – read-only roll
        [HttpPost]
        [Authorize(Roles = Roles.CanWrite)]
        [ProducesResponseType(typeof(ApiResponse<CreateCardResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create(
            [FromBody] CreateCardRequest request)
        {
            var result = await _mediator.Send(
                new CreateCardCommand
                {
                    AccountId = request.AccountId,
                    CardHolderName = request.CardHolderName,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Kort skapades framgångsrikt"));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // PUT api/cards/{id}/activate
        // --------------------------------------------------------
        // Kortets ägare, Admin, BankManager och Teller kan aktivera kort
        [HttpPut("{id:guid}/activate")]
        [Authorize(Roles = Roles.CanWrite)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Activate(Guid id)
        {
            var result = await _mediator.Send(
                new ActivateCardCommand
                {
                    CardId = id,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    message: "Kort aktiverades framgångsrikt"));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // PUT api/cards/{id}/block
        // --------------------------------------------------------
        // Bara Admin och BankManager kan blockera kort
        [HttpPut("{id:guid}/block")]
        [Authorize(Roles = Roles.CanBlockCard)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Block(
            Guid id,
            [FromBody] BlockCardRequest request)
        {
            var result = await _mediator.Send(
                new BlockCardCommand
                {
                    CardId = id,
                    Reason = request.Reason,
                    AdminId = User.GetUserId()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    message: "Kort blockerades framgångsrikt"));

            return this.ToErrorResponse(result);
        }
    }

}