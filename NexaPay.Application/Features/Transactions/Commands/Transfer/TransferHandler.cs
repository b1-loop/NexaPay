// ============================================================
// TransferHandler.cs
// NexaPay.Application/Features/Transactions/Commands/Transfer
// ============================================================
// Hanterar överföring av pengar mellan två konton.
//
// BUGGFIX: Tidigare uppdaterades kontona innan alla
// affärsregler var verifierade. Det innebar att
// SaveChangesAsync kunde anropas även vid fel.
//
// KORREKT flöde:
//   1. Hämta och VALIDERA alla konton (ingen uppdatering än!)
//   2. Kör ALLA affärsregler (ägare, saldo, aktiv)
//   3. Uppdatera kontona FÖRST när allt är verifierat
//   4. Spara atomärt via Unit of Work
//
// Unit of Work garanterar att ALLA operationer lyckas
// eller att INGEN av dem sparas – atomärt!
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
                // FAS 1: VALIDERING
                // Hämta och validera ALLT innan vi ändrar något!
                // Inga uppdateringar sker i denna fas.
                // --------------------------------------------------------

                // Steg 1: Hämta avsändarkontot
                var fromAccount = await _unitOfWork.Accounts
                    .GetByIdAsync(request.FromAccountId);

                // Kontot måste finnas
                if (fromAccount == null)
                    return Result<TransactionDto>.Failure(
                        $"Avsändarkonto med ID " +
                        $"{request.FromAccountId} hittades inte");

                // Steg 2: Kontrollera ägarskap DIREKT
                // Detta görs tidigt innan någon uppdatering sker
                // En användare kan bara överföra från SINA egna konton
                if (fromAccount.OwnerId != request.UserId)
                    return Result<TransactionDto>.Failure(
                        $"Avsändarkonto med ID " +
                        $"{request.FromAccountId} hittades inte");
                // Vi returnerar samma fel som "inte hittat" av säkerhetsskäl
                // – avslöjar inte att kontot finns men ägs av någon annan

                // Steg 3: Kontrollera att avsändarkontot är aktivt
                if (!fromAccount.IsActive)
                    return Result<TransactionDto>.Failure(
                        "Avsändarkontot är inaktivt");

                // Steg 4: Hämta mottagarkontot
                var toAccount = await _unitOfWork.Accounts
                    .GetByIdAsync(request.ToAccountId);

                if (toAccount == null)
                    return Result<TransactionDto>.Failure(
                        $"Mottagarkonto med ID " +
                        $"{request.ToAccountId} hittades inte");

                // Steg 5: Kontrollera att mottagarkontot är aktivt
                if (!toAccount.IsActive)
                    return Result<TransactionDto>.Failure(
                        "Mottagarkontot är inaktivt");

                // Steg 6: Overdraft-skydd
                // Kontrollera saldot INNAN vi uppdaterar något
                if (fromAccount.Balance < request.Amount)
                    return Result<TransactionDto>.Failure(
                        $"Otillräckligt saldo. " +
                        $"Tillgängligt: {fromAccount.Balance:C}, " +
                        $"Begärt: {request.Amount:C}");

                // --------------------------------------------------------
                // FAS 2: UPPDATERING
                // Alla valideringar har passerat – nu är det säkert
                // att uppdatera kontona och skapa transaktioner.
                // --------------------------------------------------------

                // Steg 7: Uppdatera saldona
                // Dra av från avsändaren
                fromAccount.Balance -= request.Amount;
                fromAccount.UpdatedAt = DateTime.UtcNow;

                // Lägg till hos mottagaren
                toAccount.Balance += request.Amount;
                toAccount.UpdatedAt = DateTime.UtcNow;

                // Steg 8: Skapa transaktionspost för avsändaren
                // Denna post visar uttaget från avsändarens perspektiv
                var fromTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    Amount = request.Amount,
                    Type = TransactionType.Transfer,
                    Description = $"Överföring till konto: " +
                                  $"{request.Description}",
                    // Saldot EFTER överföringen
                    BalanceAfterTransaction = fromAccount.Balance,
                    // Peka ut mottagarkontot för historiken
                    ReceiverAccountId = request.ToAccountId,
                    AccountId = request.FromAccountId,
                    CreatedAt = DateTime.UtcNow
                };

                // Steg 9: Skapa transaktionspost för mottagaren
                // Denna post visar insättningen från mottagarens perspektiv
                var toTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    Amount = request.Amount,
                    Type = TransactionType.Transfer,
                    Description = $"Överföring från konto: " +
                                  $"{request.Description}",
                    BalanceAfterTransaction = toAccount.Balance,
                    ReceiverAccountId = null,
                    AccountId = request.ToAccountId,
                    CreatedAt = DateTime.UtcNow
                };

                // --------------------------------------------------------
                // FAS 3: SPARA ATOMÄRT via Unit of Work
                // --------------------------------------------------------
                // Alla fyra operationer sparas i EN databastransaktion:
                //   1. Uppdatera avsändarkontots saldo
                //   2. Uppdatera mottagarkontots saldo
                //   3. Spara avsändarens transaktionspost
                //   4. Spara mottagarens transaktionspost
                //
                // Om NÅGOT av dessa misslyckas → rullar ALLT tillbaka
                // Ingen förlorar pengar!

                // Markera båda kontona som uppdaterade
                _unitOfWork.Accounts.Update(fromAccount);
                _unitOfWork.Accounts.Update(toAccount);

                // Lägg till båda transaktionsposterna
                await _unitOfWork.Transactions.AddAsync(fromTransaction);
                await _unitOfWork.Transactions.AddAsync(toTransaction);

                // Spara ALLT i en enda databastransaktion
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // --------------------------------------------------------
                // FAS 4: RETURNERA RESULTAT
                // --------------------------------------------------------
                // Returnera avsändarens transaktionspost som bekräftelse
                var transactionDto = _mapper
                    .Map<TransactionDto>(fromTransaction);

                return Result<TransactionDto>.Success(transactionDto);
            }
            catch (Exception ex)
            {
                // Fånga oväntade fel
                // T.ex. databasfel, nätverksavbrott osv.
                return Result<TransactionDto>.Failure(
                    $"Ett fel uppstod vid överföringen: {ex.Message}");
            }
        }
    }
}