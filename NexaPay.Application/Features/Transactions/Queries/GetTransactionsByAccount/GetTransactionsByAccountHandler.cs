// ============================================================
// GetTransactionsByAccountHandler.cs
// NexaPay.Application/Features/Transactions/Queries/
// GetTransactionsByAccount
// ============================================================
// Uppdaterad med paginering.
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

            var account = await _unitOfWork.Accounts
                .GetByIdAsync(request.AccountId, cancellationToken);

            if (account == null)
                return Result<PagedResult<TransactionDto>>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

            var isOwner = account.OwnerId == request.UserId;

            if (!request.IsStaff && !isOwner)
                return Result<PagedResult<TransactionDto>>.NotFound(
                    $"Konto med ID {request.AccountId} hittades inte");

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