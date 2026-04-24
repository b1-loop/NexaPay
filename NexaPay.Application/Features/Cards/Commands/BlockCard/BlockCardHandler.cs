// ============================================================
// BlockCardHandler.cs
// NexaPay.Application/Features/Cards/Commands/BlockCard
// ============================================================
// Blockerar ett bankkort.
//
// Affärsregler:
//   1. Kortet måste finnas
//   2. Kortet får inte redan vara blockerat
//   3. Kortet får inte vara utgånget
//   4. Sätt status till Blocked och spara
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Domain.Enums;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Cards.Commands.BlockCard
{
    public class BlockCardHandler : IRequestHandler<BlockCardCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public BlockCardHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(
            BlockCardCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Steg 1: Hämta kortet
                var card = await _unitOfWork.Cards.GetByIdAsync(request.CardId);

                if (card == null)
                    return Result.Failure(
                        $"Kort med ID {request.CardId} hittades inte");

                // Steg 2: Kontrollera nuvarande status
                // Redan blockerat kort behöver inte blockeras igen
                if (card.Status == CardStatus.Blocked)
                    return Result.Failure("Kortet är redan blockerat");

                // Utgånget kort kan inte blockeras – det är redan inaktivt
                if (card.Status == CardStatus.Expired)
                    return Result.Failure(
                        "Kan inte blockera ett utgånget kort");

                // Steg 3: Blockera kortet
                // Sätt status till Blocked
                card.Status = CardStatus.Blocked;
                card.UpdatedAt = DateTime.UtcNow;

                // Spara ändringen
                _unitOfWork.Cards.Update(card);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(
                    $"Ett fel uppstod när kortet skulle blockeras: {ex.Message}");
            }
        }
    }
}