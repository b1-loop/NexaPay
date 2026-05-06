// ============================================================
// ActivateCardValidator.cs
// NexaPay.Application/Features/Cards/Commands/ActivateCard
// ============================================================

using FluentValidation;

namespace NexaPay.Application.Features.Cards.Commands.ActivateCard
{
    public class ActivateCardValidator : AbstractValidator<ActivateCardCommand>
    {
        public ActivateCardValidator()
        {
            RuleFor(x => x.CardId)
                .NotEmpty()
                .WithMessage("Kort-ID är obligatoriskt");

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("Användar-ID är obligatoriskt");
        }
    }
}
