// ============================================================
// DeleteAccountHandler.cs
// NexaPay.Application/Features/Accounts/Commands/DeleteAccount
// ============================================================
// "Tar bort" ett konto genom att kalla Account.Close() –
// vi raderar aldrig fysiskt (audit-krav), bara markerar
// kontot som stängt. Returnerar NotFound för konton som
// inte ägs av användaren (säkerhet: avslöjar inte att kontot
// finns för fel ägare). Domän-undantag översätts till Failure.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Commands.DeleteAccount
{
    public class DeleteAccountHandler
        : IRequestHandler<DeleteAccountCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteAccountHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(
            DeleteAccountCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId, cancellationToken);

                if (account == null)
                    return Result.NotFound(
                        $"Konto med ID {request.AccountId} hittades inte");

                var isOwner = account.OwnerId == request.UserId;

                if (!request.IsStaff && !isOwner)
                    return Result.NotFound(
                        $"Konto med ID {request.AccountId} hittades inte");

                account.Close();

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(ex.Message);
            }
        }
    }
}
