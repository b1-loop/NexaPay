// ============================================================
// TransferHandler.cs
// NexaPay.Application/Features/Transactions/Commands/Transfer
// ============================================================
// Genomför en kontoöverföring via Account.TransferTo(). Skapar
// TVÅ transaktionsrader (en på varje konto) som sparas atomärt
// i samma SaveChanges → antingen sker hela överföringen eller
// rullas allt tillbaka. Pengar kan därför aldrig "tappa bort sig".
//
// Olika valutor avvisas direkt – vi gör ingen valutaväxling.
// Idempotency-Key kortsluter dubbla POSTs.
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Exceptions;
using NexaPay.Domain.Interfaces;
using NexaPay.Domain.ValueObjects;

namespace NexaPay.Application.Features.Transactions.Commands.Transfer
{
    public class TransferHandler
        : IRequestHandler<TransferCommand, Result<TransactionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public TransferHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<TransactionDto>> Handle(
            TransferCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var fromAccount = await _unitOfWork.Accounts
                    .GetByIdAsync(request.FromAccountId, cancellationToken);

                if (fromAccount == null)
                    return Result<TransactionDto>.NotFound(
                        $"Avsändarkonto med ID {request.FromAccountId} hittades inte");

                if (!request.IsStaff && fromAccount.OwnerId != request.UserId)
                    return Result<TransactionDto>.NotFound(
                        $"Avsändarkonto med ID {request.FromAccountId} hittades inte");

                var toAccount = await _unitOfWork.Accounts
                    .GetByIdAsync(request.ToAccountId, cancellationToken);

                if (toAccount == null)
                    return Result<TransactionDto>.NotFound(
                        $"Mottagarkonto med ID {request.ToAccountId} hittades inte");

                // Idempotency-nyckeln sitter på fromTransaction (avsändarkontot),
                // så scope:a uppslagningen till FromAccountId.
                if (request.IdempotencyKey.HasValue)
                {
                    var existing = await _unitOfWork.Transactions
                        .GetByIdempotencyKeyAsync(request.IdempotencyKey.Value, request.FromAccountId, cancellationToken);
                    if (existing != null)
                        return Result<TransactionDto>.Success(_mapper.Map<TransactionDto>(existing));
                }

                if (fromAccount.Balance.Currency != toAccount.Balance.Currency)
                    return Result<TransactionDto>.Failure(
                        $"Kontona har olika valutor ({fromAccount.Balance.Currency} och " +
                        $"{toAccount.Balance.Currency}). Överföring kräver att båda konton har samma valuta.");

                var amount = new Money(request.Amount, fromAccount.Balance.Currency);

                var (fromTransaction, toTransaction) = fromAccount.TransferTo(
                    amount,
                    request.Description,
                    toAccount,
                    request.IdempotencyKey);

                await _unitOfWork.Transactions.AddAsync(fromTransaction, cancellationToken);
                await _unitOfWork.Transactions.AddAsync(toTransaction, cancellationToken);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (IdempotencyConflictException) when (request.IdempotencyKey.HasValue)
                {
                    var winner = await _unitOfWork.Transactions
                        .GetByIdempotencyKeyAsync(request.IdempotencyKey.Value, request.FromAccountId, cancellationToken);
                    if (winner != null)
                        return Result<TransactionDto>.Success(_mapper.Map<TransactionDto>(winner));
                    throw;
                }

                return Result<TransactionDto>.Success(
                    _mapper.Map<TransactionDto>(fromTransaction));
            }
            catch (InvalidOperationException ex)
            {
                return Result<TransactionDto>.Failure(ex.Message);
            }
        }
    }
}
