// ============================================================
// TransactionsController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar alla transaktions-relaterade HTTP-endpoints.
//
// Rollbehörigheter:
//   GET  /transactions/account/{id} → Alla inloggade
//   POST /transactions/deposit      → Admin, BankManager, Teller, User
//   POST /transactions/withdraw     → Admin, BankManager, Teller, User
//   POST /transactions/transfer     → Admin, BankManager, User
//
// Paginering på GET /transactions/account/{id}:
//   ?page=1&pageSize=20 (standard)
//   ?page=2&pageSize=10 (sida 2 med 10 per sida)
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
using NexaPay.Application.DTOs;
using NexaPay.Application.Features.Transactions.Commands.Deposit;
using NexaPay.Application.Features.Transactions.Commands.Transfer;
using NexaPay.Application.Features.Transactions.Commands.Withdraw;
using NexaPay.Application.Features.Transactions.Queries.GetTransactionsByAccount;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("financial")]
    [Produces("application/json")]
    public class TransactionsController : ControllerBase
    {
        // IMediator skickar Commands och Queries till rätt Handler
        private readonly IMediator _mediator;

        public TransactionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // Parses the Idempotency-Key request header. Returns null if absent or not a valid GUID.
        private Guid? GetIdempotencyKey()
        {
            Request.Headers.TryGetValue("Idempotency-Key", out var value);
            return Guid.TryParse(value, out var parsed) ? parsed : null;
        }

        // --------------------------------------------------------
        // GET api/transactions/account/{accountId}
        // --------------------------------------------------------
        // Stödjer paginering via query-parametrar:
        //   ?page=1&pageSize=20  ← standard
        //   ?page=2&pageSize=10  ← sida 2 med 10 per sida
        //
        // Svaret innehåller:
        //   items         = transaktionerna för denna sida
        //   totalCount    = totalt antal transaktioner
        //   page          = aktuell sida
        //   pageSize      = antal per sida
        //   totalPages    = totalt antal sidor
        //   hasNextPage   = om det finns fler sidor
        //   hasPreviousPage = om det finns föregående sida
        [HttpGet("account/{accountId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TransactionDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetByAccount(
            Guid accountId,
            // [FromQuery] läser parametrar från URL:en
            // Default = 1 om inget anges
            [FromQuery] int page = 1,
            // Default = 20 om inget anges
            [FromQuery] int pageSize = 20)
        {
            var result = await _mediator.Send(
                new GetTransactionsByAccountQuery
                {
                    AccountId = accountId,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff(),
                    // Skicka med pagineringsparametrar från URL
                    Page = page,
                    PageSize = pageSize
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // POST api/transactions/deposit
        // --------------------------------------------------------
        // Auditor kan INTE göra insättningar – read-only roll
        [HttpPost("deposit")]
        [Authorize(Roles = Roles.CanWrite)]
        [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Deposit(
            [FromBody] DepositRequest request)
        {
            var result = await _mediator.Send(
                new DepositCommand
                {
                    AccountId = request.AccountId,
                    Amount = request.Amount,
                    Description = request.Description,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff(),
                    IdempotencyKey = GetIdempotencyKey()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Insättning genomfördes framgångsrikt"));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // POST api/transactions/withdraw
        // --------------------------------------------------------
        // Auditor kan INTE göra uttag – read-only roll
        [HttpPost("withdraw")]
        [Authorize(Roles = Roles.CanWrite)]
        [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Withdraw(
            [FromBody] WithdrawRequest request)
        {
            var result = await _mediator.Send(
                new WithdrawCommand
                {
                    AccountId = request.AccountId,
                    Amount = request.Amount,
                    Description = request.Description,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff(),
                    IdempotencyKey = GetIdempotencyKey()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Uttag genomfördes framgångsrikt"));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // POST api/transactions/transfer
        // --------------------------------------------------------
        // Bara Admin, BankManager och User kan överföra
        // Teller och Auditor kan INTE göra överföringar
        [HttpPost("transfer")]
        [Authorize(Roles = Roles.CanTransfer)]
        [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Transfer(
            [FromBody] TransferRequest request)
        {
            var result = await _mediator.Send(
                new TransferCommand
                {
                    FromAccountId = request.FromAccountId,
                    ToAccountId = request.ToAccountId,
                    Amount = request.Amount,
                    Description = request.Description,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff(),
                    IdempotencyKey = GetIdempotencyKey()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Överföring genomfördes framgångsrikt"));

            return this.ToErrorResponse(result);
        }
    }

}