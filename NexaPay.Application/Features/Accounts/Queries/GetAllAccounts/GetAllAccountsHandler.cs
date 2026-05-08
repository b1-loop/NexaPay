// ============================================================
// GetAllAccountsHandler.cs
// NexaPay.Application/Features/Accounts/Queries/GetAllAccounts
// ============================================================
// Hanterar GetAllAccountsQuery och returnerar konton
// baserat på användarens roll (RBAC).
//
// RBAC = Role-Based Access Control:
//   Admin → ser alla konton i systemet
//   User  → ser bara sina egna konton
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Queries.GetAllAccounts
{
    public class GetAllAccountsHandler
        : IRequestHandler<GetAllAccountsQuery, Result<IEnumerable<AccountDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAllAccountsHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<IEnumerable<AccountDto>>> Handle(
            GetAllAccountsQuery request,
            CancellationToken cancellationToken)
        {
            IEnumerable<Domain.Entities.Account> accounts;

            if (request.IsStaff)
                accounts = await _unitOfWork.Accounts.GetAllAccountsIncludingClosedAsync(cancellationToken);
            else
                accounts = await _unitOfWork.Accounts
                    .GetAccountsByOwnerIdAsync(request.UserId, cancellationToken);

            var accountDtos = _mapper.Map<IEnumerable<AccountDto>>(accounts);
            return Result<IEnumerable<AccountDto>>.Success(accountDtos);
        }
    }
}