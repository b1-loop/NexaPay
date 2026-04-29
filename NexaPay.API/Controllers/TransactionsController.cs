// ============================================================
// TransactionsController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar alla transaktions-relaterade HTTP-endpoints.
//
// Controllern är tunn och delegerar all logik till MediatR.
// All banklogik (saldokontroll, overdraft-skydd osv.)
// finns i Application-lagrets Handlers.
//
// Endpoints:
//   GET  api/transactions/account/{accountId} ← Kontoutdrag
//   POST api/transactions/deposit             ← Sätt in pengar
//   POST api/transactions/withdraw            ← Ta ut pengar
//   POST api/transactions/transfer            ← Överför pengar
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

        // Hämta inloggad användares ID från JWT-token
        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        // Kontrollera om användaren är Admin
        private bool IsAdmin() => User.IsInRole("Admin");

        // --------------------------------------------------------
        // GET api/transactions/account/{accountId}
        // --------------------------------------------------------
        // Hämtar transaktionshistoriken för ett konto (kontoutdrag)
        // Sorterade med senaste transaktion först
        [HttpGet("account/{accountId:guid}")]
        public async Task<IActionResult> GetByAccount(Guid accountId)
        {
            var result = await _mediator.Send(
                new GetTransactionsByAccountQuery
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
        // POST api/transactions/deposit
        // --------------------------------------------------------
        // Sätter in pengar på ett konto
        // Saldot ökar med beloppet och en transaktion skapas
        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit(
            [FromBody] DepositRequest request)
        {
            var result = await _mediator.Send(
                new DepositCommand
                {
                    AccountId = request.AccountId,
                    Amount = request.Amount,
                    Description = request.Description,
                    // UserId från JWT – kontrollerar att kontot tillhör användaren
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
        // Tar ut pengar från ett konto
        // Saldot minskar med beloppet – overdraft-skydd finns i Handler
        [HttpPost("withdraw")]
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
        // Överför pengar mellan två konton atomärt via Unit of Work
        // Båda kontona uppdateras i samma databastransaktion
        [HttpPost("transfer")]
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

    // --------------------------------------------------------
    // Request-modeller
    // --------------------------------------------------------

    // Request-modell för POST /api/transactions/deposit
    public record DepositRequest
    {
        // Vilket konto pengarna ska sättas in på
        public Guid AccountId { get; init; }

        // Beloppet – måste vara > 0 (valideras av DepositValidator)
        public decimal Amount { get; init; }

        // Beskrivning som syns i kontoutdraget
        public string Description { get; init; } = string.Empty;
    }

    // Request-modell för POST /api/transactions/withdraw
    public record WithdrawRequest
    {
        // Vilket konto pengarna ska tas från
        public Guid AccountId { get; init; }

        // Beloppet – måste vara > 0 och <= saldo
        public decimal Amount { get; init; }

        // Beskrivning som syns i kontoutdraget
        public string Description { get; init; } = string.Empty;
    }

    // Request-modell för POST /api/transactions/transfer
    public record TransferRequest
    {
        // Kontot som pengarna dras ifrån
        public Guid FromAccountId { get; init; }

        // Kontot som pengarna sätts in på
        public Guid ToAccountId { get; init; }

        // Beloppet som ska överföras
        public decimal Amount { get; init; }

        // Beskrivning som syns i kontoutdraget för båda kontona
        public string Description { get; init; } = string.Empty;
    }
}