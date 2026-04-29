// ============================================================
// AccountsController.cs – NexaPay.API/Controllers
// ============================================================
// Uppdaterad med rollbaserad åtkomstkontroll (RBAC).
//
// Rollbehörigheter:
//   GET    /accounts     → Admin, BankManager, Teller, Auditor, User
//   GET    /accounts/{id}→ Admin, BankManager, Teller, Auditor, User
//   POST   /accounts     → Alla inloggade
//   DELETE /accounts/{id}→ Admin, BankManager, User (ej Teller/Auditor)
// ============================================================

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Application.Common.Constants;
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
    [Authorize]
    public class AccountsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AccountsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        // Kontrollera om användaren är personal (ej vanlig User)
        // Personal kan se alla konton
        private bool IsStaff() =>
            User.IsInRole(Roles.Admin) ||
            User.IsInRole(Roles.BankManager) ||
            User.IsInRole(Roles.Teller) ||
            User.IsInRole(Roles.Auditor);

        private bool IsAdmin() => User.IsInRole(Roles.Admin);

        // --------------------------------------------------------
        // GET api/accounts
        // --------------------------------------------------------
        // Personal ser alla konton
        // Vanlig User ser bara sina egna
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _mediator.Send(
                new GetAllAccountsQuery
                {
                    UserId = GetUserId(),
                    // IsAdmin = true gör att handleren returnerar alla konton
                    // Vi använder IsStaff() för att inkludera all personal
                    IsAdmin = IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return BadRequest(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // GET api/accounts/{id}
        // --------------------------------------------------------
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _mediator.Send(
                new GetAccountByIdQuery
                {
                    AccountId = id,
                    UserId = GetUserId(),
                    IsAdmin = IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return NotFound(ApiResponse.Fail(result.Error));
        }

        // --------------------------------------------------------
        // POST api/accounts
        // --------------------------------------------------------
        // Alla inloggade kan skapa konton
        // Auditor kan INTE skapa konton (read-only roll)
        [HttpPost]
        [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager},{Roles.Teller},{Roles.User}")]
        public async Task<IActionResult> Create(
            [FromBody] CreateAccountRequest request)
        {
            var result = await _mediator.Send(
                new CreateAccountCommand
                {
                    AccountName = request.AccountName,
                    AccountType = request.AccountType,
                    OwnerId = GetUserId()
                });

            if (result.IsSuccess)
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
        // Admin, BankManager och User kan stänga konton
        // Teller och Auditor kan INTE stänga konton
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = $"{Roles.Admin},{Roles.BankManager},{Roles.User}")]
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
                return Ok(ApiResponse.Ok(
                    message: "Konto stängdes framgångsrikt"));

            return BadRequest(ApiResponse.Fail(result.Error));
        }
    }

    public record CreateAccountRequest
    {
        public string AccountName { get; init; } = string.Empty;
        public AccountType AccountType { get; init; }
    }
}