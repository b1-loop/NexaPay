// ============================================================
// TransactionsController.cs – NexaPay.API/Controllers
// ============================================================
// Rollbehörigheter:
//   GET  /transactions/account/{id} → Alla inloggade
//   POST /transactions/deposit      → Admin, BankManager, Teller, User
//   POST /transactions/withdraw     → Admin, BankManager, Teller, User
//   POST /transactions/transfer     → Admin, BankManager, User
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Common.Constants;
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

        private bool IsStaff() =>
            User.IsInRole(Roles.Admin) ||
            User.IsInRole(Roles.BankManager) ||
            User.IsInRole(Roles.Teller) ||
            User.IsInRole(Roles.Auditor);

        // --------------------------------------------------------
        // GET api/transactions/account/{accountId}
        // --------------------------------------------------------
        // Alla inloggade kan se transaktioner
        // Personal ser alla – User ser bara sina egna
        [HttpGet("account/{accountId:guid}")]
        public async Task<IActionResult> GetByAccount(Guid accountId)
        {
            var result = await _mediator.Send(
                new GetTransactionsByAccountQuery
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
        // POST api/transactions/deposit
        // --------------------------------------------------------
        // Auditor kan INTE göra insättningar
        [HttpPost("deposit")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager},{Roles.Teller},{Roles.User}")]
        public async Task<IActionResult> Deposit(
            [FromBody] DepositRequest request)
        {
            var result = await _mediator.Send(
                new DepositCommand
                {
                    AccountId = request.AccountId,
                    Amount = request.Amount,
                    Description = request.Description,
                    UserId = GetUserId()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Insättning genomfördes framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/transactions/withdraw
        // --------------------------------------------------------
        // Auditor kan INTE göra uttag
        [HttpPost("withdraw")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager},{Roles.Teller},{Roles.User}")]
        public async Task<IActionResult> Withdraw(
            [FromBody] WithdrawRequest request)
        {
            var result = await _mediator.Send(
                new WithdrawCommand
                {
                    AccountId = request.AccountId,
                    Amount = request.Amount,
                    Description = request.Description,
                    UserId = GetUserId()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Uttag genomfördes framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/transactions/transfer
        // --------------------------------------------------------
        // Bara Admin, BankManager och User kan överföra
        // Teller och Auditor kan INTE göra överföringar
        [HttpPost("transfer")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager},{Roles.User}")]
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
                    UserId = GetUserId()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    result.Value,
                    "Överföring genomfördes framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }
    }

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