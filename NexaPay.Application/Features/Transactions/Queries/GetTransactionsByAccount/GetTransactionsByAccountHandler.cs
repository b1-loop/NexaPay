// ============================================================
// GetTransactionsByAccountHandler.cs
// NexaPay.Application/Features/Transactions/Queries/GetTransactionsByAccount
// ============================================================
// Returnerar transaktionshistorik för ett konto som paginerat
// resultat. Använder lättviktiga "exists/ownedBy"-checkar
// istället för att ladda hela Account-aggregatet – vi behöver
// bara verifiera behörighet, inte mutera state.
// Page/PageSize klampas till säkra gränser (1..100) för att
// undvika DoS via gigantiska sidor.
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Transactions.Queries
    .GetTransactionsByAccount
{
    public class GetTransactionsByAccountHandler
        : IRequestHandler<GetTransactionsByAccountQuery,
            Result<PagedResult<TransactionDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetTransactionsByAccountHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<PagedResult<TransactionDto>>> Handle(
            GetTransactionsByAccountQuery request,
            CancellationToken cancellationToken)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            // Lightweight ownership check — avoids loading the full Account entity.
            if (request.IsStaff)
            {
                var exists = await _unitOfWork.Accounts
                    .AccountExistsAsync(request.AccountId, cancellationToken);
                if (!exists)
                    return Result<PagedResult<TransactionDto>>.NotFound(
                        $"Konto med ID {request.AccountId} hittades inte");
            }
            else
            {
                var owned = await _unitOfWork.Accounts
                    .AccountOwnedByAsync(request.AccountId, request.UserId, cancellationToken);
                if (!owned)
                    return Result<PagedResult<TransactionDto>>.NotFound(
                        $"Konto med ID {request.AccountId} hittades inte");
            }

            var (transactions, totalCount) = await _unitOfWork
                .Transactions
                .GetTransactionsByAccountIdPagedAsync(
                    request.AccountId,
                    page,
                    pageSize,
                    cancellationToken);

            var transactionDtos = _mapper
                .Map<IEnumerable<TransactionDto>>(transactions);

            var pagedResult = PagedResult<TransactionDto>.Create(
                items: transactionDtos,
                totalCount: totalCount,
                page: page,
                pageSize: pageSize);

            return Result<PagedResult<TransactionDto>>.Success(pagedResult);
        }
    }
}
