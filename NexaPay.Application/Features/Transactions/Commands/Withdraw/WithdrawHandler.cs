// ============================================================
// WithdrawHandler.cs
// NexaPay.Application/Features/Transactions/Commands/Withdraw
// ============================================================
// Hanterar uttag från ett konto.
//
// Kritisk affärsregel: Overdraft-skydd
// Saldot får ALDRIG bli negativt.
// Om användaren försöker ta ut mer än saldot →
// returnera Result.Failure med ett tydligt meddelande.
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Transactions.Commands.Withdraw
{
    public class WithdrawHandler
        : IRequestHandler<WithdrawCommand, Result<TransactionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public WithdrawHandler(IUnitOfWork unitOfWork, IMapper mapper)
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
                // Steg 1: Hämta och validera kontot
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                if (account == null)
                    return Result<TransactionDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                if (account.OwnerId != request.UserId)
                    return Result<TransactionDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                if (!account.IsActive)
                    return Result<TransactionDto>.Failure(
                        "Kan inte ta ut pengar från ett inaktivt konto");

                // --------------------------------------------------------
                // Steg 2: Overdraft-skydd – kritisk affärsregel!
                // --------------------------------------------------------
                // Kontrollera att saldot räcker för uttaget
                // account.Balance < request.Amount = saldot räcker inte
                if (account.Balance < request.Amount)
                    return Result<TransactionDto>.Failure(
                        $"Otillräckligt saldo. " +
                        $"Tillgängligt saldo: {account.Balance:C}, " +
                        $"Begärt belopp: {request.Amount:C}");
                // ":C" formaterar decimal som valuta t.ex. "1 234,56 kr"

                // Steg 3: Dra av beloppet från saldot
                account.Balance -= request.Amount;
                account.UpdatedAt = DateTime.UtcNow;

                // Steg 4: Skapa transaktionspost
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    Amount = request.Amount,
                    Type = TransactionType.Withdrawal,
                    Description = request.Description,
                    BalanceAfterTransaction = account.Balance,
                    ReceiverAccountId = null,
                    AccountId = request.AccountId,
                    CreatedAt = DateTime.UtcNow
                };

                // Steg 5: Spara atomärt
                _unitOfWork.Accounts.Update(account);
                await _unitOfWork.Transactions.AddAsync(transaction);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var transactionDto = _mapper.Map<TransactionDto>(transaction);
                return Result<TransactionDto>.Success(transactionDto);
            }
            catch (Exception ex)
            {
                return Result<TransactionDto>.Failure(
                    $"Ett fel uppstod vid uttaget: {ex.Message}");
            }
        }
    }
}