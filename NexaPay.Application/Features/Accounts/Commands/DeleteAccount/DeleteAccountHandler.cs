// ============================================================
// DeleteAccountHandler.cs
// NexaPay.Application/Features/Accounts/Commands/DeleteAccount
// ============================================================
// Hanterar borttagning av ett bankkonto.
//
// Affärsregler:
//   1. Kontot måste finnas
//   2. Användaren måste vara ägare eller Admin
//   3. Kontot måste ha saldo = 0 (kan inte stänga konto med pengar)
//   4. Markera kontot som inaktivt (soft delete)
//      Vi tar inte bort kontot fysiskt – av revisionsskäl
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Commands.DeleteAccount
{
    public class DeleteAccountHandler
        : IRequestHandler<DeleteAccountCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteAccountHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(
            DeleteAccountCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Steg 1: Hämta kontot
                var account = await _unitOfWork.Accounts
                    .GetByIdAsync(request.AccountId);

                if (account == null)
                    return Result.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Steg 2: Kontrollera behörighet
                var isOwner = account.OwnerId == request.UserId;

                if (!request.IsAdmin && !isOwner)
                    return Result.Failure(
                        $"Konto med ID {request.AccountId} hittades inte");

                // Steg 3: Affärsregel – kontot måste vara tomt
                // Man kan inte stänga ett konto med pengar på
                // Kunden måste först ta ut eller överföra saldot
                if (account.Balance > 0)
                    return Result.Failure(
                        $"Kontot kan inte stängas eftersom det har ett saldo på {account.Balance} kr. " +
                        "Töm kontot innan du stänger det.");

                // Steg 4: Soft delete – markera som inaktivt
                // Vi tar ALDRIG bort ett bankkonto fysiskt från databasen
                // Det måste finnas kvar för revision och historik
                account.IsActive = false;
                account.UpdatedAt = DateTime.UtcNow;

                // Uppdatera och spara
                _unitOfWork.Accounts.Update(account);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(
                    $"Ett fel uppstod när kontot skulle stängas: {ex.Message}");
            }
        }
    }
}