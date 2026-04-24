// ============================================================
// DepositHandler.cs
// NexaPay.Application/Features/Transactions/Commands/Deposit
// ============================================================
// Hanterar insättning av pengar på ett konto.
//
// Flöde:
//   1. Hämta och validera kontot
//   2. Uppdatera saldot
//   3. Skapa en Transaction-rad (oföränderligt revisionsspår)
//   4. Spara ALLT atomärt via Unit of Work
//   5. Returnera TransactionDto
//
// Unit of Work garanterar att både saldouppdateringen
// och transaktionen sparas tillsammans – eller inte alls.
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Transactions.Commands.Deposit
{
    public class DepositHandler
        : IRequestHandler<DepositCommand, Result<TransactionDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public DepositHandler(IUnitOfWork unitOfWork, IMapper mapper)
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
                // --------------------------------------------------------
                // Steg 1: Hämta och validera kontot
                // --------------------------------------------------------
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                if (account == null)
                    return Result<TransactionDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Kontot måste tillhöra den inloggade användaren
                if (account.OwnerId != request.UserId)
                    return Result<TransactionDto>.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Kontot måste vara aktivt
                if (!account.IsActive)
                    return Result<TransactionDto>.Failure(
                        "Kan inte sätta in pengar på ett inaktivt konto");

                // --------------------------------------------------------
                // Steg 2: Uppdatera saldot
                // --------------------------------------------------------
                // Lägg till beloppet på kontots nuvarande saldo
                account.Balance += request.Amount;
                account.UpdatedAt = DateTime.UtcNow;

                // --------------------------------------------------------
                // Steg 3: Skapa Transaction-raden
                // --------------------------------------------------------
                // Varje insättning skapar en oföränderlig transaktionspost
                // Denna post kan aldrig ändras eller tas bort
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    Amount = request.Amount,
                    Type = TransactionType.Deposit,
                    Description = request.Description,

                    // Spara saldot EFTER insättningen
                    // Användbart för att visa saldohistorik i kontoutdrag
                    BalanceAfterTransaction = account.Balance,

                    // Ingen mottagare – det är en direkt insättning
                    ReceiverAccountId = null,

                    // Koppla till rätt konto
                    AccountId = request.AccountId,
                    CreatedAt = DateTime.UtcNow
                };

                // --------------------------------------------------------
                // Steg 4: Spara ALLT atomärt via Unit of Work
                // --------------------------------------------------------
                // Vi uppdaterar kontots saldo
                _unitOfWork.Accounts.Update(account);

                // Vi lägger till transaktionsposten
                await _unitOfWork.Transactions.AddAsync(transaction);

                // SaveChangesAsync sparar BÅDA operationerna i en transaktion
                // Om något kraschar här → rullar ALLT tillbaka
                // Kontots saldo ändras inte utan att transaktionen sparas
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // --------------------------------------------------------
                // Steg 5: Returnera TransactionDto
                // --------------------------------------------------------
                var transactionDto = _mapper.Map<TransactionDto>(transaction);
                return Result<TransactionDto>.Success(transactionDto);
            }
            catch (Exception ex)
            {
                return Result<TransactionDto>.Failure(
                    $"Ett fel uppstod vid insättningen: {ex.Message}");
            }
        }
    }
}