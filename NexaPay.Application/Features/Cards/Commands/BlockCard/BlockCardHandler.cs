// ============================================================
// BlockCardHandler.cs
// NexaPay.Application/Features/Cards/Commands/BlockCard
// ============================================================
// Blockerar ett kort via Card.Block(). Endast Admin och
// BankManager får anropa endpointen (kontrolleras i Controller).
// Domän-eventet CardBlocked publiceras vidare till sin handler
// som skickar notifiering till ägaren.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
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
                var card = await _unitOfWork.Cards.GetByIdAsync(request.CardId, cancellationToken);

                if (card == null)
                    return Result.NotFound(
                        $"Kort med ID {request.CardId} hittades inte");

                card.Block();

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(ex.Message);
            }
        }
    }
}
