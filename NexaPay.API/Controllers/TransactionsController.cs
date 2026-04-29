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
        // IMediator skickar Commands och Queries till rätt Handler
        private readonly IMediator _mediator;

        public TransactionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // Hämta inloggad användares ID från JWT-token
        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        // Kontrollera om användaren är personal
        // Personal kan se alla konton och transaktioner
        private bool IsStaff() =>
            User.IsInRole(Roles.Admin) ||
            User.IsInRole(Roles.BankManager) ||
            User.IsInRole(Roles.Teller) ||
            User.IsInRole(Roles.Auditor);

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
                    UserId = GetUserId(),
                    IsAdmin = IsStaff(),
                    // Skicka med pagineringsparametrar från URL
                    Page = page,
                    PageSize = pageSize
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return NotFound(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/transactions/deposit
        // --------------------------------------------------------
        // Auditor kan INTE göra insättningar – read-only roll
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
                    // UserId från JWT – kontrollerar att kontot
                    // tillhör den inloggade användaren
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
        // Auditor kan INTE göra uttag – read-only roll
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