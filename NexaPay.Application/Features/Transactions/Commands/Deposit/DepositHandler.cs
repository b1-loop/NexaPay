// ============================================================
// DepositHandler.cs
// NexaPay.Application/Features/Transactions/Commands/Deposit
// ============================================================
// Sätter in pengar via Account.Deposit(). Före själva insättningen:
//   1. Vi laddar kontot och verifierar ägarskap (eller staff-roll).
//   2. Vi kollar Idempotency-Key – om samma key redan finns
//      returnerar vi den existerande transaktionen istället
//      för att skapa en dubblett.
//
// Domän-eventet MoneyDeposited publiceras automatiskt efter
// SaveChanges (via UnitOfWork) och triggar notifikation till ägaren.
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Exceptions;
using NexaPay.Domain.Interfaces;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Application.Features.Transactions.Commands.Deposit
{
    public class DepositHandler
        : IRequestHandler<DepositCommand, Result<TransactionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public DepositHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<TransactionDto>> Handle(
            DepositCommand request,
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
                var transaction = account.Deposit(amount, request.Description, request.IdempotencyKey);

                await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (IdempotencyConflictException) when (request.IdempotencyKey.HasValue)
                {
                    // Race: en parallell request hann lägga in samma (key, account)
                    // och tog det unika indexet. Slå upp vinnaren och returnera den
                    // istället för att bubbla upp 500.
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
