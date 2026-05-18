// ============================================================
// WithdrawHandler.cs
// NexaPay.Application/Features/Transactions/Commands/Withdraw
// ============================================================
// Tar ut pengar via Account.Withdraw(). Behörighet, idempotency
// och felhantering identiskt med DepositHandler. Aggregatet
// kastar InvalidOperationException om saldot är otillräckligt
// – det fångas och returneras som Failure med klar text.
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Exceptions;
using NexaPay.Domain.Interfaces;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Application.Features.Transactions.Commands.Withdraw
{
    public class WithdrawHandler
        : IRequestHandler<WithdrawCommand, Result<TransactionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public WithdrawHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<TransactionDto>> Handle(
            WithdrawCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId, cancellationToken);

                if (account == null)
                    return Result<TransactionDto>.NotFound(
                        $"Konto med ID {request.AccountId} hittades inte");

                if (!request.IsStaff && account.OwnerId != request.UserId)
                    return Result<TransactionDto>.NotFound(
                        $"Konto med ID {request.AccountId} hittades inte");

                if (request.IdempotencyKey.HasValue)
                {
                    var existing = await _unitOfWork.Transactions
                        .GetByIdempotencyKeyAsync(request.IdempotencyKey.Value, request.AccountId, cancellationToken);
                    if (existing != null)
                        return Result<TransactionDto>.Success(_mapper.Map<TransactionDto>(existing));
                }

                var amount = new Money(request.Amount, account.Balance.Currency);
                var transaction = account.Withdraw(amount, request.Description, request.IdempotencyKey);

                await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (IdempotencyConflictException) when (request.IdempotencyKey.HasValue)
                {
                    var winner = await _unitOfWork.Transactions
                        .GetByIdempotencyKeyAsync(request.IdempotencyKey.Value, request.AccountId, cancellationToken);
                    if (winner != null)
                        return Result<TransactionDto>.Success(_mapper.Map<TransactionDto>(winner));
                    throw;
                }

                return Result<TransactionDto>.Success(_mapper.Map<TransactionDto>(transaction));
            }
            catch (InvalidOperationException ex)
            {
                return Result<TransactionDto>.Failure(ex.Message);
            }
        }
    }
}
