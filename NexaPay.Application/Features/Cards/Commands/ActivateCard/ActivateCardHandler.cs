// ============================================================
// ActivateCardHandler.cs
// NexaPay.Application/Features/Cards/Commands/ActivateCard
// ============================================================
// Aktiverar ett bankkort som befinner sig i status Inactive.
//
// Affärsregler:
//   1. Kortet måste finnas
//   2. Kortet måste tillhöra den inloggade användaren (om inte personal)
//   3. Endast Inactive-kort kan aktiveras
//   4. Sätt status till Active och spara
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Cards.Commands.ActivateCard
{
    public class ActivateCardHandler : IRequestHandler<ActivateCardCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ActivateCardHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(
            ActivateCardCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var card = await _unitOfWork.Cards.GetByIdAsync(request.CardId);

                if (card == null)
                    return Result.Failure(
                        $"Kort med ID {request.CardId} hittades inte");

                if (!request.IsStaff)
                {
                    var account = await _unitOfWork.Accounts
                        .GetByIdAsync(card.AccountId);

                    if (account == null || account.OwnerId != request.UserId)
                        return Result.Failure(
                            $"Kort med ID {request.CardId} hittades inte");
                }

                if (card.Status == CardStatus.Active)
                    return Result.Failure("Kortet är redan aktivt");

                if (card.Status == CardStatus.Blocked)
                    return Result.Failure(
                        "Kan inte aktivera ett blockerat kort");

                if (card.Status == CardStatus.Expired)
                    return Result.Failure(
                        "Kan inte aktivera ett utgånget kort");

                card.Status = CardStatus.Active;
                card.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Cards.Update(card);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(
                    $"Ett fel uppstod när kortet skulle aktiveras: {ex.Message}");
            }
        }
    }
}
