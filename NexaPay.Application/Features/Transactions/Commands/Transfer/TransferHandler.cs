// ============================================================
// TransferHandler.cs
// NexaPay.Application/Features/Transactions/Commands/Transfer
// ============================================================
// Den mest komplexa handlern i NexaPay.
// Hanterar överföring av pengar mellan två konton.
//
// Atomärt flöde via Unit of Work:
//   1. Hämta och validera båda kontona
//   2. Kontrollera saldo (overdraft-skydd)
//   3. Dra av från avsändarkonto
//   4. Lägg till på mottagarkonto
//   5. Skapa transaktionspost för avsändaren
//   6. Skapa transaktionspost för mottagaren
//   7. Spara ALLT i en enda databastransaktion
//
// Om steg 7 misslyckas → ingenting sparas → inga pengar försvinner
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Transactions.Commands.Transfer
{
    public class TransferHandler
        : IRequestHandler<TransferCommand, Result<TransactionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public TransferHandler(IUnitOfWork unitOfWork, IMapper mapper)
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
                // --------------------------------------------------------
                // Steg 1: Hämta och validera avsändarkontot
                // --------------------------------------------------------
                var fromAccount = await _unitOfWork.Accounts
                    .GetByIdAsync(request.FromAccountId);

                if (fromAccount == null)
                    return Result<TransactionDto>.Failure(
                        $"Avsändarkonto med ID {request.FromAccountId} hittades inte");

                // Måste äga avsändarkontot
                if (fromAccount.OwnerId != request.UserId)
                    return Result<TransactionDto>.Failure(
                        $"Avsändarkonto med ID {request.FromAccountId} hittades inte");

                if (!fromAccount.IsActive)
                    return Result<TransactionDto>.Failure(
                        "Avsändarkontot är inaktivt");

                // --------------------------------------------------------
                // Steg 2: Hämta och validera mottagarkontot
                // --------------------------------------------------------
                var toAccount = await _unitOfWork.Accounts
                    .GetByIdAsync(request.ToAccountId);

                if (toAccount == null)
                    return Result<TransactionDto>.Failure(
                        $"Mottagarkonto med ID {request.ToAccountId} hittades inte");

                if (!toAccount.IsActive)
                    return Result<TransactionDto>.Failure(
                        "Mottagarkontot är inaktivt");

                // --------------------------------------------------------
                // Steg 3: Overdraft-skydd
                // --------------------------------------------------------
                if (fromAccount.Balance < request.Amount)
                    return Result<TransactionDto>.Failure(
                        $"Otillräckligt saldo. " +
                        $"Tillgängligt: {fromAccount.Balance:C}, " +
                        $"Begärt: {request.Amount:C}");

                // --------------------------------------------------------
                // Steg 4: Uppdatera båda kontona
                // --------------------------------------------------------
                // Dra av från avsändaren
                fromAccount.Balance -= request.Amount;
                fromAccount.UpdatedAt = DateTime.UtcNow;

                // Lägg till hos mottagaren
                toAccount.Balance += request.Amount;
                toAccount.UpdatedAt = DateTime.UtcNow;

                // --------------------------------------------------------
                // Steg 5: Skapa transaktionsposter för BÅDA kontona
                // --------------------------------------------------------
                // Transaktionspost för avsändaren (uttag)
                var fromTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    Amount = request.Amount,
                    Type = TransactionType.Transfer,
                    Description = $"Överföring till konto: {request.Description}",
                    BalanceAfterTransaction = fromAccount.Balance,
                    // Peka ut mottagarkontot – viktigt för historiken
                    ReceiverAccountId = request.ToAccountId,
                    AccountId = request.FromAccountId,
                    CreatedAt = DateTime.UtcNow
                };

                // Transaktionspost för mottagaren (insättning)
                var toTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    Amount = request.Amount,
                    Type = TransactionType.Transfer,
                    Description = $"Överföring från konto: {request.Description}",
                    BalanceAfterTransaction = toAccount.Balance,
                    ReceiverAccountId = null,
                    AccountId = request.ToAccountId,
                    CreatedAt = DateTime.UtcNow
                };

                // --------------------------------------------------------
                // Steg 6: Spara ALLT atomärt – Unit of Work i aktion!
                // --------------------------------------------------------
                // Uppdatera båda kontona
                _unitOfWork.Accounts.Update(fromAccount);
                _unitOfWork.Accounts.Update(toAccount);

                // Lägg till båda transaktionsposterna
                await _unitOfWork.Transactions.AddAsync(fromTransaction);
                await _unitOfWork.Transactions.AddAsync(toTransaction);

                // EN enda SaveChangesAsync sparar ALLT i en databastransaktion
                // 4 operationer → 1 transaktion → atomärt garanterat
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Returnera avsändarens transaktionspost
                var transactionDto = _mapper.Map<TransactionDto>(fromTransaction);
                return Result<TransactionDto>.Success(transactionDto);
            }
            catch (Exception ex)
            {
                return Result<TransactionDto>.Failure(
                    $"Ett fel uppstod vid överföringen: {ex.Message}");
            }
        }
    }
}