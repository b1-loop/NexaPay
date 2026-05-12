using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Queries.LookupAccountByNumber
{
    public class LookupAccountByNumberHandler
        : IRequestHandler<LookupAccountByNumberQuery, Result<AccountLookupDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public LookupAccountByNumberHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<AccountLookupDto>> Handle(
            LookupAccountByNumberQuery request,
            CancellationToken cancellationToken)
        {
            var account = await _unitOfWork.Accounts
                .GetByAccountNumberAsync(request.AccountNumber, cancellationToken);

            if (account == null)
                return Result<AccountLookupDto>.NotFound(
                    "Inget konto hittades med det kontonumret.");

            return Result<AccountLookupDto>.Success(new AccountLookupDto
            {
                Id            = account.Id,
                AccountName   = account.AccountName,
                AccountNumber = account.AccountNumber
            });
        }
    }
}
