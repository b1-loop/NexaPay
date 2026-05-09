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

using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NexaPay.API.Contracts;
using NexaPay.API.Extensions;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Models;
using NexaPay.Application.Features.Accounts.Commands.CreateAccount;
using NexaPay.Application.Features.Accounts.Commands.DeleteAccount;
using NexaPay.Application.Features.Accounts.Queries.GetAccountById;
using NexaPay.Application.Features.Accounts.Queries.GetAllAccounts;
using NexaPay.Application.DTOs;

namespace NexaPay.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("financial")]
    [Produces("application/json")]
    public class AccountsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AccountsController(IMediator mediator)
        {
            _mediator = mediator;
        }


        // --------------------------------------------------------
        // GET api/accounts
        // --------------------------------------------------------
        // Personal ser alla konton
        // Vanlig User ser bara sina egna
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<AccountDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _mediator.Send(
                new GetAllAccountsQuery
                {
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // GET api/accounts/{id}
        // --------------------------------------------------------
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _mediator.Send(
                new GetAccountByIdQuery
                {
                    AccountId = id,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(result.Value));

            return this.ToErrorResponse(result);
        }

        // --------------------------------------------------------
        // POST api/accounts
        // --------------------------------------------------------
        // Alla inloggade kan skapa konton
        // Auditor kan INTE skapa konton (read-only roll)
        [HttpPost]
        [Authorize(Roles = Roles.CanWrite)]
        [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create(
            [FromBody] CreateAccountRequest request)
        {
            var result = await _mediator.Send(
                new CreateAccountCommand
                {
                    AccountName = request.AccountName,
                    AccountType = request.AccountType,
                    OwnerId = User.GetUserId()
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
        [Authorize(Roles = Roles.CanDelete)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _mediator.Send(
                new DeleteAccountCommand
                {
                    AccountId = id,
                    UserId = User.GetUserId(),
                    IsStaff = User.IsStaff()
                });

            if (result.IsSuccess)
                return Ok(ApiResponse.Ok(
                    message: "Konto stängdes framgångsrikt"));

            return this.ToErrorResponse(result);
        }
    }

}