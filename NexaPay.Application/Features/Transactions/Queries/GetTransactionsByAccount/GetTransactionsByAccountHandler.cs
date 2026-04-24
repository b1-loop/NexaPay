// ============================================================
// GetTransactionsByAccountHandler.cs
// NexaPay.Application/Features/Transactions/Queries/
// GetTransactionsByAccount
// ============================================================
// Hämtar transaktionshistoriken för ett konto (kontoutdrag).
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Transactions.Queries.GetTransactionsByAccount
{
    public class GetTransactionsByAccountHandler
        : IRequestHandler<GetTransactionsByAccountQuery,
            Result<IEnumerable<TransactionDto>>>
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

        public async Task<Result<IEnumerable<TransactionDto>>> Handle(
            GetTransactionsByAccountQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Steg 1: Kontrollera att kontot finns och behörighet
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                if (account == null)
                    return Result<IEnumerable<TransactionDto>>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                var isOwner = account.OwnerId == request.UserId;

                if (!request.IsAdmin && !isOwner)
                    return Result<IEnumerable<TransactionDto>>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Steg 2: Hämta transaktionerna
                // Sorterade med senaste transaktion först
                // (standard för kontoutdrag)
                var transactions = await _unitOfWork.Transactions
                    .GetTransactionsByAccountIdAsync(request.AccountId);

                // Steg 3: Mappa och returnera
                var transactionDtos = _mapper
                    .Map<IEnumerable<TransactionDto>>(transactions);

                return Result<IEnumerable<TransactionDto>>
                    .Success(transactionDtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<TransactionDto>>.Failure(
                    $"Ett fel uppstod: {ex.Message}");
            }
        }
    }
}