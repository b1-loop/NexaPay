// ============================================================
// TransactionsController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar alla transaktions-relaterade endpoints.
//
// Endpoints:
//   GET  api/transactions/account/{accountId} ← Historik
//   POST api/transactions/deposit             ← Insättning
//   POST api/transactions/withdraw            ← Uttag
//   POST api/transactions/transfer            ← Överföring
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Features.Transactions.Commands.Deposit;
using NexaPay.Application.Features.Transactions.Commands.Transfer;
using NexaPay.Application.Features.Transactions.Commands.Withdraw;
using NexaPay.Application.Features.Transactions.Queries.GetTransactionsByAccount;
using System.Security.Claims;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TransactionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        private bool IsAdmin() => User.IsInRole("Admin");

        // --------------------------------------------------------
        // GET api/transactions/account/{accountId}
        // --------------------------------------------------------
        [HttpGet("account/{accountId:guid}")]
        public async Task<IActionResult> GetByAccount(Guid accountId)
        {
            var query = new GetTransactionsByAccountQuery
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
        // POST api/transactions/deposit
        // --------------------------------------------------------
        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit(
            [FromBody] DepositRequest request)
        {
            var command = new DepositCommand
            {
                AccountId = request.AccountId,
                Amount = request.Amount,
                Description = request.Description,
                UserId = GetUserId()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result.Value);

            return BadRequest(new { message = result.Error });
        }

        // --------------------------------------------------------
        // POST api/transactions/withdraw
        // --------------------------------------------------------
        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw(
            [FromBody] WithdrawRequest request)
        {
            var command = new WithdrawCommand
            {
                AccountId = request.AccountId,
                Amount = request.Amount,
                Description = request.Description,
                UserId = GetUserId()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result.Value);

            return BadRequest(new { message = result.Error });
        }

        // --------------------------------------------------------
        // POST api/transactions/transfer
        // --------------------------------------------------------
        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer(
            [FromBody] TransferRequest request)
        {
            var command = new TransferCommand
            {
                FromAccountId = request.FromAccountId,
                ToAccountId = request.ToAccountId,
                Amount = request.Amount,
                Description = request.Description,
                UserId = GetUserId()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result.Value);

            return BadRequest(new { message = result.Error });
        }
    }

    // --------------------------------------------------------
    // Request-modeller
    // --------------------------------------------------------
    public record DepositRequest
    {
        public Guid AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public record WithdrawRequest
    {
        public Guid AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public record TransferRequest
    {
        public Guid FromAccountId { get; init; }
        public Guid ToAccountId { get; init; }
        public decimal Amount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}