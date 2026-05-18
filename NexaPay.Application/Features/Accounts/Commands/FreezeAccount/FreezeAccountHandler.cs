// ============================================================
// FreezeAccountHandler.cs
// NexaPay.Application/Features/Accounts/Commands/FreezeAccount
// ============================================================
// Fryser ett konto via Account.Freeze(). NotFound returneras
// både när kontot inte finns OCH när användaren saknar behörighet
// (samma respons skyddar mot att man kan kartlägga andras konto-id).
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Commands.FreezeAccount
{
    public class FreezeAccountHandler : IRequestHandler<FreezeAccountCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public FreezeAccountHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(FreezeAccountCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var account = await _unitOfWork.Accounts.GetByIdAsync(request.AccountId, cancellationToken);

                if (account is null)
                    return Result.NotFound($"Konto med ID {request.AccountId} hittades inte");

                if (!request.IsStaff && account.OwnerId != request.UserId)
                    return Result.NotFound($"Konto med ID {request.AccountId} hittades inte");

                account.Freeze();
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
