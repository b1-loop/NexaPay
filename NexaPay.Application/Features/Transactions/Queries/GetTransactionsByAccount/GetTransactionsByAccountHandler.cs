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
            try
            {
                // ------------------------------------------------
                // Steg 1: Validera pagineringsparametrar
                // ------------------------------------------------
                // Page måste vara minst 1
                var page = Math.Max(1, request.Page);

                // PageSize måste vara mellan 1 och 100
                // Vi begränsar till max 100 för att förhindra
                // att någon hämtar för mycket data på en gång
                var pageSize = Math.Clamp(request.PageSize, 1, 100);

                // ------------------------------------------------
                // Steg 2: Kontrollera konto och behörighet
                // ------------------------------------------------
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                if (account == null)
                    return Result<PagedResult<TransactionDto>>.Failure(
                        $"Konto med ID {request.AccountId} " +
                        $"hittades inte");

                var isOwner = account.OwnerId == request.UserId;

                if (!request.IsAdmin && !isOwner)
                    return Result<PagedResult<TransactionDto>>.Failure(
                        $"Konto med ID {request.AccountId} " +
                        $"hittades inte");

                // ------------------------------------------------
                // Steg 3: Hämta paginerade transaktioner
                // ------------------------------------------------
                // GetTransactionsByAccountIdPagedAsync returnerar
                // en tuple med transaktioner och totalt antal
                var (transactions, totalCount) = await _unitOfWork
                    .Transactions
                    .GetTransactionsByAccountIdPagedAsync(
                        request.AccountId,
                        page,
                        pageSize);

                // ------------------------------------------------
                // Steg 4: Mappa och returnera
                // ------------------------------------------------
                var transactionDtos = _mapper
                    .Map<IEnumerable<TransactionDto>>(transactions);

                // Skapa PagedResult med all pagineringsinformation
                var pagedResult = PagedResult<TransactionDto>.Create(
                    items: transactionDtos,
                    totalCount: totalCount,
                    page: page,
                    pageSize: pageSize);

                return Result<PagedResult<TransactionDto>>
                    .Success(pagedResult);
            }
            catch (Exception ex)
            {
                return Result<PagedResult<TransactionDto>>.Failure(
                    $"Ett fel uppstod: {ex.Message}");
            }
        }
    }
}