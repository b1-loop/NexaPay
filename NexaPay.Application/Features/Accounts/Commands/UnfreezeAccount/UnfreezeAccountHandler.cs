using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Commands.UnfreezeAccount
{
    public class UnfreezeAccountHandler : IRequestHandler<UnfreezeAccountCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UnfreezeAccountHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(UnfreezeAccountCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var account = await _unitOfWork.Accounts.GetByIdAsync(request.AccountId, cancellationToken);

                if (account is null)
                    return Result.NotFound($"Konto med ID {request.AccountId} hittades inte");

                if (!request.IsStaff && account.OwnerId != request.UserId)
                    return Result.NotFound($"Konto med ID {request.AccountId} hittades inte");

                account.Unfreeze();
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
