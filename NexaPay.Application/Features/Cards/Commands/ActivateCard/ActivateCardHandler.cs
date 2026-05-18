// ============================================================
// ActivateCardHandler.cs
// NexaPay.Application/Features/Cards/Commands/ActivateCard
// ============================================================
// Aktiverar ett kort via Card.Activate(). Personal får aktivera
// vilket kort som helst, vanliga användare bara sina egna.
// Vi slår upp ägar-kontot för behörighetskoll först.
// ============================================================

using MediatR;
using NexaPay.Application.Common.Models;
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
                var card = await _unitOfWork.Cards.GetByIdAsync(request.CardId, cancellationToken);

                if (card == null)
                    return Result.NotFound(
                        $"Kort med ID {request.CardId} hittades inte");

                if (!request.IsStaff)
                {
                    var account = await _unitOfWork.Accounts
                        .GetByIdAsync(card.AccountId, cancellationToken);

                    if (account == null || account.OwnerId != request.UserId)
                        return Result.NotFound(
                            $"Kort med ID {request.CardId} hittades inte");
                }

                card.Activate();

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
